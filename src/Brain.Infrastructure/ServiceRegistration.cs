using Brain.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pgvector.EntityFrameworkCore;

namespace Brain.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddBrain(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<BrainDbContext>(options =>
        {
            var connection = configuration.GetConnectionString("Brain")
                ?? throw new InvalidOperationException("ConnectionStrings:Brain must be configured.");
            options.UseNpgsql(connection, o => o.UseVector());
        });
        services.AddSingleton(TimeProvider.System);
        services.AddBrainActivity(configuration);
        services.AddSingleton<IEmbeddingProvider, DevelopmentEmbeddingProvider>();
        services.AddScoped<IMemoryStore, MemoryStore>();
        services.AddScoped<IAgentDirectory, AgentDirectory>();
        services.AddScoped<IDeviceStore, DeviceStore>();
        services.AddScoped<DeviceService>();
        services.AddScoped<IRuntimeStore, RuntimeStore>();
        services.AddScoped<RuntimeService>();
        services.AddScoped<DependencyService>();
        services.AddScoped<IDeviceSkillStore, DeviceSkillStore>();
        services.AddScoped<DeviceSkillService>();
        services.AddSingleton(_ => configuration.GetSection("Provisioning").Get<ProvisioningOptions>() ?? new ProvisioningOptions());
        services.AddScoped<IProvisioningStore, ProvisioningStore>();
        services.AddScoped<ProvisioningService>();
        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<MemoryService>();
        services.AddSingleton(_ => configuration.GetSection("Knowledge").Get<KnowledgeOptions>() ?? new KnowledgeOptions());
        services.AddSingleton<IKnowledgeVault, FileSystemKnowledgeVault>();
        services.AddSingleton<IMarkdownChunker, MarkdownChunker>();
        services.AddScoped<IKnowledgeIndex, KnowledgeIndex>();
        services.AddScoped<KnowledgeService>();
        services.AddSingleton<IGraphReferenceExtractor, GraphReferenceExtractor>();
        services.AddScoped<IGraphSource, GraphSource>();
        services.AddScoped<GraphService>();
        services.AddScoped<IKnowledgeProposalStore, KnowledgeProposalStore>();
        services.AddScoped<KnowledgeProposalService>();
        services.AddSingleton(_ => configuration.GetSection("Registry").Get<RegistryOptions>() ?? new RegistryOptions());
        services.AddSingleton<IRegistrySource, FileSystemRegistrySource>();
        services.AddScoped<IRegistryIndex, RegistryIndex>();
        services.AddScoped<RegistryService>();
        services.AddScoped<SearchService>();
        services.AddSingleton(_ => configuration.GetSection("Metadata").Get<MetadataOptions>() ?? new MetadataOptions());
        services.AddSingleton(_ => configuration.GetSection("Metadata:Semantic").Get<MetadataProviderOptions>() ?? new MetadataProviderOptions());
        services.AddSingleton<IMetadataDocumentProcessor, MetadataDocumentProcessor>();
        services.AddSingleton<IKnowledgeProvenance, GitKnowledgeProvenance>();
        services.AddHttpClient<IMetadataEnrichmentProvider, CompatibleMetadataProvider>(client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<IMetadataScanStore, MetadataScanStore>();
        services.AddScoped<MetadataService>();
        return services;
    }

    public static IServiceCollection AddKnowledgeWriter(this IServiceCollection services)
    {
        services.AddScoped<ICanonicalKnowledgeWriter, GitKnowledgeWriter>();
        services.AddScoped<KnowledgeWriterService>();
        return services;
    }
}

public sealed class BrainDesignTimeFactory : IDesignTimeDbContextFactory<BrainDbContext>
{
    public BrainDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<BrainDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__Brain") ??
            "Host=localhost;Database=brain;Username=brain", o => o.UseVector()).Options);
}
