using Brain.Domain;
using Brain.Application;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace Brain.Infrastructure;

public sealed class MemoryEmbedding
{
    public Guid MemoryId { get; set; }
    public AgentMemory Memory { get; set; } = null!;
    public Vector Embedding { get; set; } = null!;
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public int Dimensions { get; set; }
}

public sealed class BrainDbContext(DbContextOptions<BrainDbContext> options) : DbContext(options)
{
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<AgentMemory> Memories => Set<AgentMemory>();
    public DbSet<MemoryEmbedding> MemoryEmbeddings => Set<MemoryEmbedding>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();
    public DbSet<KnowledgeWriteProposal> KnowledgeWriteProposals => Set<KnowledgeWriteProposal>();
    public DbSet<RegistryIndexEntry> RegistryEntries => Set<RegistryIndexEntry>();
    public DbSet<MetadataScanState> MetadataScanStates => Set<MetadataScanState>();
    public DbSet<ActivityTrace> ActivityTraces => Set<ActivityTrace>();
    public DbSet<RetainedActivityEvent> ActivityEvents => Set<RetainedActivityEvent>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasPostgresExtension("vector");
        b.Entity<Agent>(e =>
        {
            e.ToTable("agents");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(100);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.TeamId).HasMaxLength(100);
            e.Property(x => x.MetadataJson).HasColumnType("jsonb");
            e.HasMany(x => x.Roles).WithMany().UsingEntity<Dictionary<string, object>>("agent_roles",
                r => r.HasOne<Role>().WithMany().HasForeignKey("role_id"),
                l => l.HasOne<Agent>().WithMany().HasForeignKey("agent_id"));
            e.HasMany(x => x.Permissions).WithMany().UsingEntity<Dictionary<string, object>>("agent_permissions",
                r => r.HasOne<Permission>().WithMany().HasForeignKey("permission_id"),
                l => l.HasOne<Agent>().WithMany().HasForeignKey("agent_id"));
        });
        b.Entity<Role>(e =>
        {
            e.ToTable("roles"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(100);
            e.HasMany(x => x.Permissions).WithMany().UsingEntity<Dictionary<string, object>>("role_permissions",
                r => r.HasOne<Permission>().WithMany().HasForeignKey("permission_id"),
                l => l.HasOne<Role>().WithMany().HasForeignKey("role_id"));
        });
        b.Entity<Permission>(e => { e.ToTable("permissions"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasMaxLength(100); });
        b.Entity<AgentMemory>(e =>
        {
            e.ToTable("memories", t =>
            {
                t.HasCheckConstraint("ck_memory_scores", "importance BETWEEN 0 AND 1 AND confidence BETWEEN 0 AND 1");
                t.HasCheckConstraint("ck_memory_visibility", "visibility IN ('private','team','organization') AND (visibility <> 'team' OR team_id IS NOT NULL)");
                t.HasCheckConstraint("ck_memory_type", "memory_type IN ('observation','fact','decision','task_context','incident','preference','discovery')");
                t.HasCheckConstraint("ck_promotion_status", "promotion_status IN ('none','candidate','approved','rejected','promoted')");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Subject).HasMaxLength(300);
            e.Property(x => x.Content).HasMaxLength(32000);
            e.Property(x => x.Source).HasMaxLength(2000);
            e.Property(x => x.TeamId).HasMaxLength(100);
            e.Property(x => x.MemoryType).HasConversion(v => EnumName(v), v => ParseEnum<MemoryType>(v));
            e.Property(x => x.Visibility).HasConversion(v => EnumName(v), v => ParseEnum<MemoryVisibility>(v));
            e.Property(x => x.PromotionStatus).HasConversion(v => EnumName(v), v => ParseEnum<PromotionStatus>(v));
            e.Property(x => x.MetadataJson).HasColumnType("jsonb");
            e.HasOne<Agent>().WithMany().HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.AgentId); e.HasIndex(x => x.ExpiresAt);
            e.HasIndex(x => new { x.Visibility, x.TeamId });
        });
        b.Entity<MemoryEmbedding>(e =>
        {
            e.ToTable("memory_embeddings", t => t.HasCheckConstraint("ck_embedding_dimensions", "dimensions = 384"));
            e.HasKey(x => x.MemoryId);
            e.HasOne(x => x.Memory).WithOne().HasForeignKey<MemoryEmbedding>(x => x.MemoryId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Embedding).HasColumnType("vector(384)");
            e.Property(x => x.Provider).HasMaxLength(200);
            e.Property(x => x.Model).HasMaxLength(200);
            e.HasIndex(x => new { x.Provider, x.Model });
        });
        b.Entity<AuditEvent>(e =>
        {
            e.ToTable("audit_events"); e.HasKey(x => x.Id);
            e.Property(x => x.MetadataJson).HasColumnType("jsonb");
            e.HasIndex(x => new { x.AgentId, x.Timestamp });
        });
        b.Entity<KnowledgeDocument>(e =>
        {
            e.ToTable("knowledge_documents"); e.HasKey(d => d.Id);
            e.Property(d => d.Path).HasMaxLength(1000);
            e.Property(d => d.Title).HasMaxLength(300);
            e.Property(d => d.ContentHash).HasMaxLength(64);
            e.Property(d => d.RevisionToken).HasMaxLength(67);
            e.Property(d => d.EmbeddingProvider).HasMaxLength(200);
            e.Property(d => d.EmbeddingModel).HasMaxLength(200);
            e.Property(d => d.ChunkerVersion).HasMaxLength(100);
            e.Property(d => d.GraphReferences).HasDefaultValueSql("ARRAY[]::text[]");
            e.HasIndex(d => d.GraphReferences).HasMethod("gin");
            e.HasIndex(d => d.Path).IsUnique();
        });
        b.Entity<KnowledgeChunk>(e =>
        {
            e.ToTable("knowledge_chunks", t => t.HasCheckConstraint("ck_knowledge_chunk_lines", "start_line > 0 AND end_line >= start_line"));
            e.HasKey(c => c.Id);
            e.HasOne(c => c.Document).WithMany().HasForeignKey(c => c.DocumentId).OnDelete(DeleteBehavior.Cascade);
            e.Property(c => c.Content).HasMaxLength(MarkdownChunker.MaximumChunkCharacters);
            e.Property(c => c.Embedding).HasColumnType("vector(384)");
            e.HasIndex(c => new { c.DocumentId, c.Ordinal }).IsUnique();
        });
        b.Entity<KnowledgeWriteProposal>(e =>
        {
            e.ToTable("knowledge_write_proposals", t =>
            {
                t.HasCheckConstraint("ck_proposal_status", "status IN ('pending','validating','approved','rejected','conflict','applied')");
                t.HasCheckConstraint("ck_proposal_operation", "operation IN ('append','replace_section','create_document','metadata_enrichment')");
                t.HasCheckConstraint("ck_proposal_version", "version >= 0");
                t.HasCheckConstraint("ck_proposal_confidence", "enrichment_confidence IS NULL OR enrichment_confidence BETWEEN 0 AND 1");
            });
            e.HasKey(p => p.Id);
            e.Property(p => p.Path).HasMaxLength(1000);
            e.Property(p => p.Content).HasMaxLength(32000);
            e.Property(p => p.Reason).HasMaxLength(2000);
            e.Property(p => p.Section).HasMaxLength(4000);
            e.Property(p => p.ReviewReason).HasMaxLength(2000);
            e.Property(p => p.RevisionToken).HasMaxLength(67);
            e.Property(p => p.LastError).HasMaxLength(2000);
            e.Property(p => p.GitBranch).HasMaxLength(1000);
            e.Property(p => p.ParentCommit).HasMaxLength(64);
            e.Property(p => p.GitCommit).HasMaxLength(64);
            e.Property(p => p.GitBlob).HasMaxLength(64);
            e.Property(p => p.BaseContentHash).HasMaxLength(64);
            e.Property(p => p.ResultContentHash).HasMaxLength(64);
            e.Property(p => p.EnrichmentProvider).HasMaxLength(200);
            e.Property(p => p.EnrichmentModel).HasMaxLength(200);
            e.Property(p => p.GeneratedBy).HasMaxLength(100);
            e.Property(p => p.Version).IsConcurrencyToken();
            e.Property(p => p.Status).HasConversion(v => EnumName(v), v => ParseEnum<KnowledgeProposalStatus>(v));
            e.Property(p => p.Operation).HasConversion(v => EnumName(v), v => ParseEnum<KnowledgeOperation>(v));
            e.HasOne<Agent>().WithMany().HasForeignKey(p => p.AgentId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(p => new { p.Status, p.CreatedAt });
            e.HasIndex(p => new { p.AgentId, p.CreatedAt });
        });
        b.Entity<RegistryIndexEntry>(e =>
        {
            e.ToTable("registry_entries", t =>
            {
                t.HasCheckConstraint("ck_registry_kind", "kind IN ('skill','tool')");
                t.HasCheckConstraint("ck_registry_dimensions", "embedding_dimensions = 384");
                t.HasCheckConstraint("ck_registry_provider", "(kind = 'skill' AND provider IS NULL) OR (kind = 'tool' AND provider IS NOT NULL AND provider IN ('mcp','http'))");
            });
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(106);
            e.Property(x => x.Fingerprint).HasMaxLength(171);
            e.Property(x => x.Id).HasMaxLength(100).IsRequired();
            e.Property(x => x.Kind).HasMaxLength(5);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Source).HasMaxLength(200);
            e.Property(x => x.ContentHash).HasMaxLength(64);
            e.Property(x => x.Provider).HasMaxLength(20);
            e.Property(x => x.EmbeddingProvider).HasMaxLength(200);
            e.Property(x => x.EmbeddingModel).HasMaxLength(200);
            e.Property(x => x.Embedding).HasColumnType("vector(384)");
            e.HasIndex(x => x.Fingerprint).IsUnique();
        });
        b.Entity<MetadataScanState>(e =>
        {
            e.ToTable("metadata_scan_states"); e.HasKey(x => x.Path);
            e.Property(x => x.Path).HasMaxLength(1000);
            e.Property(x => x.ContentHash).HasMaxLength(64);
            e.Property(x => x.RevisionToken).HasMaxLength(67);
            e.Property(x => x.PolicyHash).HasMaxLength(64);
            e.Property(x => x.Provider).HasMaxLength(200);
            e.Property(x => x.Model).HasMaxLength(200);
            e.Property(x => x.LastError).HasMaxLength(2000);
            e.Property(x => x.IssuesJson).HasColumnType("jsonb");
            e.HasOne<KnowledgeWriteProposal>().WithMany().HasForeignKey(x => x.ProposalId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<ActivityTrace>(e =>
        {
            e.ToTable("activity_traces");
            e.HasKey(x => new { x.TraceId, x.AgentId });
            e.Property(x => x.TraceId).HasMaxLength(32);
            e.Property(x => x.AgentId).HasMaxLength(200);
            e.Property(x => x.RootSpanId).HasMaxLength(16);
            e.Property(x => x.RootAction).HasMaxLength(200);
            e.Property(x => x.Status).HasConversion(v => EnumName(v), v => ParseEnum<ActivityStatus>(v));
            e.HasIndex(x => new { x.AgentId, x.StartedAt });
            e.HasIndex(x => x.StartedAt);
            e.HasIndex(x => x.CompletedAt);
        });
        b.Entity<RetainedActivityEvent>(e =>
        {
            e.ToTable("activity_events"); e.HasKey(x => x.Id);
            e.HasOne<ActivityTrace>().WithMany().HasForeignKey(x => new { x.TraceId, x.AgentId }).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.TraceId).HasMaxLength(32);
            e.Property(x => x.AgentId).HasMaxLength(200);
            e.Property(x => x.SpanId).HasMaxLength(16);
            e.Property(x => x.ParentSpanId).HasMaxLength(16);
            e.Property(x => x.SourceEntityId).HasMaxLength(200);
            e.Property(x => x.TargetEntityId).HasMaxLength(200);
            e.Property(x => x.EventType).HasMaxLength(200);
            e.Property(x => x.Action).HasMaxLength(200);
            e.Property(x => x.ErrorType).HasMaxLength(200);
            e.Property(x => x.ErrorMessage).HasMaxLength(200);
            e.Property(x => x.MetadataJson).HasColumnType("jsonb");
            e.Property(x => x.Status).HasConversion(v => EnumName(v), v => ParseEnum<ActivityStatus>(v));
            e.HasIndex(x => new { x.TraceId, x.AgentId, x.Timestamp });
            e.HasIndex(x => x.SourceEntityId);
            e.HasIndex(x => x.TargetEntityId);
        });
        // Keep the PostgreSQL schema readable without an extra conventions package.
        foreach (var entity in b.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
                property.SetColumnName(System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name));
    }

    private static string EnumName<T>(T value) where T : struct, Enum =>
        System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
    private static T ParseEnum<T>(string value) where T : struct, Enum => Enum.Parse<T>(value.Replace("_", ""), true);
}
