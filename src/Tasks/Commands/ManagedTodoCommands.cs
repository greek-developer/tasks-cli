using System.CommandLine;

using Tasks.Config;
using Tasks.Managed;
using Tasks.Todo;

namespace Tasks.Commands;

/// <summary>
/// <c>todo done</c>, <c>todo edit</c>, <c>todo rm</c> and <c>todo open</c> - the commands that
/// change a task already written. They act only on the tasks file of a managed folder, and
/// every change is committed and pushed.
/// </summary>
internal static class ManagedTodoCommands
{
    private const string ReferenceDescription = "The task: its id (abcd-efgh, or a unique first 4+ characters), or file:line in a managed tasks file";

    internal static Command CreateDone()
    {
        var command = new Command("done", "tick a task, record {done-date: …} and move it to the done section")
        {
            new Argument<string>("task") { Description = ReferenceDescription },
        };

        command.SetAction(parseResult => Change(parseResult, (reference, file, index) =>
        {
            var folder = reference.Folder;
            var line = file.Lines[index];

            if (!ManagedFolders.IsOpen(folder, line))
            {
                return Edit.Failed($"'{line.Trim()}' is not an open task.");
            }

            var done = TodoLine.Complete(line, folder.todoPrefixes, DateOnly.FromDateTime(DateTime.Now));
            var at = file.MoveToSection(index, folder.Managed!.DoneSection, done);

            return Edit.Done(
                $"complete task: {TodoLine.Summary(line, folder.todoPrefixes)}",
                $"{done.Trim()} (-> {ManagedFolders.TasksFilePath(folder)}:{at + 1})");
        }));

        return command;
    }

    internal static Command CreateEdit()
    {
        var command = new Command("edit", "change a task's text, due date, tags, project or priority")
        {
            new Argument<string>("task") { Description = ReferenceDescription },
            new Option<string?>("--text") { Description = "New text. Tags, project and markers already on the line are kept" },
            new Option<string?>("--due", "-d") { Description = "New due date: yyyy-MM-dd, today, tomorrow, +3d, +2w, or a weekday" },
            new Option<bool>("--no-due") { Description = "Remove the due date" },
            new Option<string[]>("--add-tag") { Description = "Add a tag. Repeat it, or separate with commas" },
            new Option<string[]>("--remove-tag") { Description = "Remove a tag. Repeat it, or separate with commas" },
            new Option<string?>("--project", "-p") { Description = "Replace the project" },
            new Option<bool>("--no-project") { Description = "Remove the project" },
            new Option<string?>("--priority") { Description = "New priority: a single letter, A highest" },
            new Option<bool>("--no-priority") { Description = "Remove the priority" },
        };

        command.SetAction(parseResult =>
        {
            var change = ReadEdit(parseResult);
            if (change is null)
            {
                return 1;
            }

            return Change(parseResult, (reference, file, index) =>
            {
                var folder = reference.Folder;
                var line = file.Lines[index];

                if (!ManagedFolders.IsOpen(folder, line))
                {
                    return Edit.Failed($"'{line.Trim()}' is not an open task.");
                }

                var edited = change(line, folder);
                if (edited == line)
                {
                    return Edit.Failed("That changes nothing on the line.");
                }

                file.Replace(index, edited);

                return Edit.Done(
                    $"edit task: {TodoLine.Summary(edited, folder.todoPrefixes)}",
                    $"{edited.Trim()} (-> {ManagedFolders.TasksFilePath(folder)}:{index + 1})");
            });
        });

        return command;
    }

    internal static Command CreateRemove()
    {
        var command = new Command("rm", "delete a task's line")
        {
            new Argument<string>("task") { Description = ReferenceDescription },
        };

        command.SetAction(parseResult => Change(parseResult, (reference, file, index) =>
        {
            var folder = reference.Folder;
            var line = file.Lines[index];

            // Done lines may be removed too; anything that is not a task at all may not.
            if (!ManagedFolders.IsOpen(folder, line) && TodoLine.GetId(line) is null)
            {
                return Edit.Failed($"'{line.Trim()}' is not a task.");
            }

            file.RemoveAt(index);

            return Edit.Done(
                $"remove task: {TodoLine.Summary(line, folder.todoPrefixes)}",
                $"removed: {line.Trim()}");
        }));

        return command;
    }

