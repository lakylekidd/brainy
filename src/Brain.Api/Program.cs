using System.Text.Json;
using System.Text.Json.Serialization;
using Brain.Api;
using Brain.Application;
using Brain.Domain;
using Brain.Infrastructure;
using Brain.Mcp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddBrain(builder.Configuration);
builder.Services.AddScoped<RequestAgent>();
builder.Services.AddScoped<ICurrentAgent>(services => services.GetRequiredService<RequestAgent>());
builder.Services.AddBrainMcp();
builder.Services.AddAuthentication(AgentAuthenticationHandler.SchemeName)
    .AddScheme<AgentAuthenticationOptions, AgentAuthenticationHandler>(AgentAuthenticationHandler.SchemeName,
        options => builder.Configuration.GetSection("Authentication").Bind(options));
builder.Services.AddOptions<AgentAuthenticationOptions>(AgentAuthenticationHandler.SchemeName)
    .Validate(options => options.AgentTokenHashes.Count > 0 &&
        options.AgentTokenHashes.Values.All(hash => hash is { Length: 64 } && hash.All(Uri.IsHexDigit)) &&
        options.AgentTokenHashes.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() == options.AgentTokenHashes.Count,
        "Configure a distinct SHA-256 credential digest for every authenticated agent.")
    .ValidateOnStart();
builder.Services.AddAuthorization(options => options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser().Build());
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "Homelab Brain";
    document.Info.Description = "Identity, permissions, memories, canonical knowledge and skill/tool discovery. Every data request needs both security schemes.";
    document.Components ??= new OpenApiComponents();
    document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
    {
        ["agentId"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Header, Name = "X-Agent-Id" },
        ["agentToken"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer" }
    };
    document.Security = [new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("agentId", document)] = [],
        [new OpenApiSecuritySchemeReference("agentToken", document)] = []
    }];
    if (document.Paths.TryGetValue("/health/live", out var live) && live.Operations is not null)
        foreach (var operation in live.Operations.Values) operation.Security = [];
    return Task.CompletedTask;
}));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false));
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
});

var app = builder.Build();
if (builder.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var seed = builder.Configuration.GetValue<bool>("Database:SeedDevelopment");
    if (seed && !app.Environment.IsDevelopment()) throw new InvalidOperationException("Development seed is only allowed in Development.");
    await DatabaseInitializer.InitializeAsync(scope.ServiceProvider.GetRequiredService<BrainDbContext>(), seed);
}

app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (BadHttpRequestException e)
    {
        context.Response.StatusCode = e.StatusCode;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        { Status = e.StatusCode, Title = "Invalid request." }, context.RequestAborted);
    }
    catch (BrainException e)
    {
        if (e.StatusCode == 403 && context.User.Identity?.IsAuthenticated == true)
        {
            var actor = context.RequestServices.GetRequiredService<RequestAgent>().Context;
            await context.RequestServices.GetRequiredService<IAuditLog>().WriteAsync(new AuditEvent
            {
                AgentId = actor.Id, Action = "brain.authorization.denied", Result = "denied",
                Resource = context.GetEndpoint()?.DisplayName ?? "request", Timestamp = DateTimeOffset.UtcNow
            }, context.RequestAborted);
        }
        context.Response.StatusCode = e.StatusCode;
        await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = e.StatusCode, Title = e.Message }, context.RequestAborted);
    }
});
app.UseAuthentication();
app.UseAuthorization();

// Process-only liveness for infrastructure; all data and schema endpoints require identity.
app.MapGet("/health/live", () => Results.Ok(new { status = "alive" })).AllowAnonymous();
app.MapGet("/health/ready", async (BrainDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503));
app.MapOpenApi().RequireAuthorization();
app.MapBrainMcp();
app.MapPost("/api/search", (BrainSearchRequest request, RequestAgent agent, SearchService search, CancellationToken ct) =>
    search.SearchAsync(agent.Context, request, ct));

app.MapGet("/api/agents/me", (RequestAgent agent) => Results.Ok(new
{ agent.Context.Id, agent.Context.TeamId, permissions = agent.Context.Permissions.Order() }));
app.MapGet("/api/agents", (RequestAgent agent, IAgentDirectory directory, CancellationToken ct) => directory.ListAsync(agent.Context, ct));
app.MapPost("/api/agents", async (CreateAgentRequest request, RequestAgent agent, IAgentDirectory directory, CancellationToken ct) =>
{
    var result = await directory.CreateAsync(agent.Context, request, ct);
    return Results.Created($"/api/agents/{result.Id}", result);
});
app.MapPut("/api/agents/{id}/access", (string id, UpdateAccessRequest request, RequestAgent agent, IAgentDirectory directory, CancellationToken ct) =>
    directory.UpdateAccessAsync(agent.Context, id, request, ct));
app.MapPost("/api/memories", async (RememberRequest request, RequestAgent agent, MemoryService memories, CancellationToken ct) =>
{
    var memory = await memories.RememberAsync(agent.Context, request, ct);
    return Results.Created($"/api/memories/{memory.Id}", memory);
});
app.MapPost("/api/memories/search", (MemorySearchRequest request, RequestAgent agent, MemoryService memories, CancellationToken ct) =>
    memories.SearchAsync(agent.Context, request, ct));
app.MapGet("/api/memories/{id:guid}", (Guid id, RequestAgent agent, MemoryService memories, CancellationToken ct) =>
    memories.GetAsync(agent.Context, id, ct));
