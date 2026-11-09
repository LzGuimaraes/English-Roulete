using System.Security.Claims;
using EnglishRoulette.Application.Admin;
using EnglishRoulette.Contracts;
using EnglishRoulette.Contracts.Admin;
using EnglishRoulette.Domain;
using EnglishRoulette.Domain.Challenges;
using EnglishRoulette.Domain.Stories;
using EnglishRoulette.Infrastructure.Admin;
using EnglishRoulette.Infrastructure.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EnglishRoulette.Api.Admin;

internal static class AdminEndpoints
{
    public static void MapAdmin(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api/admin"))
            {
                context.Response.Headers.CacheControl = "no-store";
                var limit = context.Request.Path.Value is { } uploadPath && (uploadPath.EndsWith("/audio", StringComparison.Ordinal) || uploadPath.EndsWith("/visual", StringComparison.Ordinal))
                    ? CatalogMediaStorage.MaximumUploadBytes + 16384 : 1024 * 1024;
                if (context.Request.ContentLength > limit)
                { await Results.Problem(statusCode: 413, title: "O arquivo ou formulário excede o limite permitido.").ExecuteAsync(context); return; }
                var feature = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
                if (feature is { IsReadOnly: false }) { feature.MaxRequestBodySize = limit; }
            }
            await next(context);
        });
        app.MapGet("/api/admin/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
            Results.Ok(new AntiforgeryView(antiforgery.GetAndStoreTokens(context).RequestToken ?? "")));
        app.MapPost("/api/admin/login", async (AdminLoginRequest request, SignInManager<AdminUser> signIn) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 256 ||
                string.IsNullOrEmpty(request.Password) || request.Password.Length > 1024)
            { return Results.Problem(statusCode: 401, title: "E-mail ou senha inválidos, ou acesso temporariamente bloqueado."); }
            var result = await signIn.PasswordSignInAsync(request.Email.Trim(), request.Password, false, lockoutOnFailure: true);
            return result.Succeeded ? Results.Ok() : Results.Problem(statusCode: 401,
                title: "E-mail ou senha inválidos, ou acesso temporariamente bloqueado.");
        }).AddEndpointFilter(AdminSecurity.ValidateCsrf).RequireRateLimiting("AdminLogin");

        var group = app.MapGroup("/api/admin").RequireAuthorization("AdminOnly").AddEndpointFilter(AdminSecurity.ValidateCsrf);
        group.MapGet("/me", (HttpContext context) => Results.Ok(new AdminUserView(context.User.Identity?.Name ?? "")));
        group.MapPost("/logout", async (SignInManager<AdminUser> signIn) => { await signIn.SignOutAsync(); return Results.NoContent(); });
        group.MapPost("/password", async (HttpContext context, ChangePasswordRequest request,
            UserManager<AdminUser> users, SignInManager<AdminUser> signIn) =>
        {
            var user = await users.GetUserAsync(context.User);
            if (user is null) { return Results.Unauthorized(); }
            if (string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length > 1024)
            { return Results.Problem(statusCode: 400, title: "Informe a senha atual e uma nova senha válida."); }
            var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
            {
                return Results.Problem(statusCode: 400, title: "Não foi possível trocar a senha.",
                detail: "Confira a senha atual. A nova senha precisa ter 12 caracteres, maiúscula, minúscula, número e símbolo.");
            }
            await signIn.RefreshSignInAsync(user);
            return Results.NoContent();
        }).RequireRateLimiting("AdminWrite");

        group.MapGet("/stories", async (int? page, IAdminCatalog catalog, CancellationToken token) =>
            Results.Ok((await catalog.ListAsync(page ?? 1, token)).Select(AdminContracts.Summary)));
        group.MapGet("/stories/{id:guid}", async (Guid id, IAdminCatalog catalog, CancellationToken token) =>
            await catalog.FindAsync(id, token) is { } draft ? Results.Ok(AdminContracts.View(draft)) : Results.NotFound());
        group.MapPost("/stories", async (HttpContext context, IAdminCatalog catalog) =>
            Results.Ok(AdminContracts.View(await catalog.CreateAsync(Actor(context), context.RequestAborted)))).RequireRateLimiting("AdminWrite");
        group.MapPut("/stories/{id:guid}", async (Guid id, AdminStoryEditor editor, HttpContext context, IAdminCatalog catalog) =>
            await catalog.FindAsync(id, context.RequestAborted) is null ? Results.NotFound() :
                Results.Ok(AdminContracts.View(await catalog.SaveAsync(AdminContracts.Draft(editor, id), Actor(context), context.RequestAborted))))
            .RequireRateLimiting("AdminWrite");
        group.MapPost("/stories/{id:guid}/duplicate", async (Guid id, HttpContext context, IAdminCatalog catalog) =>
            Results.Ok(AdminContracts.View(await catalog.DuplicateAsync(id, Actor(context), context.RequestAborted)))).RequireRateLimiting("AdminWrite");
        group.MapGet("/stories/{id:guid}/validation", async (Guid id, IAdminCatalog catalog, CancellationToken token) =>
        {
            var check = await catalog.ValidateAsync(id, token);
            return Results.Ok(new PublicationCheckView(check.CanPublish, check.Issues));
        });
        group.MapGet("/stories/{id:guid}/preview", async (Guid id, long revision, string? currentSceneKey,
            string? transitionKey, IAdminCatalog catalog, CancellationToken token) =>
        {
            var draft = await PreviewDraft(catalog, id, revision, token);
            var story = CatalogRules.ToStory(draft);
            var scene = currentSceneKey is null ? StoryFlowEngine.GetInitialScene(story)
                : StoryFlowEngine.GetNextScene(story, currentSceneKey, transitionKey);
            if (scene is null) { return Results.NoContent(); }
            var source = draft.Scenes.Single(item => item.Key == scene.Key);
            return Results.Ok(new PreviewSceneView(scene.Key, source.Title, scene.Narration, scene.IsEnding,
                scene.Challenge.Prompt, scene.Challenge.Options.Select(option => new OptionView(option.Key, option.Text)).ToArray(),
                scene.Transitions.Select(choice => new ChoiceView(choice.Key, choice.Label)).ToArray(),
                source.Audio is { } audio ? $"/api/admin/media/{audio.Id}" : null,
                source.Visual is { } visual ? new SceneVisualView($"/api/admin/visuals/{visual.Id}", source.Support?.VisualDescription ?? visual.AltText, visual.Animated) : null,
                AdminContracts.Appearance(draft.Metadata.Appearance ?? new()), draft.Metadata.Language));
        });
        group.MapPost("/stories/{id:guid}/preview-answer", async (Guid id, PreviewAnswerRequest request,
            IAdminCatalog catalog, CancellationToken token) =>
        {
            var draft = await PreviewDraft(catalog, id, request.Revision, token);
            var scene = CatalogRules.ToStory(draft).Scenes.SingleOrDefault(scene => scene.Key == request.SceneKey)
                ?? throw new GameRuleException("INVALID_CONTENT", "Cena inválida.");
            var answer = ChallengeEvaluator.Evaluate(scene.Challenge, request.OptionKey);
            return Results.Ok(new PreviewAnswerView(answer.IsCorrect, answer.Explanation));
        }).RequireRateLimiting("AdminWrite");
        group.MapPost("/stories/{id:guid}/publish", async (Guid id, PublishRequest request, HttpContext context, IAdminCatalog catalog) =>
        {
            await catalog.PublishAsync(id, request.Revision, request.Reviewed, request.RightsConfirmed, Actor(context), context.RequestAborted);
            return Results.NoContent();
        }).RequireRateLimiting("AdminWrite");
        group.MapPost("/stories/{id:guid}/archive", async (Guid id, RevisionRequest request, HttpContext context, IAdminCatalog catalog) =>
        {
            if (await catalog.FindAsync(id, context.RequestAborted) is null) { return Results.NotFound(); }
            await catalog.ArchiveAsync(id, request.Revision, Actor(context), context.RequestAborted);
            return Results.NoContent();
        }).RequireRateLimiting("AdminWrite");
        group.MapPost("/stories/{id:guid}/scenes/{sceneId:guid}/audio", async (Guid id, Guid sceneId,
            long revision, HttpContext context, CatalogMediaStorage media, IAdminCatalog catalog) =>
        {
            if (!context.Request.HasFormContentType) { return Results.Problem(statusCode: 400, title: "Envie o arquivo de áudio."); }
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length > CatalogMediaStorage.MaximumUploadBytes || file.Length == 0)
            { return Results.Problem(statusCode: 400, title: "Escolha um WAV de até 10 MB."); }
            await using var stream = file.OpenReadStream();
            await media.UploadAsync(id, sceneId, revision, stream, form["attribution"].ToString(), form["licenseEvidence"].ToString(),
                Actor(context), context.RequestAborted);
            var draft = await catalog.FindAsync(id, context.RequestAborted);
            return draft is null ? Results.NotFound() : Results.Ok(AdminContracts.View(draft));
        }).RequireRateLimiting("AdminWrite");
        group.MapPost("/stories/{id:guid}/scenes/{sceneId:guid}/visual", async (Guid id, Guid sceneId, long revision,
            HttpContext context, CatalogVisualStorage storage, IAdminCatalog catalog) =>
        {
            if (!context.Request.HasFormContentType) { return Results.BadRequest(); }
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length is <= 0 or > CatalogVisualStorage.MaximumBytes)
            { return Results.Problem(statusCode: 400, title: "Escolha PNG/APNG, JPEG ou GIF de até 8 MB."); }
            await using var stream = file.OpenReadStream();
            await storage.UploadAsync(id, sceneId, revision, stream, form["altText"].ToString(), form["attribution"].ToString(),
                form["licenseEvidence"].ToString(), Actor(context), context.RequestAborted);
            return Results.Ok(AdminContracts.View((await catalog.FindAsync(id, context.RequestAborted))!));
        }).RequireRateLimiting("AdminWrite");
        group.MapGet("/visuals/{id:guid}", async (Guid id, HttpContext context, AdventureDbContext db, CatalogVisualStorage storage, CancellationToken token) =>
        {
            context.Response.Headers.XContentTypeOptions = "nosniff";
            return await db.CatalogVisuals.AsNoTracking().SingleOrDefaultAsync(asset => asset.Id == id, token) is { } asset && storage.PathFor(asset) is { } path
                ? Results.File(path, asset.ContentType) : Results.NotFound();
        });
        app.MapGet("/media/visuals/{id:guid}", async (Guid id, HttpContext context, AdventureDbContext db, CatalogVisualStorage storage, CancellationToken token) =>
        {
            context.Response.Headers.XContentTypeOptions = "nosniff";
            return await db.CatalogVisuals.AsNoTracking().SingleOrDefaultAsync(asset => asset.Id == id && asset.Published, token) is { } asset && storage.PathFor(asset) is { } path
                ? Results.File(path, asset.ContentType) : Results.NotFound();
        });
        group.MapGet("/media/{id:guid}", async (Guid id, AdventureDbContext db, CatalogMediaStorage storage, CancellationToken token) =>
            await db.CatalogMedia.AsNoTracking().SingleOrDefaultAsync(asset => asset.Id == id, token) is { } asset && storage.PathFor(asset) is { } path
                ? Results.File(path, "audio/wav", enableRangeProcessing: true) : Results.NotFound());
        app.MapGet("/media/catalog/{id:guid}", async (Guid id, AdventureDbContext db, CatalogMediaStorage storage, CancellationToken token) =>
            await db.CatalogMedia.AsNoTracking().SingleOrDefaultAsync(asset => asset.Id == id && asset.Published, token) is { } asset && storage.PathFor(asset) is { } path
                ? Results.File(path, "audio/wav", enableRangeProcessing: true) : Results.NotFound());
        app.MapGet("/api/catalog", async (string? language, AdventureDbContext db, CancellationToken token) =>
        {
            language = StoryLanguage.Require(language);
            var rows = await db.CatalogVersions.AsNoTracking().Where(version => version.Status == "Published" && version.Language == language)
                .OrderBy(version => version.CreatedAtUtc).Take(100).Select(version => new
                { version.Id, version.Title, version.Synopsis, version.Genre, version.Difficulty, Count = version.Scenes.Count, version.AppearanceJson, version.Language }).ToListAsync(token);
            return Results.Ok(rows.Select(version => new PublicStoryView(version.Id, version.Title, version.Synopsis, version.Genre,
                version.Difficulty, version.Count, AdminContracts.Appearance(CatalogService.AppearanceFromJson(version.AppearanceJson)), version.Language)));
        });
        app.MapGet("/api/catalog/round-preview", async (string? language, IAdminCatalog catalog, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            language = StoryLanguage.Require(language);
            var first = (await catalog.PublishedAsync(context.RequestAborted)).FirstOrDefault(story => story.Language == language);
            var content = first is null ? null : await catalog.LoadPublishedAsync(first.Id, context.RequestAborted);
            if (content is null) { return Results.NoContent(); }
            var scene = StoryFlowEngine.GetInitialScene(content.Story);
            const int maximumExcerptLength = 420;
            var excerpt = scene.Narration;
            if (excerpt.Length > maximumExcerptLength)
            {
                var boundary = excerpt.LastIndexOf(' ', maximumExcerptLength);
                excerpt = excerpt[..(boundary > 0 ? boundary : maximumExcerptLength)] + "…";
            }
            return Results.Ok(new RoundPreviewView(content.Metadata.Title, content.SceneTitles[scene.Key],
                content.Metadata.Genre, content.Metadata.Difficulty, excerpt, scene.Challenge.Prompt,
                scene.Challenge.Options.Select(option => option.Text).ToArray(), content.Metadata.Language));
        });
    }

    private static async Task<CatalogDraft> PreviewDraft(IAdminCatalog catalog, Guid id, long revision, CancellationToken token)
    {
        var draft = await catalog.FindAsync(id, token) ?? throw new GameRuleException("NOT_FOUND", "História não encontrada.");
        if (draft.Revision != revision) { throw new GameRuleException("STATE_CONFLICT", "Recarregue a versão antes de simular."); }
        try { StoryFlowEngine.ValidateForPlay(CatalogRules.ToStory(draft)); }
        catch (Exception exception) when (exception is ArgumentException or GameRuleException)
        { throw new GameRuleException("INVALID_CONTENT", "Complete as perguntas, a cena inicial e os caminhos antes de simular."); }
        return draft;
    }

    private static Guid Actor(HttpContext context) => Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("The admin identity is missing its ID."));
}
