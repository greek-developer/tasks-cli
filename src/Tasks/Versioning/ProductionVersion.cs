using System.Text.Json;

namespace Tasks.Versioning;

/// <summary>
/// The identity of the build this tool is part of, as captured in ProductionVersion.json at
/// build time. Generated during the build - never hand-written.
/// </summary>
internal sealed record ProductionVersion(
    string Version,
    string CommitMessage,
    string CommitSha,
    string BuildTime)
{
    /// <summary>The file name written beside the tool and read back by the version command.</summary>
    internal const string FileName = "ProductionVersion.json";

    internal static Validated<ProductionVersion> Parse(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            return Validated<ProductionVersion>.Failure($"{FileName} is not valid JSON: {exception.Message}");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Validated<ProductionVersion>.Failure($"{FileName} does not contain a JSON object.");
            }

            var errors = new List<string>();
            var version = ReadString(document.RootElement, errors, "version");
            var message = ReadString(document.RootElement, errors, "commit", "message");
            var sha = ReadString(document.RootElement, errors, "commit", "sha");
            var time = ReadString(document.RootElement, errors, "build", "time");

            return errors.Count > 0
                ? Validated<ProductionVersion>.Failure(errors)
                : Validated<ProductionVersion>.Success(new ProductionVersion(version, message, sha, time));
        }
    }

    /// <summary>Renders the four lines the version command prints.</summary>
    internal string ToDisplay() => string.Join(
        Environment.NewLine,
        $"version: {Version}",
        $"commit-text: {CommitMessage}",
        $"commit-sha: {CommitSha}",
        $"build-time: {BuildTime}");

    private static string ReadString(JsonElement root, List<string> errors, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                errors.Add($"{FileName} is missing '{string.Join('.', path)}'.");
                return string.Empty;
            }
        }

        var value = current.ValueKind == JsonValueKind.String ? current.GetString() : null;
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{FileName} has an empty '{string.Join('.', path)}'.");
            return string.Empty;
        }

        return value;
    }
}