app.MapDelete("/api/memories/{id:guid}", async (Guid id, RequestAgent agent, MemoryService memories, CancellationToken ct) =>
{
    await memories.DeleteAsync(agent.Context, id, ct);
    return Results.NoContent();
});
app.MapGet("/api/audit", (int? limit, RequestAgent agent, IAuditLog audit, CancellationToken ct) =>
    audit.ListAsync(agent.Context, limit ?? 100, ct));

app.MapPost("/api/knowledge/search", (KnowledgeSearchRequest request, RequestAgent agent, KnowledgeService knowledge, CancellationToken ct) =>
    knowledge.SearchAsync(agent.Context, request, ct));
app.MapGet("/api/knowledge/documents/{id:guid}", (Guid id, RequestAgent agent, KnowledgeService knowledge, CancellationToken ct) =>
    knowledge.ReadAsync(agent.Context, id, ct));
app.MapGet("/api/knowledge/document", (string path, RequestAgent agent, KnowledgeService knowledge, CancellationToken ct) =>
    knowledge.ReadPathAsync(agent.Context, path, ct));
app.MapPost("/api/knowledge/scan", (RequestAgent agent, KnowledgeService knowledge, CancellationToken ct) =>
    knowledge.ScanAsync(agent.Context, ct));
app.MapGet("/api/knowledge/metadata/issues", (int? limit, RequestAgent agent, MetadataService metadata, CancellationToken ct) =>
    metadata.IssuesAsync(agent.Context, limit ?? 100, ct));
app.MapPost("/api/knowledge/metadata/scan", (RequestAgent agent, MetadataService metadata, CancellationToken ct) =>
    metadata.ScanAsync(agent.Context, ct));

app.MapPost("/api/knowledge/proposals", async (ProposeKnowledgeChangeRequest request, RequestAgent agent, KnowledgeProposalService proposals, CancellationToken ct) =>
{
    var proposal = await proposals.ProposeAsync(agent.Context, request, ct);
    return Results.Created($"/api/knowledge/proposals/{proposal.Id}", proposal);
});
app.MapGet("/api/knowledge/proposals", (string? status, int? limit, string? type, RequestAgent agent, KnowledgeProposalService proposals, CancellationToken ct) =>
    proposals.ListAsync(agent.Context, ParseProposalStatus(status), limit ?? 50, ct, type));
app.MapGet("/api/knowledge/proposals/{id:guid}", (Guid id, RequestAgent agent, KnowledgeProposalService proposals, CancellationToken ct) =>
    proposals.GetAsync(agent.Context, id, ct));
app.MapGet("/api/knowledge/proposals/{id:guid}/preview", (Guid id, RequestAgent agent, KnowledgeProposalService proposals, CancellationToken ct) =>
    proposals.PreviewAsync(agent.Context, id, ct));
app.MapPost("/api/knowledge/proposals/{id:guid}/approve", (Guid id, ReviewKnowledgeProposalRequest request, RequestAgent agent, KnowledgeProposalService proposals, CancellationToken ct) =>
    proposals.ReviewAsync(agent.Context, id, request, true, ct));
app.MapPost("/api/knowledge/proposals/{id:guid}/reject", (Guid id, ReviewKnowledgeProposalRequest request, RequestAgent agent, KnowledgeProposalService proposals, CancellationToken ct) =>
    proposals.ReviewAsync(agent.Context, id, request, false, ct));

app.MapGet("/api/skills", (RequestAgent agent, RegistryService registry, CancellationToken ct) =>
    registry.ListAsync(agent.Context, RegistryKind.Skill, ct));
app.MapGet("/api/skills/{id}", (string id, RequestAgent agent, RegistryService registry, CancellationToken ct) =>
    registry.GetSkillAsync(agent.Context, id, ct));
app.MapPost("/api/skills/search", (RegistrySearchRequest request, RequestAgent agent, RegistryService registry, CancellationToken ct) =>
    registry.SearchAsync(agent.Context, RegistryKind.Skill, request, ct));
app.MapGet("/api/tools", (RequestAgent agent, RegistryService registry, CancellationToken ct) =>
    registry.ListAsync(agent.Context, RegistryKind.Tool, ct));
app.MapGet("/api/tools/{id}", (string id, RequestAgent agent, RegistryService registry, CancellationToken ct) =>
    registry.GetToolAsync(agent.Context, id, ct));
app.MapPost("/api/tools/search", (RegistrySearchRequest request, RequestAgent agent, RegistryService registry, CancellationToken ct) =>
    registry.SearchAsync(agent.Context, RegistryKind.Tool, request, ct));
app.MapPost("/api/capabilities/search", (RegistrySearchRequest request, RequestAgent agent, RegistryService registry, CancellationToken ct) =>
    registry.FindCapabilitiesAsync(agent.Context, request, ct));
app.MapPost("/api/registry/scan", (RequestAgent agent, RegistryService registry, CancellationToken ct) =>
    registry.ScanAsync(agent.Context, ct));

static KnowledgeProposalStatus? ParseProposalStatus(string? value)
{
    if (value is null) return null;
    if (value.Length == 0 || value.Any(c => !char.IsAsciiLetter(c) && c != '_') ||
        !Enum.TryParse<KnowledgeProposalStatus>(value.Replace("_", ""), true, out var status) || !Enum.IsDefined(status))
        throw new BrainException(400, "Unknown proposal status.");
    return status;
}

app.Run();
public partial class Program;
