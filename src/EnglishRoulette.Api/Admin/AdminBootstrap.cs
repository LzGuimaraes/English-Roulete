using EnglishRoulette.Api.Demo;
using EnglishRoulette.Application.Admin;
using EnglishRoulette.Infrastructure.Admin;
using EnglishRoulette.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EnglishRoulette.Api.Admin;

internal static class AdminBootstrap
{
    public static async Task RunAsync(IServiceProvider services, IConfiguration configuration, bool resetPassword)
    {
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AdminUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var email = configuration["Admin:BootstrapEmail"] ?? throw new InvalidOperationException("Set Admin__BootstrapEmail.");
        var password = configuration["Admin:BootstrapPassword"] ?? throw new InvalidOperationException("Set Admin__BootstrapPassword.");
        if (!await roles.RoleExistsAsync("ADMIN")) { Check(await roles.CreateAsync(new("ADMIN") { Id = Guid.NewGuid() })); }
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new() { Id = Guid.NewGuid(), Email = email, UserName = email, EmailConfirmed = true };
            Check(await users.CreateAsync(user, password));
        }
        else if (resetPassword)
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            Check(await users.ResetPasswordAsync(user, token, password));
        }
        if (!await users.IsInRoleAsync(user, "ADMIN")) { Check(await users.AddToRoleAsync(user, "ADMIN")); }
        if (configuration.GetValue<bool>("Admin:SeedDemo")) { await SeedDemoAsync(scope.ServiceProvider, user.Id); }
        Console.WriteLine("Administrator provisioned. Credentials remain in the local environment file; existing passwords were preserved unless reset was requested.");
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) { throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description))); }
    }

    private static async Task SeedDemoAsync(IServiceProvider services, Guid adminId)
    {
        var db = services.GetRequiredService<AdventureDbContext>();
        var source = DemoStory.Create();
        if (await db.CatalogVersions.AnyAsync(version => version.Id == source.Id)) { return; }
        var media = services.GetRequiredService<DemoMediaCatalog>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var root = new CatalogStoryRow { Id = Guid.NewGuid(), Slug = "notebook-at-platform-three", CreatedAtUtc = now };
        var row = new CatalogVersionRow
        {
            Id = source.Id,
            StoryId = root.Id,
            Number = 1,
            Status = "Published",
            Title = source.Title,
            Synopsis = "Um caderno perdido leva Mia a dois encontros possíveis.",
            Genre = "Mistério",
            Difficulty = "A2",
            Author = "English Roulette",
            LicenseType = "Original",
            LicenseEvidence = "Conteúdo original de demonstração do projeto.",
            Attribution = "English Roulette; narração sintética de desenvolvimento eSpeak NG.",
            InitialSceneKey = source.InitialSceneKey,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            PublishedAtUtc = now
        };
        db.CatalogStories.Add(root);
        var definition = EnglishRoulette.Domain.Stories.StoryDefinition.Capture(source);
        foreach (var scene in definition.Scenes)
        {
            var asset = media.Get(source.Id, scene.Key);
            var audio = new CatalogMediaRow
            {
                Id = Guid.NewGuid(),
                StorageKey = "audio/" + asset.Sha256 + ".wav",
                Sha256 = asset.Sha256,
                NarrationSha256 = CatalogMediaStorage.TextHash(scene.Narration),
                SizeBytes = asset.SizeBytes,
                DurationMs = asset.DurationMs,
                Attribution = row.Attribution,
                LicenseEvidence = row.LicenseEvidence,
                Published = true,
                CreatedAtUtc = now
            };
            db.CatalogMedia.Add(audio);
            var entry = new CatalogScene(scene.Challenge.Id, scene.Key, DemoStory.Describe(scene.Key).Title, scene.Narration, scene.IsEnding,
                scene.Challenge.Prompt, scene.Challenge.Options.Select(option => new CatalogOption(option.Key, option.Text)).ToArray(),
                scene.Challenge.CorrectOptionKey, scene.Challenge.Explanation, scene.Transitions.Select(transition =>
                    new CatalogTransition(transition.Key, transition.Label, transition.TargetSceneKey)).ToArray(),
                new(audio.Id, audio.DurationMs, audio.Attribution, audio.LicenseEvidence));
            row.Scenes.Add(CatalogService.SceneRow(entry, row.Id, row.Scenes.Count, new Dictionary<Guid, CatalogMediaRow> { [audio.Id] = audio }));
        }
        db.CatalogVersions.Add(row);
        db.AdminAudit.Add(new() { Id = Guid.NewGuid(), ActorId = adminId, TargetId = row.Id, Action = "ImportExistingDemo", AtUtc = now });
        await db.SaveChangesAsync();
    }
}
