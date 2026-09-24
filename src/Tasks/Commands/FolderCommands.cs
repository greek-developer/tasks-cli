using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Runtime.CompilerServices;

using Tasks.Config;
using Tasks.Managed;

namespace Tasks.Commands;

public static class FolderCommands
{

    public static IEnumerable<Command> GenerateFolderCommands()
    {
        var foldersListCommand = new Command("list", "list all monitored folders");
        foldersListCommand.SetAction(_ =>
        {
            Console.WriteLine("");
            Console.WriteLine("Monitored Folders:");
            Console.WriteLine("");
            Console.WriteLine(
                string.Join(
                    Environment.NewLine,
                    ConfigurationManager.Config.Folders.Select(Describe)
                )
            );
            Console.WriteLine("");
        });

        var folderAddCommand = new Command("add", "add a monitored folder")
        {
            new Argument<string>("path"),
            new Option<string>("--name", "-n") { Required = false },
            new Option<bool>("--managed") { Description = "Let the tool write tasks here, committing and pushing each change. The folder must be in a git repository" },
            new Option<string?>("--tasks-file") { Description = "With --managed: the tasks file, relative to the folder (default tasks/tasks.md)" },
            new Option<bool>("--no-push") { Description = "With --managed: commit each change but do not push it" },
            new Option<bool>("--default") { Description = "With --managed: new tasks go here when no --folder is named" },
        };

        folderAddCommand.SetAction(parseResult =>
        {
            var path = Path.GetFullPath(parseResult.GetValue<string>("path")!);
            var name = parseResult.GetValue<string>("--name") ?? Path.GetFileName(path);

            if (ManagedFolders.FindByName(ConfigurationManager.Config, name) is not null)
            {
                Console.Error.WriteLine($"A folder is already named '{name}'. Pick another with --name.");
                return 1;
            }

            var folder = new MonitoredFolder
            {
                Path = path,
                FriendlyName = name
            };

            if (parseResult.GetValue<bool>("--managed"))
            {
                var error = Manage(folder, parseResult);
                if (error is not null)
                {
                    Console.Error.WriteLine(error);
                    return 1;
                }
            }
            else if (HasManagedOptions(parseResult))
            {
                Console.Error.WriteLine("--tasks-file, --no-push and --default only apply with --managed.");
                return 1;
            }

            ConfigurationManager.Config.Folders.Add(folder);
            ConfigurationManager.SaveConfig();
            Console.WriteLine(Describe(folder));
            return 0;
        });

        var folderManageCommand = new Command("manage", "let the tool write tasks to a monitored folder, committing and pushing each change")
        {
            new Argument<string>("name") { Description = "The folder's name, as `folders list` shows it" },
            new Option<string?>("--tasks-file") { Description = "The tasks file, relative to the folder (default tasks/tasks.md)" },
            new Option<bool>("--no-push") { Description = "Commit each change but do not push it" },
            new Option<bool>("--default") { Description = "New tasks go here when no --folder is named" },
        };

        folderManageCommand.SetAction(parseResult =>
        {
            var name = parseResult.GetValue<string>("name")!;
            var folder = ManagedFolders.FindByName(ConfigurationManager.Config, name);
            if (folder is null)
            {
                Console.Error.WriteLine($"No monitored folder is named '{name}'. See `tasks folders list`.");
                return 1;
            }

            var error = Manage(folder, parseResult);
            if (error is not null)
            {
                Console.Error.WriteLine(error);
                return 1;
            }

            ConfigurationManager.SaveConfig();
            Console.WriteLine(Describe(folder));
            return 0;
        });

        var folderUnmanageCommand = new Command("unmanage", "stop writing to a folder; it stays monitored, read-only")
        {
            new Argument<string>("name") { Description = "The folder's name, as `folders list` shows it" },
        };

        folderUnmanageCommand.SetAction(parseResult =>
        {
            var config = ConfigurationManager.Config;
            var name = parseResult.GetValue<string>("name")!;
            var folder = ManagedFolders.FindByName(config, name);
            if (folder?.Managed is null)
            {
                Console.Error.WriteLine($"No managed folder is named '{name}'. See `tasks folders list`.");
                return 1;
            }

            folder.Managed = null;
            if (string.Equals(config.DefaultFolder, folder.FriendlyName, StringComparison.OrdinalIgnoreCase))
            {
                config.DefaultFolder = null;
            }

            ConfigurationManager.SaveConfig();
            Console.WriteLine(Describe(folder));
            return 0;
        });

        var foldersCommand = new Command("folders", "Manage monitored folders")
        {
            foldersListCommand,
            folderAddCommand,
            folderManageCommand,
            folderUnmanageCommand
        };

        return new[] { foldersCommand };
    }

    private static string Describe(MonitoredFolder folder)
    {
        var line = $"{folder.FriendlyName}: {folder.Path}";
        if (folder.Managed is not { } managed)
        {
            return line;
        }

        var isDefault = string.Equals(ConfigurationManager.Config.DefaultFolder, folder.FriendlyName, StringComparison.OrdinalIgnoreCase);
        var sync = managed.Git.Push ? "commit+push" : "commit";
        return $"{line} [managed: {managed.TasksFile}, {sync}{(isDefault ? ", default" : string.Empty)}]";
    }

    private static bool HasManagedOptions(ParseResult parseResult) =>
        parseResult.GetValue<string?>("--tasks-file") is not null
        || parseResult.GetValue<bool>("--no-push")
        || parseResult.GetValue<bool>("--default");

    /// <summary>
    /// Marks the folder managed. Refuses a folder outside a git repository, since every
    /// change to a managed folder is committed. Returns the reason on refusal.
    /// </summary>
    private static string? Manage(MonitoredFolder folder, ParseResult parseResult)
    {
        if (!Directory.Exists(folder.Path))
        {
            return $"The folder '{folder.Path}' does not exist.";
        }

        if (Repository.Open(new GitClient(), folder.Path, out var error) is null)
        {
            return error;
        }

        var tasksFile = parseResult.GetValue<string?>("--tasks-file")?.Trim();
        if (tasksFile is { Length: 0 } || (tasksFile is not null && Path.IsPathRooted(tasksFile)))
        {
            return "--tasks-file is a path relative to the folder, such as tasks/tasks.md.";
        }

        folder.Managed ??= new ManagedSettings();
        if (tasksFile is not null)
        {
            folder.Managed.TasksFile = tasksFile.Replace('\\', '/');
        }

        if (parseResult.GetValue<bool>("--no-push"))
        {
            folder.Managed.Git.Push = false;
        }

        if (parseResult.GetValue<bool>("--default"))
        {
            ConfigurationManager.Config.DefaultFolder = folder.FriendlyName;
        }

        return null;
    }
}
