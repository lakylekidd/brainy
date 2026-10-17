using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Brain.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using ActivityEvent = Brain.Application.ActivityEvent;

namespace Brain.Infrastructure;

// Fail closed: arbitrary text, headers, bodies, URLs and exception messages never enter telemetry.
public sealed partial class ActivitySanitizer(IConfiguration configuration)
{
    private readonly string[] secrets = configuration.AsEnumerable()
        .Where(p => SensitiveKey().IsMatch(p.Key) && !string.IsNullOrEmpty(p.Value))
        .Select(p => p.Value!).Distinct(StringComparer.Ordinal).ToArray();

    public string Identifier(string? value, string fallback = "redacted") =>
        value is { Length: > 0 and <= 200 } && IdentifierPattern().IsMatch(value) &&
        !SensitiveValue().IsMatch(value) && !secrets.Any(s => value.Contains(s, StringComparison.Ordinal))
            ? value : fallback;

    public IReadOnlyDictionary<string, string> Metadata(IEnumerable<KeyValuePair<string, string>>? metadata)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (metadata is null) return result;
        foreach (var (key, value) in metadata.Take(32))
        {
            if (key is "http.method" or "http.request.method")
            {
                if (value is "GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS")
                    result[key] = value;
            }
            else if (key is "http.status_code" or "http.response.status_code")
            {
                if (int.TryParse(value, out var status) && status is >= 100 and <= 599) result[key] = value;
            }
            else if (key is "environment" or "project" or "service" or "agent" or "skill" or "tool" or "connection")
                result[key] = Identifier(value);
        }
        return result;
    }

    public ActivityEvent Sanitize(ActivityEvent e) => e with
    {
        // Authenticated ownership keys must not collapse into a shared redaction placeholder.
        EventType = Identifier(e.EventType), Action = Identifier(e.Action),
        SourceEntityId = e.SourceEntityId is null ? null : Identifier(e.SourceEntityId),
        TargetEntityId = e.TargetEntityId is null ? null : Identifier(e.TargetEntityId),
        ErrorType = e.ErrorType is null ? null : Identifier(e.ErrorType),
        ErrorMessage = e.ErrorType is null ? null : "Operation failed.",
        Metadata = Metadata(e.Metadata)
    };

    [GeneratedRegex("password|secret|token|authorization|credential|private.?key|api.?key|connectionstring", RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveKey();
    [GeneratedRegex("^[a-zA-Z0-9][a-zA-Z0-9_.:/{} -]*$")]
    private static partial Regex IdentifierPattern();
    [GeneratedRegex("bearer|-----BEGIN|eyJ[a-zA-Z0-9_-]{10}|(?:sk|ghp|github_pat)[_-]|password|secret|token|authorization|api.?key", RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveValue();
}

public sealed class SanitizingActivityProcessor(ActivitySanitizer sanitizer, ILogger<SanitizingActivityProcessor> logger) : BaseProcessor<Activity>
{
    public override void OnEnd(Activity activity)
    {
        foreach (var tag in activity.TagObjects.ToArray())
        {
            var value = Convert.ToString(tag.Value, CultureInfo.InvariantCulture) ?? "";
            var safe = tag.Key is "brain.agent_id" or "brain.source_entity_id" or "brain.target_entity_id" or
                "brain.instance_id" or "brain.root" or "brain.child_failed"
                ? sanitizer.Identifier(value)
                : sanitizer.Metadata([new(tag.Key, value)]).GetValueOrDefault(tag.Key);
            activity.SetTag(tag.Key, safe);
        }
        // Activity event/link payloads are immutable. Fail closed rather than exporting unreviewed attributes.
        if (activity.Events.Any() || activity.Links.Any())
        {
            activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            logger.LogWarning("Excluded span {SpanId} from export because it contains immutable event/link payloads", activity.SpanId);
        }
        foreach (var baggage in activity.Baggage.ToArray()) activity.SetBaggage(baggage.Key, null);
        activity.DisplayName = sanitizer.Identifier(activity.OperationName);
        activity.SetStatus(activity.Status, activity.Status == ActivityStatusCode.Error ? "Operation failed." : null);
    }
}
