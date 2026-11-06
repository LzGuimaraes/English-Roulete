using EnglishRoulette.Api.Demo;
using EnglishRoulette.Contracts;
using EnglishRoulette.Domain;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<DemoSessions>();
var app = builder.Build();
app.UseExceptionHandler();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));

if (app.Environment.IsDevelopment())
{
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
    app.MapGet("/api/demo/session", (HttpContext context, DemoSessions sessions) =>
        sessions.Get(context) is { } state ? Results.Ok(state) : Results.NotFound());
    app.MapPost("/api/demo/session", (HttpContext context, DemoSessions sessions) => Results.Ok(sessions.Create(context)));
    app.MapPost("/api/demo/commands", (HttpContext context, AdventureCommand command, DemoSessions sessions) =>
    {
        try
        {
            return sessions.Execute(context, command) is { } state ? Results.Ok(state) : Results.NotFound();
        }
        catch (GameRuleException exception)
        {
            return Results.Problem(statusCode: 409, title: "O estado da aventura mudou.", detail: exception.Code);
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
