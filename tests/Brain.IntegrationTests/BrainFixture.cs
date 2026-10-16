using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Brain.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Npgsql;
using Brain.Tests;

namespace Brain.IntegrationTests;

public sealed class BrainFixture : IAsyncLifetime
{
    public string VaultPath { get; } = Path.Combine(Path.GetTempPath(), "brain-test-vault-" + Guid.NewGuid().ToString("N"));
    public string RegistryPath { get; } = Path.Combine(Path.GetTempPath(), "brain-test-registry-" + Guid.NewGuid().ToString("N"));
    private PostgreSqlContainer? postgres;
    private readonly string? externalConnection = Environment.GetEnvironmentVariable("BRAIN_TEST_POSTGRES");
    private readonly string scratchDatabase = "brain_test_" + Guid.NewGuid().ToString("N");
    private bool scratchCreated;
    public BrainFactory Factory { get; private set; } = null!;
    public async Task InitializeAsync()
    {
        string connection;
        if (externalConnection is null)
        {
            postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();
            await postgres.StartAsync();
            connection = postgres.GetConnectionString();
        }
        else
        {
            await using var admin = new NpgsqlConnection(externalConnection);
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{scratchDatabase}\"", admin);
            await create.ExecuteNonQueryAsync();
            scratchCreated = true;
            connection = new NpgsqlConnectionStringBuilder(externalConnection) { Database = scratchDatabase }.ConnectionString;
        }
        Directory.CreateDirectory(VaultPath);
        Directory.CreateDirectory(Path.Combine(RegistryPath, "skills"));
        Directory.CreateDirectory(Path.Combine(RegistryPath, "tools"));
        Factory = new BrainFactory(connection, VaultPath, RegistryPath);
        using var client = Factory.CreateClient();
        (await client.GetAsync("/health/live")).EnsureSuccessStatusCode();
    }
    public async Task ResetAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrainDbContext>();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE agents, roles, permissions, memories, memory_embeddings, knowledge_documents, knowledge_chunks, knowledge_write_proposals, registry_entries, metadata_scan_states, audit_events CASCADE");
        await DatabaseInitializer.InitializeAsync(db, true);
        Directory.Delete(RegistryPath, true);
        Directory.CreateDirectory(Path.Combine(RegistryPath, "skills"));
        Directory.CreateDirectory(Path.Combine(RegistryPath, "tools"));
        TestGit.ClearReadOnly(VaultPath);
        foreach (var entry in Directory.EnumerateFileSystemEntries(VaultPath))
        {
            if (Directory.Exists(entry)) Directory.Delete(entry, true);
            else File.Delete(entry);
        }
    }
    public async Task DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
        if (Directory.Exists(VaultPath)) { TestGit.ClearReadOnly(VaultPath); Directory.Delete(VaultPath, true); }
        if (Directory.Exists(RegistryPath)) Directory.Delete(RegistryPath, true);
        if (postgres is not null) await postgres.DisposeAsync();
        if (scratchCreated)
        {
            await using var admin = new NpgsqlConnection(externalConnection);
            await admin.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{scratchDatabase}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}

public sealed class BrainFactory(string connection, string vaultPath, string? registryPath = null) : WebApplicationFactory<Program>
{
    public const string WillemToken = "integration-willem-credential-000000000000";
    public const string CodexToken = "integration-codex-credential-0000000000000";
    public const string OtherToken = "integration-other-credential-0000000000000";
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services => services.AddKnowledgeWriter());
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Brain"] = connection,
            ["Knowledge:VaultPath"] = vaultPath,
            ["Registry:RootPath"] = registryPath,
            ["Database:MigrateOnStartup"] = "true",
            ["Database:SeedDevelopment"] = "true",
            ["Authentication:AgentTokenHashes:willem-de-jong"] = Hash(WillemToken),
            ["Authentication:AgentTokenHashes:codex"] = Hash(CodexToken),
            ["Authentication:AgentTokenHashes:other"] = Hash(OtherToken),
            ["Logging:LogLevel:Default"] = "Warning"
        }));
    }
    public HttpClient Agent(string id)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Agent-Id", id);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", id switch
        { "willem-de-jong" => WillemToken, "codex" => CodexToken, _ => OtherToken });
        return client;
    }
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
