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
}

public sealed class AgentAuthenticationOptions : AuthenticationSchemeOptions
{
    // SHA-256 digests only; raw client tokens are never persisted by the Brain.
    public Dictionary<string, string> AgentTokenHashes { get; set; } = new(StringComparer.Ordinal);
}

public sealed class AgentAuthenticationHandler(
    IOptionsMonitor<AgentAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    IAgentDirectory directory, RequestAgent requestAgent)
    : AuthenticationHandler<AgentAuthenticationOptions>(options, logger, encoder)
{
    public const string SchemeName = "AgentToken";
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var ids = Request.Headers["X-Agent-Id"];
        var authorization = Request.Headers.Authorization;
        if (ids.Count == 0 && authorization.Count == 0) return AuthenticateResult.NoResult();
        if (ids.Count != 1 || authorization.Count != 1 || ids[0] is not { Length: > 0 and <= 100 } id ||
            authorization[0] is not { } header || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.Fail("Invalid agent credentials.");
        var token = header[7..];
        if (token.Length is < 32 or > 1024 || !Options.AgentTokenHashes.TryGetValue(id, out var expected))
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
        { Status = 401, Title = "Agent authentication required.", Detail = "Supply X-Agent-Id and a valid bearer token." });
    }
}
