using System.Globalization;
using System.Text.RegularExpressions;

namespace Tasks.Todo;

/// <summary>
/// Reads and rewrites the inline syntax of one todo line - <c>#tag</c>, <c>@project</c> and
/// <c>{key: value}</c> markers. Pure string work: nothing here touches the disk, so every rule
/// about how a line is composed or edited can be tested on its own.
/// </summary>
/// <remarks>
/// The tool writes this default syntax regardless of the regexes a folder configures for
/// reading, so a managed folder should keep the default patterns.
/// </remarks>
internal static partial class TodoLine
{
    internal const string DateFormat = "yyyy-MM-dd";

    internal const string DoneDateKey = "done-date";

    [GeneratedRegex(@"\{id: ([0-9a-z]{4}-[0-9a-z]{4})\}")]
    private static partial Regex IdRegex();

    [GeneratedRegex(@"(?<!\S)#(\w+)")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"(?<!\S)@(\w+)")]
    private static partial Regex ProjectRegex();

    [GeneratedRegex(@"\{[\w-]+: [^}]*\}")]
    private static partial Regex MarkerRegex();

    [GeneratedRegex(@"^\w+$")]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"^[A-Z]$")]
    private static partial Regex PriorityRegex();

    [GeneratedRegex(@"[ \t]{2,}")]
    private static partial Regex RepeatedSpaceRegex();

    /// <summary>
    /// Builds a new todo line: the text, then the tags, the project, the due date, the
    /// priority and the id, each in the syntax the reader picks up.
    /// </summary>
    internal static string Compose(
        string prefix,
        string text,
        IEnumerable<string> tags,
        string? project,
        DateOnly? due,
        string? priority,
        string? id)
    {
        var parts = new List<string> { prefix, text.Trim() };

        foreach (var tag in tags)
        {
            if (!HasTag(text, tag))
            {
                parts.Add($"#{tag}");
            }
        }

        if (project is not null && !ProjectRegex().Matches(text).Any(m => m.Groups[1].Value == project))
        {
            parts.Add($"@{project}");
        }

        if (due is not null)
        {
            parts.Add(Marker("due", due.Value.ToString(DateFormat, CultureInfo.InvariantCulture)));
        }

        if (priority is not null)
        {
            parts.Add(Marker("pri", priority));
        }

        if (id is not null)
        {
            parts.Add(Marker("id", id));
        }

        return string.Join(' ', parts.Where(p => p.Length > 0));
    }

    internal static string? GetId(string line)
    {
        var match = IdRegex().Match(line);
        return match.Success ? match.Groups[1].Value : null;
    }

    internal static bool HasTag(string line, string tag) =>
        TagRegex().Matches(line).Any(m => m.Groups[1].Value == tag);

    internal static string AddTag(string line, string tag) =>
        HasTag(line, tag) ? line : InsertBeforeMarkers(line, $"#{tag}");

    internal static string RemoveTag(string line, string tag) =>
        Tidy(Regex.Replace(line, $@"(?<!\S)#{Regex.Escape(tag)}(?!\w)", string.Empty));

    /// <summary>Replaces every <c>@project</c> on the line with one, or removes them all when null.</summary>
    internal static string SetProject(string line, string? project)
    {
        var without = Tidy(ProjectRegex().Replace(line, string.Empty));
        return project is null ? without : InsertBeforeMarkers(without, $"@{project}");
    }

    /// <summary>Sets a <c>{key: value}</c> marker, replacing one already there; null removes it.</summary>
    internal static string SetMarker(string line, string key, string? value)
    {
        var existing = new Regex($@"\{{{Regex.Escape(key)}: [^}}]*\}}");

        if (value is null)
        {
            return Tidy(existing.Replace(line, string.Empty));
        }

        if (existing.IsMatch(line))
        {
            return existing.Replace(line, Marker(key, value), 1);
        }

        // The id stays last on the line, where it is easy to find.
        var id = IdRegex().Match(line);
        return id.Success
            ? $"{line[..id.Index].TrimEnd()} {Marker(key, value)} {line[id.Index..]}".TrimEnd()
            : $"{line.TrimEnd()} {Marker(key, value)}";
    }

    /// <summary>
    /// Replaces the free text of a line while keeping everything structured on it: the prefix,
    /// the markers, and any tag or project the new text does not already carry.
    /// </summary>
    internal static string ReplaceText(string line, string prefix, string newText)
    {
        var body = line.TrimStart();
        var indent = line[..^body.Length];
        body = body.StartsWith(prefix, StringComparison.Ordinal) ? body[prefix.Length..] : body;

        var parts = new List<string> { prefix, newText.Trim() };
        parts.AddRange(TagRegex().Matches(body)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .Where(tag => !HasTag(newText, tag))
            .Select(tag => $"#{tag}"));
        parts.AddRange(ProjectRegex().Matches(body)
            .Select(m => m.Value)
            .Distinct()
            .Where(project => !ProjectRegex().Matches(newText).Any(m => m.Value == project)));
        parts.AddRange(MarkerRegex().Matches(body).Select(m => m.Value));

        return indent + string.Join(' ', parts.Where(p => p.Length > 0));
    }

    /// <summary>
    /// Marks the line done: the open checkbox is ticked (a bare <c>TODO</c>-style prefix becomes a
    /// ticked checkbox) and the completion date is recorded.
    /// </summary>
    internal static string Complete(string line, IEnumerable<string> prefixes, DateOnly today)
    {
        var body = line.TrimStart();
        var indent = line[..^body.Length];

        var prefix = prefixes
            .Where(p => body.StartsWith(p, StringComparison.Ordinal))
            .OrderByDescending(p => p.Length)
            .FirstOrDefault();

        string ticked;
        if (prefix is not null && prefix.Contains("[ ]", StringComparison.Ordinal))
        {
            ticked = prefix.Replace("[ ]", "[x]", StringComparison.Ordinal) + body[prefix.Length..];
        }
        else if (prefix is not null)
        {
            ticked = "- [x]" + body[prefix.Length..];
        }
        else
        {
            ticked = "- [x] " + body;
        }

        return SetMarker(indent + ticked, DoneDateKey, today.ToString(DateFormat, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The line without its prefix or markers, cut to a length that fits a commit subject.
    /// </summary>
    internal static string Summary(string line, IEnumerable<string> prefixes, int maxLength = 60)
    {
        var body = line.Trim();
        var prefix = prefixes
            .Append("- [x]")
            .Where(p => body.StartsWith(p, StringComparison.Ordinal))
            .OrderByDescending(p => p.Length)
            .FirstOrDefault();

        if (prefix is not null)
        {
            body = body[prefix.Length..];
        }

        body = Tidy(MarkerRegex().Replace(body, string.Empty)).Trim();
        return body.Length <= maxLength ? body : body[..(maxLength - 1)].TrimEnd() + "…";
    }

    /// <summary>Accepts <c>work</c> or <c>#work</c>; null when the name is not a single word.</summary>
    internal static string? NormalizeTag(string raw) => NormalizeWord(raw, '#');

    /// <summary>Accepts <c>taxes</c> or <c>@taxes</c>; null when the name is not a single word.</summary>
    internal static string? NormalizeProject(string raw) => NormalizeWord(raw, '@');

    /// <summary>A single letter, upper-cased; null when it is anything else.</summary>
    internal static string? NormalizePriority(string raw)
    {
        var value = raw.Trim().ToUpperInvariant();
        return PriorityRegex().IsMatch(value) ? value : null;
    }

    private static string? NormalizeWord(string raw, char sigil)
    {
        var value = raw.Trim().TrimStart(sigil);
        return WordRegex().IsMatch(value) ? value : null;
    }

    private static string Marker(string key, string value) => $"{{{key}: {value}}}";

    /// <summary>Tags and projects read best ahead of the <c>{key: value}</c> markers.</summary>
    private static string InsertBeforeMarkers(string line, string token)
    {
        var firstMarker = MarkerRegex().Match(line);
        if (!firstMarker.Success)
        {
            return $"{line.TrimEnd()} {token}";
        }

        return Tidy($"{line[..firstMarker.Index].TrimEnd()} {token} {line[firstMarker.Index..]}");
    }

    /// <summary>Collapses the gaps a removed token leaves behind, without touching indentation.</summary>
    private static string Tidy(string line)
    {
        var body = line.TrimStart();
        var indent = line[..^body.Length];
        return indent + RepeatedSpaceRegex().Replace(body, " ").TrimEnd();
    }
}
