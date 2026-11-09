using EnglishRoulette.Api;
using EnglishRoulette.Api.Admin;
using EnglishRoulette.Api.Demo;
using EnglishRoulette.Api.Multiplayer;
using EnglishRoulette.Application.Admin;
using EnglishRoulette.Application.Multiplayer;
using EnglishRoulette.Application.Solo;
using EnglishRoulette.Contracts;
using EnglishRoulette.Domain;
using EnglishRoulette.Infrastructure.Admin;
using EnglishRoulette.Infrastructure.Multiplayer;
using EnglishRoulette.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

var bootstrapAdmin = args.Contains("--bootstrap-admin");
var migrate = args.Contains("--migrate");
var resetAdmin = args.Contains("--reset-admin-password");
var builder = WebApplication.CreateBuilder(args.Where(argument => argument is not ("--bootstrap-admin" or "--reset-admin-password" or "--migrate")).ToArray());
_ = GameAssistance.Rules(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainRuleExceptionHandler>();
var connection = builder.Configuration.GetConnectionString("Adventure")
    ?? throw new InvalidOperationException("Configure ConnectionStrings__Adventure. See scripts/dev.py setup.");
builder.Services.AddDbContext<AdventureDbContext>(options => options.UseNpgsql(connection));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ISoloSessionStore, PostgresSoloSessionStore>();
builder.Services.AddScoped<DemoSessions>();
builder.Services.AddSingleton<DemoMediaCatalog>();
builder.AddAdminSecurity();
builder.AddRooms();
builder.Services.AddScoped<ILobbyStore, LobbyStore>();
builder.Services.AddScoped<IAdminCatalog, CatalogService>();
builder.Services.AddScoped<CatalogVisualStorage>();
builder.Services.AddScoped(provider => new CatalogMediaStorage(
    builder.Configuration["Media:Root"] ?? Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../media")),
    provider.GetRequiredService<AdventureDbContext>(), provider.GetRequiredService<TimeProvider>()));
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    options.MultipartBodyLengthLimit = CatalogMediaStorage.MaximumUploadBytes + 16384);
var proxyNetwork = builder.Configuration["Proxy:TrustedNetwork"];
if (!string.IsNullOrWhiteSpace(proxyNetwork))
{
    builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
            Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(proxyNetwork));
        options.ForwardLimit = 1;
    });
}
var app = builder.Build();
if (migrate)
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<AdventureDbContext>();
    var assembly = database.GetService<IMigrationsAssembly>();
    foreach (var pending in await database.Database.GetPendingMigrationsAsync())
    {
        var migration = assembly.CreateMigration(assembly.Migrations[pending], database.Database.ProviderName!);
        if (migration.UpOperations.Any(operation => operation.IsDestructiveChange || operation is Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation))
        { throw new InvalidOperationException("Automatic deployment refuses destructive or raw SQL migrations. Review and apply explicitly before deploying."); }
    }
    await database.Database.MigrateAsync();
    return;
}
if (bootstrapAdmin)
{
    await AdminBootstrap.RunAsync(app.Services, builder.Configuration, resetAdmin);
    return;
}
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();
app.UseStaticFiles();
app.MapStaticAssets();
app.MapAdmin();
app.MapRooms();

app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", async (AdventureDbContext db, CancellationToken cancellationToken) =>
    await db.Database.CanConnectAsync(cancellationToken) &&
    !(await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any()
        ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503));

if (app.Environment.IsDevelopment())
{
    var media = app.Services.GetRequiredService<DemoMediaCatalog>();
    app.MapGet("/media/audio/{hash}.wav", (string hash, HttpContext context) =>
    {
        var path = media.FilePath(hash);
        if (path is null) { return Results.NotFound(); }
        context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        return Results.File(path, "audio/wav", enableRangeProcessing: true);
    });
}

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/demo"))
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) && context.Request.Headers["X-English-Roulette"] != "demo")
        {
            await Results.Problem(statusCode: 403, title: "Requisição inválida.").ExecuteAsync(context);
            return;
        }
    }

    await next(context);
});
app.MapGet("/api/demo/session", async (HttpContext context, DemoSessions sessions) =>
    await sessions.GetAsync(context, context.RequestAborted) is { } state ? Results.Ok(state) : Results.NotFound());
app.MapPost("/api/demo/session", async (HttpContext context, CreateAdventureRequest request, DemoSessions sessions) =>
    Results.Ok(await sessions.CreateAsync(context, request.StoryVersionId, context.RequestAborted, request.Language)));
app.MapDelete("/api/demo/session", async (HttpContext context, DemoSessions sessions) =>
{
    await sessions.EndAsync(context, context.RequestAborted);
    return Results.NoContent();
});
app.MapPost("/api/demo/commands", async (HttpContext context, AdventureCommand command, DemoSessions sessions) =>
{
    try
    {
        return await sessions.ExecuteAsync(context, command, context.RequestAborted) is { } state
            ? Results.Ok(state) : Results.NotFound();
    }
    catch (ArgumentException)
    {
        return Results.Problem(statusCode: 400, title: "Comando inválido.");
    }
});

app.Map("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");
app.Run();

public partial class Program { }
