using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EnglishRoulette.Infrastructure.Admin;

internal static class AdminModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<AdminUser>().ToTable("admin_users");
        model.Entity<AdminUser>().HasIndex(user => user.NormalizedEmail).IsUnique();
        model.Entity<IdentityRole<Guid>>().ToTable("admin_roles");
        model.Entity<IdentityUserRole<Guid>>().ToTable("admin_user_roles");
        model.Entity<IdentityUserClaim<Guid>>().ToTable("admin_user_claims");
        model.Entity<IdentityUserLogin<Guid>>().ToTable("admin_user_logins");
        model.Entity<IdentityUserToken<Guid>>().ToTable("admin_user_tokens");
        model.Entity<IdentityRoleClaim<Guid>>().ToTable("admin_role_claims");

        model.Entity<CatalogStoryRow>().ToTable("catalog_stories").HasKey(row => row.Id);
        model.Entity<CatalogStoryRow>().HasIndex(row => row.Slug).IsUnique();
        model.Entity<CatalogStoryRow>().Property(row => row.Slug).HasMaxLength(160);
        model.Entity<CatalogVersionRow>().ToTable("catalog_versions", table =>
        {
            table.HasCheckConstraint("ck_catalog_status", "status IN ('Draft','Published','Archived')");
            table.HasCheckConstraint("ck_catalog_revision", "revision > 0 AND number > 0");
        }).HasKey(row => row.Id);
        model.Entity<CatalogVersionRow>().HasOne<CatalogStoryRow>().WithMany().HasForeignKey(row => row.StoryId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<CatalogVersionRow>().HasIndex(row => new { row.StoryId, row.Number }).IsUnique();
        model.Entity<CatalogVersionRow>().HasIndex(row => row.StoryId).IsUnique().HasFilter("status = 'Published'");
        model.Entity<CatalogVersionRow>().Property(row => row.Revision).IsConcurrencyToken();
        model.Entity<CatalogVersionRow>().HasMany(row => row.Scenes).WithOne().HasForeignKey(scene => scene.VersionId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<CatalogSceneRow>().ToTable("catalog_scenes").HasKey(row => row.Id);
        model.Entity<CatalogSceneRow>().Property(row => row.SupportJson).HasColumnType("jsonb").HasDefaultValue("{}");
        model.Entity<CatalogSceneRow>().HasIndex(row => new { row.VersionId, row.Key }).IsUnique();
        model.Entity<CatalogSceneRow>().HasOne(row => row.Visual).WithMany().HasForeignKey(row => row.VisualId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<CatalogSceneRow>().HasOne(row => row.Audio).WithMany().HasForeignKey(row => row.AudioId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<CatalogSceneRow>().HasMany(row => row.Options).WithOne().HasForeignKey(row => row.SceneId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<CatalogSceneRow>().HasMany(row => row.Transitions).WithOne().HasForeignKey(row => row.SceneId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<CatalogOptionRow>().ToTable("catalog_options").HasKey(row => row.Id);
        model.Entity<CatalogOptionRow>().HasIndex(row => new { row.SceneId, row.Key }).IsUnique();
        model.Entity<CatalogTransitionRow>().ToTable("catalog_transitions").HasKey(row => row.Id);
        model.Entity<CatalogTransitionRow>().HasIndex(row => new { row.SceneId, row.Key }).IsUnique();
        model.Entity<CatalogMediaRow>().ToTable("catalog_media", table => table.HasCheckConstraint("ck_catalog_media", "size_bytes > 0 AND duration_ms > 0")).HasKey(row => row.Id);
        model.Entity<CatalogVisualRow>().ToTable("catalog_visuals").HasKey(row => row.Id);
        model.Entity<AdminAuditRow>().ToTable("admin_audit").HasKey(row => row.Id);
        model.Entity<AdminAuditRow>().HasIndex(row => row.AtUtc);
        foreach (var entity in model.Model.GetEntityTypes().Where(entity => entity.ClrType != typeof(Persistence.StoredSessionRow)))
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(System.Text.RegularExpressions.Regex.Replace(property.Name, "(?<!^)([A-Z])", "_$1").ToLowerInvariant());
            }
        }
    }
}
