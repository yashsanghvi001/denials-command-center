using Microsoft.EntityFrameworkCore;

namespace DenialsCommandCenter.Api.Data;

public sealed class DenialsDbContext(DbContextOptions<DenialsDbContext> options) : DbContext(options)
{
    public DbSet<ClaimRow> Claims => Set<ClaimRow>();
    public DbSet<RemitEventRow> RemitEvents => Set<RemitEventRow>();
    public DbSet<ClaimStateRow> ClaimStates => Set<ClaimStateRow>();
    public DbSet<WorklogEntryRow> WorklogEntries => Set<WorklogEntryRow>();
    public DbSet<IngestionIssueRow> IngestionIssues => Set<IngestionIssueRow>();
    public DbSet<SourceFileRow> SourceFiles => Set<SourceFileRow>();
    public DbSet<IngestionRun> IngestionRuns => Set<IngestionRun>();
    public DbSet<DenialAnalysisRow> DenialAnalyses => Set<DenialAnalysisRow>();
    public DbSet<AppealDraftRow> AppealDrafts => Set<AppealDraftRow>();
    public DbSet<UserRow> Users => Set<UserRow>();
    public DbSet<WorkItemRow> WorkItems => Set<WorkItemRow>();
    public DbSet<WorkNoteRow> WorkNotes => Set<WorkNoteRow>();
    public DbSet<AuditEntryRow> AuditEntries => Set<AuditEntryRow>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<decimal>().HavePrecision(12, 2);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClaimRow>(entity =>
        {
            entity.HasKey(x => x.ClaimId);
            entity.Property(x => x.LinesJson).HasColumnType("jsonb");
        });
        modelBuilder.Entity<RemitEventRow>(entity =>
        {
            entity.HasKey(x => x.EventKey);
            entity.HasIndex(x => x.ClaimId);
            entity.Property(x => x.PaymentJson).HasColumnType("jsonb");
        });
        modelBuilder.Entity<ClaimStateRow>(entity =>
        {
            entity.HasKey(x => x.ClaimId);
            entity.Property(x => x.DenialLinesJson).HasColumnType("jsonb");
        });
        modelBuilder.Entity<WorklogEntryRow>(entity =>
        {
            entity.HasKey(x => x.RowNumber);
            entity.Property(x => x.RowNumber).ValueGeneratedNever();
            entity.HasIndex(x => x.ClaimId);
        });
        modelBuilder.Entity<IngestionIssueRow>(entity =>
        {
            entity.HasKey(x => x.Key);
            entity.HasIndex(x => x.ClaimId);
        });
        modelBuilder.Entity<SourceFileRow>(entity => entity.HasKey(x => x.FileName));
        modelBuilder.Entity<IngestionRun>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ReportJson).HasColumnType("jsonb");
        });
        modelBuilder.Entity<DenialAnalysisRow>(entity =>
        {
            entity.HasKey(x => x.ClaimId);
            entity.Property(x => x.CitationsJson).HasColumnType("jsonb");
        });
        modelBuilder.Entity<AppealDraftRow>(entity =>
        {
            entity.HasKey(x => x.FactsHash);
            entity.HasIndex(x => x.ClaimId);
            entity.Property(x => x.CitationsJson).HasColumnType("jsonb");
        });
        modelBuilder.Entity<UserRow>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Username).IsUnique();
            entity.HasIndex(x => x.PasswordResetTokenHash).IsUnique();
        });
        modelBuilder.Entity<WorkItemRow>(entity =>
        {
            entity.HasKey(x => x.ClaimId);
            entity.HasIndex(x => x.AssignedTo);
            entity.Property(x => x.Version).IsRowVersion();
        });
        modelBuilder.Entity<WorkNoteRow>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ClaimId);
        });
        modelBuilder.Entity<AuditEntryRow>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.EntityId);
            entity.Property(x => x.BeforeJson).HasColumnType("jsonb");
            entity.Property(x => x.AfterJson).HasColumnType("jsonb");
        });
    }
}
