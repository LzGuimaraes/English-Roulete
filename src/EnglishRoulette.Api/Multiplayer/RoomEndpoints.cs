using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using EnglishRoulette.Api.Admin;
using EnglishRoulette.Application.Admin;
using EnglishRoulette.Application.Multiplayer;
using EnglishRoulette.Application.Presentation;
using EnglishRoulette.Contracts;
using EnglishRoulette.Contracts.Admin;
using EnglishRoulette.Domain;
using EnglishRoulette.Domain.Multiplayer;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;

namespace EnglishRoulette.Api.Multiplayer;

internal static class RoomEndpoints
{
    public const string PlayerScheme = "Player";
    public static void AddRooms(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<RoomGames>();
        builder.Services.AddHostedService<RoomMatchWorker>();
        builder.Services.AddSignalR(options => options.MaximumReceiveMessageSize = 4096);
        builder.Services.AddAuthentication().AddCookie(PlayerScheme, options =>
        {
            options.Cookie.Name = "englishroulette.player"; options.Cookie.Path = "/";
            options.Cookie.HttpOnly = true; options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromDays(30); options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        builder.Services.AddAuthorization(options => options.AddPolicy("PlayerOnly", policy =>
            policy.AddAuthenticationSchemes(PlayerScheme).RequireAuthenticatedUser()));
        builder.Services.AddRateLimiter(options => options.AddPolicy("RoomEntry", context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions
            { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true })));
    }
    public static void MapRooms(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api/rooms") || context.Request.Path.StartsWithSegments("/hubs/rooms"))
            {
                context.Response.Headers.CacheControl = "no-store";
                if (context.Request.ContentLength > 4096) { context.Response.StatusCode = 413; return; }
                var origin = context.Request.Headers.Origin.ToString();
                if (!string.IsNullOrEmpty(origin) && origin != $"{context.Request.Scheme}://{context.Request.Host}")
                { context.Response.StatusCode = 403; return; }
            }
            await next(context);
        });
        app.MapGet("/api/rooms/avatars", () => Results.Ok(LobbyRoom.Avatars));
        app.MapPost("/api/rooms/identity", async (GuestRequest request, HttpContext context, ILobbyStore lobbies) =>
        {
            if (context.Request.Headers["X-English-Roulette"] != "demo") { return Results.StatusCode(403); }
            var previous = await context.AuthenticateAsync(PlayerScheme);
            var id = Guid.TryParse(previous.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var value) ? (Guid?)value : null;
            var guest = await lobbies.SaveGuestAsync(id, request.Nickname ?? "", request.Avatar ?? "", context.RequestAborted);
            var identity = new ClaimsIdentity([new(ClaimTypes.NameIdentifier, guest.Id.ToString()), new(ClaimTypes.Name, guest.Nickname)], PlayerScheme);
            await context.SignInAsync(PlayerScheme, new ClaimsPrincipal(identity));
            return Results.Ok(new GuestView(guest.Id, guest.Nickname, guest.Avatar));
        }).RequireRateLimiting("RoomEntry");
        var group = app.MapGroup("/api/rooms").RequireAuthorization("PlayerOnly").AddEndpointFilter(AdminSecurity.ValidateCsrf);
        group.MapGet("/antiforgery", (HttpContext context, IAntiforgery antiforgery) => Results.Ok(new AntiforgeryView(antiforgery.GetAndStoreTokens(context).RequestToken!)));
        group.MapGet("/me", async (HttpContext context, ILobbyStore lobbies) =>
            await lobbies.GuestAsync(Actor(context), context.RequestAborted) is { } guest
                ? Results.Ok(new GuestView(guest.Id, guest.Nickname, guest.Avatar)) : Results.Unauthorized());
        group.MapGet("/current", async (HttpContext context, ILobbyStore lobbies, RoomGames games) =>
            await lobbies.CurrentAsync(Actor(context), context.RequestAborted) is { } state ? Results.Ok(await games.ViewAsync(state, Actor(context), context.RequestAborted)) : Results.NotFound());
        group.MapGet("/{id:guid}", async (Guid id, HttpContext context, ILobbyStore lobbies, RoomGames games) =>
            await lobbies.ReadAsync(id, Actor(context), context.RequestAborted) is { } state ? Results.Ok(await games.ViewAsync(state, Actor(context), context.RequestAborted)) : Results.NotFound());
        group.MapPost("/", async (CreateRoomRequest request, HttpContext context, ILobbyStore lobbies, RoomGames games) =>
            Results.Ok(await games.ViewAsync(await lobbies.CreateAsync(Actor(context), request.Capacity, context.RequestAborted), Actor(context), context.RequestAborted))).RequireRateLimiting("RoomEntry");
        group.MapPost("/join", async (JoinRoomRequest request, HttpContext context, ILobbyStore lobbies, RoomGames games, IHubContext<RoomHub> hub) =>
        {
            var state = await lobbies.JoinAsync(Actor(context), request.Code ?? "", context.RequestAborted);
            await Notify(hub, state); return Results.Ok(await games.ViewAsync(state, Actor(context), context.RequestAborted));
        }).RequireRateLimiting("RoomEntry");
        group.MapPost("/{id:guid}/ready", async (Guid id, ReadyRoomRequest request, HttpContext context, ILobbyStore lobbies, RoomGames games, IHubContext<RoomHub> hub) =>
        {
            var state = await lobbies.ChangeAsync(id, Actor(context), (state, now) => LobbyRoom.SetReady(state, Actor(context), request.ExpectedStateVersion, request.Ready, now), context.RequestAborted);
            await Notify(hub, state); return Results.Ok(await games.ViewAsync(state, Actor(context), context.RequestAborted));
        }).RequireRateLimiting("AdminWrite");
        group.MapPost("/{id:guid}/spin", async (Guid id, SpinRoomRequest request, HttpContext context, ILobbyStore lobbies,
            IAdminCatalog catalog, RoomGames games, IHubContext<RoomHub> hub) =>
        {
            var selected = StoryRoulette.Draw(await catalog.PublishedAsync(context.RequestAborted));
            var content = await catalog.LoadPublishedAsync(selected, context.RequestAborted)
                ?? throw new GameRuleException("INVALID_CONTENT", "O catálogo mudou. Tente girar novamente.");
            var state = await lobbies.ChangeAsync(id, Actor(context), (state, now) =>
            {
                var spun = LobbyRoom.Spin(state, Actor(context), request.ExpectedStateVersion, selected, now);
                return spun with { Match = RoomGames.Start(content, spun, now) };
            }, context.RequestAborted);
            await Notify(hub, state); return Results.Ok(await games.ViewAsync(state, Actor(context), context.RequestAborted));
        }).RequireRateLimiting("AdminWrite");
        group.MapPost("/{id:guid}/commands", async (Guid id, MatchCommandRequest request, HttpContext context,
            ILobbyStore lobbies, RoomGames games, IHubContext<RoomHub> hub) =>
        {
            var state = await lobbies.ChangeAsync(id, Actor(context), (state, now) => state with
            {
                Match = MultiplayerGame.Execute(state.Match ?? throw new GameRuleException("INVALID_GAME_PHASE", "A partida ainda não começou."),
                    Actor(context), new(request.RequestId, request.ExpectedStateVersion, request.SceneInstanceId, request.Kind, request.Key),
                    state.Members, now, RandomNumberGenerator.GetInt32),
                StateVersion = state.StateVersion + 1
            }, context.RequestAborted);
            await Notify(hub, state); return Results.Ok(await games.ViewAsync(state, Actor(context), context.RequestAborted));
        }).RequireRateLimiting("AdminWrite");
        group.MapPost("/{id:guid}/leave", async (Guid id, HttpContext context, ILobbyStore lobbies, IHubContext<RoomHub> hub) =>
        {
            var state = await lobbies.ChangeAsync(id, Actor(context), (state, now) => LobbyRoom.Leave(state, Actor(context), now), context.RequestAborted);
            await Notify(hub, state); return Results.NoContent();
        }).RequireRateLimiting("AdminWrite");
        app.MapHub<RoomHub>("/hubs/rooms").RequireAuthorization("PlayerOnly");
    }
    private static Task Notify(IHubContext<RoomHub> hub, LobbyState state) => hub.Clients.Group(RoomHub.Group(state.Id)).SendAsync("RoomChanged", state.Id, state.StateVersion);
    private static Guid Actor(HttpContext context) => Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
