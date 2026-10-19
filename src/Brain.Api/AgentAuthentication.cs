using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Brain.Application;
using Brain.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Brain.Api;

public sealed class RequestAgent : ICurrentAgent
{
    public AgentContext Context { get; set; } = null!;
    public Guid? DeviceId { get; set; }
}

public sealed class AgentAuthenticationOptions : AuthenticationSchemeOptions
{
    // SHA-256 digests only; raw client tokens are never persisted by the Brain.
    public Dictionary<string, string> AgentTokenHashes { get; set; } = new(StringComparer.Ordinal);
    public string? UiAdminAgentId { get; set; }
    public string? UiAdminTokenHash { get; set; }
}

public sealed class AgentAuthenticationHandler(
    IOptionsMonitor<AgentAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    IAgentDirectory directory, IDeviceStore devices, RequestAgent requestAgent)
    : AuthenticationHandler<AgentAuthenticationOptions>(options, logger, encoder)
{
    public const string SchemeName = "AgentToken";
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var ids = Request.Headers["X-Agent-Id"];
        var authorization = Request.Headers.Authorization;
        if (ids.Count == 0 && authorization.Count == 0) return AuthenticateResult.NoResult();
        if (authorization.Count != 1 || authorization[0] is not { } header ||
            !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.Fail("Invalid agent credentials.");
        string id;
        string expected;
        if (ids.Count == 0)
        {
            var candidate = header[7..];
            if (candidate.StartsWith("bd_", StringComparison.Ordinal))
            {
                var device = await devices.AuthenticateAsync(candidate, Context.RequestAborted);
                if (device is null) return AuthenticateResult.Fail("Invalid device credentials.");
                var boundAgent = await directory.ResolveAsync(device.AgentId, Context.RequestAborted);
                if (boundAgent is null) return AuthenticateResult.Fail("Invalid device credentials.");
                var grants = boundAgent.Permissions.Intersect(device.Scopes, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
                requestAgent.Context = new AgentContext(boundAgent.Id, boundAgent.TeamId, grants);
                requestAgent.DeviceId = device.DeviceId;
                var devicePrincipal = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, boundAgent.Id), new Claim("brain_device_id", device.DeviceId.ToString())], SchemeName));
                return AuthenticateResult.Success(new AuthenticationTicket(devicePrincipal, SchemeName));
            }
            if (Request.Path.StartsWithSegments("/mcp") ||
                Options.UiAdminAgentId is not { Length: > 0 } adminId ||
                Options.UiAdminTokenHash is not { Length: 64 } adminHash)
                return AuthenticateResult.Fail("Invalid administrator credentials.");
            id = adminId;
            expected = adminHash;
        }
        else
        {
            if (ids.Count != 1 || ids[0] is not { Length: > 0 and <= 100 } agentId ||
                !Options.AgentTokenHashes.TryGetValue(agentId, out var agentHash))
                return AuthenticateResult.Fail("Invalid agent credentials.");
            id = agentId;
            expected = agentHash;
        }
        var token = header[7..];
        if (token.Length is < 32 or > 1024)
            return AuthenticateResult.Fail("Invalid agent credentials.");
        if (expected.Length != 64 || !expected.All(Uri.IsHexDigit))
            return AuthenticateResult.Fail("Invalid agent credentials.");
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        if (!CryptographicOperations.FixedTimeEquals(digest, Convert.FromHexString(expected)))
            return AuthenticateResult.Fail("Invalid agent credentials.");
        var agent = await directory.ResolveAsync(id, Context.RequestAborted);
        if (agent is null) return AuthenticateResult.Fail("Invalid agent credentials.");
        requestAgent.Context = agent;
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, agent.Id)], SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        Response.Headers.WWWAuthenticate = "Bearer";
        return Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
        { Status = 401, Title = "Authentication required.",
            Detail = "Supply an administrator bearer token, or X-Agent-Id and its bound agent bearer token. MCP requires agent credentials." });
    }
}
