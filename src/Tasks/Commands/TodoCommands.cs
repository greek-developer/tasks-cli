using System.CommandLine;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Runtime.CompilerServices;

using Tasks.Config;
using Tasks.Managed;
using Tasks.Todo;

namespace Tasks.Commands;

public static class TodoCommands
{

    public static IEnumerable<Command> GenerateTodoCommands()
    {
        var todoListCommand = new Command("list", "list all todos")
        {
            new Option<string?>("--tags", "-t")
            {
                Required = false,
                Description = "Comma-separated tags to filter todos by (e.g. --tags tag1,tag2)"
            },
        };

        todoListCommand.SetAction(parseResult =>
        {
            var tags = parseResult
                .GetValue<string?>("--tags")
                ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            Console.WriteLine("");
            Console.WriteLine("Todos:");
            Console.WriteLine("");

            var todos = TodoManager
                .Todos
                .Where(t => tags == null || tags.Length == 0 || t.Tags.Intersect(tags).Any());

            Console.WriteLine(
                string.Join(
                    Environment.NewLine,
                    todos
                        .Where(t => t.DueDate != null)
                        .OrderBy(t => t.DueDate)
                        .Select(t => $"{t.Description} (-> {t.FilePath}:{t.LineNumber})")
                )

            );
            Console.WriteLine("");

            Console.WriteLine(
                string.Join(
                    Environment.NewLine,
                    todos
                        .Where(t => t.DueDate == null)
                        .Select(t => $"{t.Description} (-> {t.FilePath}:{t.LineNumber})")
                )
            );

            Console.WriteLine("");            
        });        

        var todoAddCommand = new Command("add", "add a todo - to a managed folder's tasks file, or appended to a named file")
        {
            new Argument<string>("description")
            {
                Description = "The text of the todo. #tags, @projects and {due: yyyy-MM-dd} inside it are picked up by the reader"
            },
            new Argument<string?>("filePath")
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = "Append to this file instead of a managed folder. Created if the containing folder exists; never committed"
            },
            new Option<string?>("--folder", "-f")
            {
                Description = "The managed folder to add to, by name. Defaults to the configured default, or the only managed folder"
            },
            new Option<string?>("--due", "-d")
            {
                Description = "Due date: yyyy-MM-dd, today, tomorrow, +3d, +2w, or a weekday (mon..sun)"
            },
            new Option<string[]>("--tag", "-t")
            {
                Description = "A tag, without the #. Repeat it, or separate with commas",
                AllowMultipleArgumentsPerToken = false
            },
            new Option<string?>("--project", "-p")
            {
                Description = "The project, with or without the @"
            },
            new Option<string?>("--priority")
            {
                Description = "Priority: a single letter, A highest"
            },
        };

        todoAddCommand.SetAction(Add);

