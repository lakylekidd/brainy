using System.Text.Json;
using Brain.Domain;

namespace Brain.Application;

public static class Validation
{
    public static void Require(bool condition, string message)
    {
        if (!condition) throw new BrainException(400, message);
    }

    public static string Text(string? value, string field, int max)
    {
        Require(!string.IsNullOrWhiteSpace(value) && value.Length <= max, $"{field} must contain 1–{max} characters.");
        return value!.Trim();
    }

    public static void Score(double value, string field) => Require(double.IsFinite(value) && value is >= 0 and <= 1,
        $"{field} must be between 0 and 1.");

    public static string Metadata(JsonElement? metadata)
    {
        if (metadata is null) return "{}";
        Require(metadata.Value.ValueKind == JsonValueKind.Object, "Metadata must be a JSON object.");
        var json = metadata.Value.GetRawText();
        Require(json.Length <= 16384, "Metadata is too large.");
        return json;
    }
}
