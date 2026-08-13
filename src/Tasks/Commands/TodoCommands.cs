using System.CommandLine;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Runtime.CompilerServices;

using Tasks.Config;
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

        var todoAddCommand = new Command("add", "add a todo line to a file")
        {
            new Argument<string>("description")
            {
                Description = "The text of the todo. #tags, @projects and {due: yyyy-MM-dd} inside it are picked up by the reader"
            },
            new Argument<string>("filePath")
            {
                Description = "The file the todo is appended to. Created if the containing folder exists"
            },
        };

        todoAddCommand.SetAction(Add);

        return new[]
        {
            new Command("todo", "Manage Todos")
            {
                todoListCommand,
                todoAddCommand
            }
        };
    }

    /// <summary>
    /// Appends one todo line to a text file. This is the only command that writes to a file the
    /// user owns, so every way it can go wrong reports on stderr and returns a non-zero exit
    /// code - it never claims to have written something it did not.
    /// </summary>
    private static int Add(ParseResult parseResult)
    {
        var description = (parseResult.GetValue<string>("description") ?? string.Empty).Trim();
        var requestedPath = (parseResult.GetValue<string>("filePath") ?? string.Empty).Trim();

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

        var line = WithPrefix(description, filePath);
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
