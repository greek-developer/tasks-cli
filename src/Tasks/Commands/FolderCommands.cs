using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Runtime.CompilerServices;

using Tasks.Config;
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
                    ConfigurationManager.Config.Folders.Select(f => $"{f.FriendlyName}: {f.Path}")
                )
            );
            Console.WriteLine("");
        });

        var folderAddCommand = new Command("add", "add a monitored folder")
        {
            new Argument<string>("path"),
            new Option<string>("--name", "-n") { Required = false }
        };

        folderAddCommand.SetAction(parseResult =>
        {
            var path = Path.GetFullPath(parseResult.GetValue<string>("path")!);
            var name = parseResult.GetValue<string>("--name") ?? Path.GetFileName(path);

            ConfigurationManager.Config.Folders.Add(new MonitoredFolder
            {
                Path = path,
                FriendlyName = name
            });

            ConfigurationManager.SaveConfig();           
        });

        var foldersCommand = new Command("folders", "Manage monitored folders")
        {
            foldersListCommand,
            folderAddCommand
        };

        return new[] { foldersCommand };
    }  
}