using EnglishRoulette.Api;
using EnglishRoulette.Api.Demo;
using EnglishRoulette.Application.Solo;
using EnglishRoulette.Contracts;
using EnglishRoulette.Domain;
using EnglishRoulette.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainRuleExceptionHandler>();
var connection = builder.Configuration.GetConnectionString("Adventure")
    ?? throw new InvalidOperationException("Configure ConnectionStrings__Adventure. See scripts/dev.py setup.");
builder.Services.AddDbContext<AdventureDbContext>(options => options.UseNpgsql(connection));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ISoloSessionStore, PostgresSoloSessionStore>();
builder.Services.AddScoped<DemoSessions>();
builder.Services.AddSingleton<DemoMediaCatalog>();
var app = builder.Build();
app.UseExceptionHandler();
app.UseStaticFiles();
app.MapStaticAssets();

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
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/api/demo"))
        {
            context.Response.Headers.CacheControl = "no-store";
            if (HttpMethods.IsPost(context.Request.Method) && context.Request.Headers["X-English-Roulette"] != "demo")
            {
                await Results.Problem(statusCode: 403, title: "Requisição inválida.").ExecuteAsync(context);
                return;
            }
        }

        await next(context);
    });
    app.MapGet("/api/demo/session", async (HttpContext context, DemoSessions sessions) =>
        await sessions.GetAsync(context, context.RequestAborted) is { } state ? Results.Ok(state) : Results.NotFound());
    app.MapPost("/api/demo/session", async (HttpContext context, DemoSessions sessions) =>
        Results.Ok(await sessions.CreateAsync(context, context.RequestAborted)));
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
}

app.Map("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");
app.Run();
