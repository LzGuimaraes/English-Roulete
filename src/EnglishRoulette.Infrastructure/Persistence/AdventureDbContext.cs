using EnglishRoulette.Infrastructure.Admin;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EnglishRoulette.Infrastructure.Persistence;

public sealed class StoredSessionRow
{
    public string CredentialHash { get; set; } = "";
    public string CheckpointJson { get; set; } = "";
    public long Revision { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class AdventureDbContext(DbContextOptions<AdventureDbContext> options) : IdentityDbContext<AdminUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<StoredSessionRow> SoloSessions => Set<StoredSessionRow>();
    public DbSet<CatalogStoryRow> CatalogStories => Set<CatalogStoryRow>();
    public DbSet<CatalogVersionRow> CatalogVersions => Set<CatalogVersionRow>();
    public DbSet<CatalogSceneRow> CatalogScenes => Set<CatalogSceneRow>();
    public DbSet<CatalogMediaRow> CatalogMedia => Set<CatalogMediaRow>();
    public DbSet<AdminAuditRow> AdminAudit => Set<AdminAuditRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        AdminModel.Configure(modelBuilder);
        var row = modelBuilder.Entity<StoredSessionRow>();
        row.ToTable("solo_session_checkpoints", table =>
        {
            table.HasCheckConstraint("ck_solo_revision", "revision >= 1");
            table.HasCheckConstraint("ck_solo_credential_hash", "credential_hash ~ '^[A-F0-9]{64}$'");
        });
        row.HasKey(session => session.CredentialHash);
        row.Property(session => session.CredentialHash).HasColumnName("credential_hash").HasMaxLength(64);
        row.Property(session => session.CheckpointJson).HasColumnName("checkpoint_json").HasColumnType("jsonb");
        row.Property(session => session.Revision).HasColumnName("revision").IsConcurrencyToken();
        row.Property(session => session.UpdatedAtUtc).HasColumnName("updated_at_utc");
    }
}
