using Microsoft.EntityFrameworkCore;

namespace EnglishRoulette.Infrastructure.Persistence;

public sealed class StoredSessionRow
{
    public string CredentialHash { get; set; } = "";
    public string CheckpointJson { get; set; } = "";
    public long Revision { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class AdventureDbContext(DbContextOptions<AdventureDbContext> options) : DbContext(options)
{
    public DbSet<StoredSessionRow> SoloSessions => Set<StoredSessionRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
