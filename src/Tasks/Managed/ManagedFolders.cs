using Tasks.Config;
using Tasks.Todo;

namespace Tasks.Managed;

/// <summary>A task named on the command line, and the managed folder whose tasks file holds it.</summary>
internal sealed record TaskReference(MonitoredFolder Folder, string Raw, string? NormalizedId, int? LineIndex)
{
    /// <summary>
    /// Finds the line in the file as it is now. An id is looked up afresh, so a pull that moved
    /// lines around does not matter. A line number is only trusted while the file is unchanged
    /// since the number was read.
    /// </summary>
    internal int Locate(TaskFile file, bool changedSinceRead, out string error)
    {
        error = string.Empty;

        if (NormalizedId is not null)
        {
            var matches = file.FindByReference(NormalizedId);
            if (matches.Count == 1)
            {
                return matches[0];
            }

            error = matches.Count == 0
                ? $"No task with id '{Raw}' in {ManagedFolders.TasksFilePath(Folder)}."
                : $"'{Raw}' matches {matches.Count} tasks. Use more of the id.";
            return -1;
        }

        if (changedSinceRead)
        {
            error = $"{ManagedFolders.TasksFilePath(Folder)} changed when it was pulled, so line {LineIndex + 1} may now be a different task. List the tasks again, or refer to the task by its id.";
            return -1;
        }

        if (LineIndex is not { } index || index >= file.Lines.Count)
        {
            error = $"{ManagedFolders.TasksFilePath(Folder)} has no line {LineIndex + 1}.";
            return -1;
        }

        return index;
    }
}

/// <summary>Finds managed folders, their tasks files, and the tasks in them.</summary>
internal static class ManagedFolders
{
    internal static IEnumerable<MonitoredFolder> All(TasksConfig config) =>
        config.Folders.Where(f => f.Managed is not null);

    internal static MonitoredFolder? FindByName(TasksConfig config, string name) =>
        config.Folders.FirstOrDefault(f => f.FriendlyName.Equals(name, StringComparison.OrdinalIgnoreCase));

    internal static string TasksFilePath(MonitoredFolder folder) =>
        Path.GetFullPath(Path.Combine(folder.Path, folder.Managed!.TasksFile));

    internal static string Prefix(MonitoredFolder folder) =>
        folder.todoPrefixes.FirstOrDefault() ?? "- [ ]";

    /// <summary>
    /// The folder a new task goes to: the one named, else the configured default, else the only
    /// managed folder there is.
    /// </summary>
    internal static MonitoredFolder? ResolveTarget(TasksConfig config, string? name, out string error)
    {
        error = string.Empty;
        var managed = All(config).ToList();

        if (name is not null || config.DefaultFolder is not null)
        {
            var wanted = name ?? config.DefaultFolder!;
            var folder = FindByName(config, wanted);

            if (folder is null)
            {
                error = $"No monitored folder is named '{wanted}'. See `tasks folders list`.";
                return null;
            }

            if (folder.Managed is null)
            {
                error = $"'{wanted}' is monitored but not managed, so tasks are not written to it. Run `tasks folders manage {wanted}` first.";
                return null;
            }

            return folder;
        }

        switch (managed.Count)
        {
            case 1:
                return managed[0];
            case 0:
                error = "No folder is managed. Run `tasks folders manage <name>`, or pass the file to append to.";
                return null;
            default:
                error = $"{managed.Count} folders are managed; name one with --folder, or set a default with `tasks folders manage <name> --default`.";
                return null;
        }
    }

    /// <summary>
    /// Resolves an id (or a unique prefix of one) across every managed tasks file, or a
    /// <c>file:line</c> pointing into one of them. Reads the files as they are on disk now.
    /// </summary>
    internal static TaskReference? Resolve(TasksConfig config, string raw, out string error)
    {
        error = string.Empty;
        var managed = All(config).ToList();

        if (managed.Count == 0)
        {
            error = "No folder is managed, so there are no tasks to change. Run `tasks folders manage <name>` first.";
            return null;
        }

        var normalized = TaskId.NormalizeReference(raw);
        if (normalized is not null)
        {
            var hits = new List<(MonitoredFolder Folder, int Count)>();
            foreach (var folder in managed)
            {
                var count = ReadIfPresent(TasksFilePath(folder))?.FindByReference(normalized).Count ?? 0;
                if (count > 0)
                {
                    hits.Add((folder, count));
                }
            }

            if (hits.Count == 1 && hits[0].Count == 1)
            {
                return new TaskReference(hits[0].Folder, raw, normalized, null);
            }

            if (hits.Sum(h => h.Count) > 1)
            {
                error = $"'{raw}' matches {hits.Sum(h => h.Count)} tasks. Use more of the id.";
                return null;
            }
        }

        var separator = raw.LastIndexOf(':');
        if (separator > 0 && int.TryParse(raw[(separator + 1)..], out var lineNumber) && lineNumber > 0)
        {
            string path;
            try
            {
                path = Path.GetFullPath(raw[..separator]);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                error = $"'{raw}' is not a usable file path: {exception.Message}";
                return null;
            }

            var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var folder = managed.FirstOrDefault(f => string.Equals(TasksFilePath(f), path, comparison));

            if (folder is null)
            {
                error = $"'{path}' is not the tasks file of a managed folder. Only managed tasks files are changed.";
                return null;
            }

            return new TaskReference(folder, raw, null, lineNumber - 1);
        }

        error = normalized is null
            ? $"'{raw}' is neither a task id (abcd-efgh, or at least its first {TaskId.MinimumPrefix} characters) nor a file:line."
            : $"No task with id '{raw}' in any managed folder.";
        return null;
    }

    /// <summary>True when the line is an open todo by the folder's prefixes.</summary>
    internal static bool IsOpen(MonitoredFolder folder, string line) =>
        folder.todoPrefixes.Any(p => line.TrimStart().StartsWith(p, StringComparison.Ordinal));

    private static TaskFile? ReadIfPresent(string path)
    {
        try
        {
            return File.Exists(path) ? TaskFile.Parse(File.ReadAllText(path)) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
