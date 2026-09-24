namespace Tasks.Managed;

/// <summary>
/// The shape of a managed tasks file - a <c># Tasks</c> heading, an open section and a done
/// section - and the edits made to it. Works on the text only; reading and writing the file is
/// the caller's job.
/// </summary>
internal sealed class TaskFile
{
    private readonly List<string> _lines;

    private TaskFile(List<string> lines, string newLine)
    {
        _lines = lines;
        NewLine = newLine;
    }

    internal string NewLine { get; }

    internal IReadOnlyList<string> Lines => _lines;

    /// <summary>Parses file content; an empty string is a file that does not exist yet.</summary>
    internal static TaskFile Parse(string content)
    {
        var newLine = content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n"
            : content.Contains('\n') ? "\n"
                : Environment.NewLine;

        var lines = content.Length == 0
            ? new List<string> { "# Tasks", string.Empty }
            : content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();

        // A file ending in a newline splits into a trailing empty entry - drop it, and put the
        // final newline back when the file is written.
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return new TaskFile(lines, newLine);
    }

    public override string ToString() => string.Join(NewLine, _lines) + NewLine;

    /// <summary>Every id on the file - what a new id must not collide with.</summary>
    internal HashSet<string> Ids() =>
        _lines.Select(Todo.TodoLine.GetId).OfType<string>().ToHashSet(StringComparer.Ordinal);

    /// <summary>Indexes of the lines whose id starts with the (normalized) reference.</summary>
    internal List<int> FindByReference(string normalizedReference) =>
        Enumerable.Range(0, _lines.Count)
            .Where(i => Todo.TodoLine.GetId(_lines[i]) is { } id && Todo.TaskId.Matches(id, normalizedReference))
            .ToList();

    /// <summary>Adds a line at the end of the section, creating the section when it is missing.</summary>
    /// <returns>The index the line landed at.</returns>
    internal int AddToSection(string heading, string line, string? createBefore = null)
    {
        var start = FindHeading(heading);
        if (start < 0)
        {
            start = CreateSection(heading, createBefore);
        }

        var end = SectionEnd(start);

        // Land after the section's last non-blank line, so the blank line that separates it
        // from the next heading stays where it is.
        var insertAt = end;
        while (insertAt > start + 1 && _lines[insertAt - 1].Trim().Length == 0)
        {
            insertAt--;
        }

        // A heading directly followed by a list reads fine; keep the one blank line after it
        // if it is there.
        if (insertAt == start + 1 && insertAt < end && _lines[insertAt].Trim().Length == 0)
        {
            insertAt++;
        }

        _lines.Insert(insertAt, line);
        return insertAt;
    }

    internal void Replace(int index, string line) => _lines[index] = line;

    internal void RemoveAt(int index) => _lines.RemoveAt(index);

    /// <summary>Removes the line and adds its replacement to the end of another section.</summary>
    internal int MoveToSection(int index, string heading, string line)
    {
        _lines.RemoveAt(index);
        return AddToSection(heading, line);
    }

    private int FindHeading(string heading) =>
        _lines.FindIndex(line => line.Trim().Equals(heading.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The index of the next heading of the same or a higher level, or the end of the file.</summary>
    private int SectionEnd(int start)
    {
        var level = HeadingLevel(_lines[start]);
        for (var i = start + 1; i < _lines.Count; i++)
        {
            var other = HeadingLevel(_lines[i]);
            if (other > 0 && other <= level)
            {
                return i;
            }
        }

        return _lines.Count;
    }

    private int CreateSection(string heading, string? createBefore)
    {
        var before = createBefore is null ? -1 : FindHeading(createBefore);
        var at = before < 0 ? _lines.Count : before;

        var block = new List<string>();
        if (at > 0 && _lines[at - 1].Trim().Length > 0)
        {
            block.Add(string.Empty);
        }

        block.Add(heading);
        block.Add(string.Empty);

        // Inserted ahead of another heading, keep a blank line between the two sections.
        if (at < _lines.Count)
        {
            block.Add(string.Empty);
        }

        _lines.InsertRange(at, block);
        return at + block.IndexOf(heading);
    }

    private static int HeadingLevel(string line)
    {
        var trimmed = line.TrimStart();
        var level = trimmed.TakeWhile(c => c == '#').Count();
        return level > 0 && level < trimmed.Length && trimmed[level] == ' ' ? level : 0;
    }
}
