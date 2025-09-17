
using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Runtime.CompilerServices;

using Tasks.Commands;
using Tasks.Config;

namespace Tasks
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var rootCommand = new RootCommand("todo-cli: text-based todo management CLI tool");

            var getConfigPathCommand = new Command(
                "get-config-path",
                "Displays the full path to the config file (in user's profile)");

            getConfigPathCommand.SetAction(_ => Console.WriteLine(ConfigurationManager.GetConfigPath()));

            rootCommand.Add(getConfigPathCommand);

            AddCommands(rootCommand, FolderCommands.GenerateFolderCommands());
            AddCommands(rootCommand, TodoCommands.GenerateTodoCommands());
            AddCommands(rootCommand, TagCommands.GenerateTagCommands());
            AddCommands(rootCommand, ProjectCommands.ProjectTagCommands());
            AddCommands(rootCommand, GTDCommands.GenerateGTDCommands());

            ParseResult parseResult = rootCommand.Parse(args);
            return parseResult.Invoke();
        }

        private static void AddCommands(RootCommand rootCommand, IEnumerable<Command> commands)
        {
            foreach (var command in commands)
            {
                rootCommand.Add(command);
            }
        }
    }  
}