        return new[]
        {
            new Command("todo", "Manage Todos")
            {
                todoListCommand,
                todoAddCommand,
                ManagedTodoCommands.CreateDone(),
                ManagedTodoCommands.CreateEdit(),
                ManagedTodoCommands.CreateRemove(),
                ManagedTodoCommands.CreateOpen(),
            }
        };
    }

    /// <summary>
    /// Adds one todo. Without a file path it goes to a managed folder's tasks file and is
    /// committed and pushed; with one it is appended to that file, as it always was. Every way it
    /// can go wrong reports on stderr and returns a non-zero exit code - it never claims to have
    /// written something it did not.
    /// </summary>
    private static int Add(ParseResult parseResult)
    {
        var description = (parseResult.GetValue<string>("description") ?? string.Empty).Trim();
        var requestedPath = parseResult.GetValue<string?>("filePath")?.Trim();
        var folderName = parseResult.GetValue<string?>("--folder");

        if (description.Length == 0)
        {
            Console.Error.WriteLine("The description is empty. Pass the text of the todo as the first argument.");
            return 1;
        }

        if (description.Contains('\n') || description.Contains('\r'))
        {
            Console.Error.WriteLine("A todo is one line. Remove the line breaks from the description.");
            return 1;
        }

        var fields = ReadFields(parseResult);
        if (fields is null)
        {
            return 1;
        }

        if (requestedPath is not null && folderName is not null)
        {
            Console.Error.WriteLine("Pass either a file path or --folder, not both.");
            return 1;
        }

        return requestedPath is null
            ? AddToManaged(description, folderName, fields)
            : AppendToFile(description, requestedPath, fields);
    }

    private sealed record Fields(List<string> Tags, string? Project, DateOnly? Due, string? Priority);

    /// <summary>The optional fields of `todo add`, validated; null (after reporting) when any is unusable.</summary>
    private static Fields? ReadFields(ParseResult parseResult)
    {
        var tags = new List<string>();
        foreach (var raw in (parseResult.GetValue<string[]>("--tag") ?? [])
                     .SelectMany(t => t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
        {
            var tag = TodoLine.NormalizeTag(raw);
            if (tag is null)
            {
                Console.Error.WriteLine($"'{raw}' is not a usable tag. A tag is one word: letters, digits and underscores.");
                return null;
            }

            if (!tags.Contains(tag))
            {
                tags.Add(tag);
            }
        }

        string? project = null;
        if (parseResult.GetValue<string?>("--project") is { } rawProject)
        {
            project = TodoLine.NormalizeProject(rawProject);
            if (project is null)
            {
                Console.Error.WriteLine($"'{rawProject}' is not a usable project. A project is one word: letters, digits and underscores.");
                return null;
            }
        }

        DateOnly? due = null;
        if (parseResult.GetValue<string?>("--due") is { } rawDue)
        {
            due = DueDate.Parse(rawDue, DateOnly.FromDateTime(DateTime.Now));
            if (due is null)
            {
                Console.Error.WriteLine($"'{rawDue}' is not a date. Use yyyy-MM-dd, today, tomorrow, +3d, +2w or a weekday.");
                return null;
            }
        }

        string? priority = null;
        if (parseResult.GetValue<string?>("--priority") is { } rawPriority)
        {
            priority = TodoLine.NormalizePriority(rawPriority);
            if (priority is null)
            {
                Console.Error.WriteLine($"'{rawPriority}' is not a priority. Use a single letter, A highest.");
                return null;
            }
        }

        return new Fields(tags, project, due, priority);
    }

    private static int AddToManaged(string description, string? folderName, Fields fields)
    {
        var folder = ManagedFolders.ResolveTarget(ConfigurationManager.Config, folderName, out var error);
        if (folder is null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        // The folder's own prefix is always used, so every line in the file reads alike.
        var prefix = ManagedFolders.Prefix(folder);
        var text = StripPrefix(description, folder.todoPrefixes);
        var tasksPath = ManagedFolders.TasksFilePath(folder);

        return ManagedWriter.Write(folder, new GitClient(), (file, _) =>
        {
            var id = TaskId.Create(text, file.Ids());
            var line = TodoLine.Compose(prefix, text, fields.Tags, fields.Project, fields.Due, fields.Priority, id);
            var index = file.AddToSection(folder.Managed!.OpenSection, line, createBefore: folder.Managed.DoneSection);

            return Edit.Done(
                $"add task: {TodoLine.Summary(line, folder.todoPrefixes)}",
                $"{line} (-> {tasksPath}:{index + 1})");
        });
    }

    private static string StripPrefix(string description, IEnumerable<string> prefixes)
    {
        var prefix = prefixes
            .Where(p => description.StartsWith(p, StringComparison.Ordinal))
            .OrderByDescending(p => p.Length)
            .FirstOrDefault();

        return prefix is null ? description : description[prefix.Length..].Trim();
    }

    /// <summary>Appends one line to a file named on the command line. No git: the file is the user's.</summary>
    private static int AppendToFile(string description, string requestedPath, Fields fields)
    {
        if (requestedPath.Length == 0)
        {
            Console.Error.WriteLine("The file path is empty. Pass the file the todo belongs in as the second argument.");
            return 1;
        }

        string filePath;
        try
        {
            filePath = Path.GetFullPath(requestedPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            Console.Error.WriteLine($"'{requestedPath}' is not a usable file path: {exception.Message}");
            return 1;
        }

        // The file is created when it is missing, but the folder is not: a typo in a folder name
        // would otherwise scatter todo files where nobody looks for them.
        var folder = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            Console.Error.WriteLine($"The folder '{folder}' does not exist. Point at a file inside an existing folder.");
            return 1;
        }

        string existing;
        try
        {
            existing = File.Exists(filePath) ? File.ReadAllText(filePath) : string.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not read '{filePath}': {exception.Message}");
            return 1;
        }

        var line = TodoLine.Compose(string.Empty, WithPrefix(description, filePath), fields.Tags, fields.Project, fields.Due, fields.Priority, id: null);
        var newLine = DetectNewLine(existing);

        var content = new StringBuilder(existing);
        if (existing.Length > 0 && !existing.EndsWith('\n'))
        {
            content.Append(newLine);
        }

        content.Append(line).Append(newLine);

        try
        {
            File.WriteAllText(filePath, content.ToString());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not write to '{filePath}': {exception.Message}");
            return 1;
        }

        var lineNumber = content.ToString().Count(c => c == '\n');
        Console.WriteLine($"{line} (-> {filePath}:{lineNumber})");
        return 0;
    }

    /// <summary>
    /// A todo is only found again if it starts with a prefix the reader recognises, so the
    /// prefix configured for the folder the file lives in is applied - unless the caller
    /// already wrote one.
    /// </summary>
    private static string WithPrefix(string description, string filePath)
    {
        var folder = ConfigurationManager.Config.Folders.FirstOrDefault(f => Contains(f.Path, filePath));
        var prefixes = folder?.todoPrefixes ?? new MonitoredFolder().todoPrefixes;

        return prefixes.Any(p => description.StartsWith(p, StringComparison.Ordinal))
            ? description
            : $"{prefixes.FirstOrDefault() ?? "- [ ]"} {description}";
    }

    private static bool Contains(string folderPath, string filePath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return false;
        }

        string root;
        try
        {
            root = Path.GetFullPath(folderPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (!root.EndsWith(Path.DirectorySeparatorChar))
        {
            root += Path.DirectorySeparatorChar;
        }

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return filePath.StartsWith(root, comparison);
    }

    /// <summary>Matches whatever the file already uses, so one line does not mix endings.</summary>
    private static string DetectNewLine(string content) =>
        content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n"
            : content.Contains('\n') ? "\n"
                : Environment.NewLine;
}