    internal static Command CreateOpen()
    {
        var command = new Command("open", "open the tasks file in $VISUAL / $EDITOR at the task's line")
        {
            new Argument<string>("task") { Description = ReferenceDescription },
        };

        command.SetAction(parseResult =>
        {
            var reference = ManagedFolders.Resolve(ConfigurationManager.Config, parseResult.GetValue<string>("task")!, out var error);
            if (reference is null)
            {
                Console.Error.WriteLine(error);
                return 1;
            }

            var path = ManagedFolders.TasksFilePath(reference.Folder);
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"{path} does not exist yet. Add a task first.");
                return 1;
            }

            var index = reference.Locate(TaskFile.Parse(File.ReadAllText(path)), changedSinceRead: false, out error);
            if (index < 0)
            {
                Console.Error.WriteLine(error);
                return 1;
            }

            // Opening changes nothing, so nothing is pulled or committed; `tasks sync` picks up
            // whatever is edited by hand.
            return Editor.Open(path, index + 1);
        });

        return command;
    }

    internal static Command CreateSync()
    {
        var command = new Command("sync", "commit, pull and push the tasks file of every managed folder")
        {
            new Option<string?>("--folder", "-f") { Description = "Sync only this managed folder" },
        };

        command.SetAction(parseResult =>
        {
            var config = ConfigurationManager.Config;
            var name = parseResult.GetValue<string?>("--folder");

            List<MonitoredFolder> folders;
            if (name is null)
            {
                folders = ManagedFolders.All(config).ToList();
            }
            else
            {
                var folder = ManagedFolders.FindByName(config, name);
                if (folder?.Managed is null)
                {
                    Console.Error.WriteLine($"No managed folder is named '{name}'. See `tasks folders list`.");
                    return 1;
                }

                folders = [folder];
            }

            if (folders.Count == 0)
            {
                Console.Error.WriteLine("No folder is managed; there is nothing to sync.");
                return 1;
            }

            var git = new GitClient();

            // Every folder is attempted even when an earlier one fails.
            return folders.Select(f => ManagedWriter.Sync(f, git)).Max();
        });

        return command;
    }

    /// <summary>Resolves the task, then runs the edit through the pull-write-commit-push cycle.</summary>
    private static int Change(ParseResult parseResult, Func<TaskReference, TaskFile, int, Edit> edit)
    {
        var reference = ManagedFolders.Resolve(ConfigurationManager.Config, parseResult.GetValue<string>("task")!, out var error);
        if (reference is null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        return ManagedWriter.Write(reference.Folder, new GitClient(), (file, changedByPull) =>
        {
            var index = reference.Locate(file, changedByPull, out var locateError);
            return index < 0 ? Edit.Failed(locateError) : edit(reference, file, index);
        });
    }

    /// <summary>
    /// Validates the edit options up front and returns the change they describe, so a bad
    /// option is reported before anything is pulled. Null (after reporting) when unusable.
    /// </summary>
    private static Func<string, MonitoredFolder, string>? ReadEdit(ParseResult parseResult)
    {
        var steps = new List<Func<string, MonitoredFolder, string>>();

        if (parseResult.GetValue<string?>("--text") is { } text)
        {
            text = text.Trim();
            if (text.Length == 0 || text.Contains('\n') || text.Contains('\r'))
            {
                Console.Error.WriteLine("--text must be one non-empty line.");
                return null;
            }

            steps.Add((line, folder) => TodoLine.ReplaceText(line, OpenPrefix(line, folder), text));
        }

        var dueText = parseResult.GetValue<string?>("--due");
        var noDue = parseResult.GetValue<bool>("--no-due");
        if (dueText is not null && noDue)
        {
            Console.Error.WriteLine("Pass --due or --no-due, not both.");
            return null;
        }

        if (dueText is not null)
        {
            if (DueDate.Parse(dueText, DateOnly.FromDateTime(DateTime.Now)) is not { } due)
            {
                Console.Error.WriteLine($"'{dueText}' is not a date. Use yyyy-MM-dd, today, tomorrow, +3d, +2w or a weekday.");
                return null;
            }

            steps.Add((line, _) => TodoLine.SetMarker(line, "due", due.ToString(TodoLine.DateFormat)));
        }
        else if (noDue)
        {
            steps.Add((line, _) => TodoLine.SetMarker(line, "due", null));
        }

        foreach (var (option, add) in new[] { ("--add-tag", true), ("--remove-tag", false) })
        {
            foreach (var raw in (parseResult.GetValue<string[]>(option) ?? [])
                         .SelectMany(t => t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            {
                if (TodoLine.NormalizeTag(raw) is not { } tag)
                {
                    Console.Error.WriteLine($"'{raw}' is not a usable tag. A tag is one word: letters, digits and underscores.");
                    return null;
                }

                steps.Add(add ? (line, _) => TodoLine.AddTag(line, tag) : (line, _) => TodoLine.RemoveTag(line, tag));
            }
        }

        var projectText = parseResult.GetValue<string?>("--project");
        var noProject = parseResult.GetValue<bool>("--no-project");
        if (projectText is not null && noProject)
        {
            Console.Error.WriteLine("Pass --project or --no-project, not both.");
            return null;
        }

        if (projectText is not null)
        {
            if (TodoLine.NormalizeProject(projectText) is not { } project)
            {
                Console.Error.WriteLine($"'{projectText}' is not a usable project. A project is one word: letters, digits and underscores.");
                return null;
            }

            steps.Add((line, _) => TodoLine.SetProject(line, project));
        }
        else if (noProject)
        {
            steps.Add((line, _) => TodoLine.SetProject(line, null));
        }

        var priorityText = parseResult.GetValue<string?>("--priority");
        var noPriority = parseResult.GetValue<bool>("--no-priority");
        if (priorityText is not null && noPriority)
        {
            Console.Error.WriteLine("Pass --priority or --no-priority, not both.");
            return null;
        }

        if (priorityText is not null)
        {
            if (TodoLine.NormalizePriority(priorityText) is not { } priority)
            {
                Console.Error.WriteLine($"'{priorityText}' is not a priority. Use a single letter, A highest.");
                return null;
            }

            steps.Add((line, _) => TodoLine.SetMarker(line, "pri", priority));
        }
        else if (noPriority)
        {
            steps.Add((line, _) => TodoLine.SetMarker(line, "pri", null));
        }

        if (steps.Count == 0)
        {
            Console.Error.WriteLine("Nothing to change. Pass at least one of --text, --due, --no-due, --add-tag, --remove-tag, --project, --no-project, --priority, --no-priority.");
            return null;
        }

        return (line, folder) => steps.Aggregate(line, (current, step) => step(current, folder));
    }

    /// <summary>The prefix the line actually starts with - the longest match, so `- [ ]` wins over `[ ]`.</summary>
    private static string OpenPrefix(string line, MonitoredFolder folder) =>
        folder.todoPrefixes
            .Where(p => line.TrimStart().StartsWith(p, StringComparison.Ordinal))
            .OrderByDescending(p => p.Length)
            .FirstOrDefault()
        ?? ManagedFolders.Prefix(folder);
}
