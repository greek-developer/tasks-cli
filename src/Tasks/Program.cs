
using System.CommandLine;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Runtime.CompilerServices;

using Tasks.Commands;
using Tasks.Config;
using Tasks.Skills;
using Tasks.Versioning;

namespace Tasks
{
    public class Program
    {
        public static Task<int> Main(string[] args)
        {
            UseUtf8Output();

            var rootCommand = new RootCommand("tasks: text-based todo management CLI tool");

            var getConfigPathCommand = new Command(
                "get-config-path",
                "Displays the full path to the config file (in user's profile)");

            getConfigPathCommand.SetAction(_ => Console.WriteLine(ConfigurationManager.GetConfigPath()));

            rootCommand.Add(getConfigPathCommand);
            rootCommand.Add(VersionCommand.Create());
            rootCommand.Add(SkillCommand.Create());

            AddCommands(rootCommand, FolderCommands.GenerateFolderCommands());
            AddCommands(rootCommand, TodoCommands.GenerateTodoCommands());
            AddCommands(rootCommand, TagCommands.GenerateTagCommands());
            AddCommands(rootCommand, ProjectCommands.ProjectTagCommands());
            AddCommands(rootCommand, GTDCommands.GenerateGTDCommands());

            return rootCommand
                .Parse(args)
                .InvokeAsync();
        }

        private static void AddCommands(RootCommand rootCommand, IEnumerable<Command> commands)
        {
            foreach (var command in commands)
            {
                rootCommand.Add(command);
            }
        }

        /// <summary>
        /// Windows consoles default to a legacy code page, which silently transliterates anything
        /// outside it - em dashes and arrows survive on screen but not through a redirect. The
        /// embedded skill file carries such characters and `tasks skill > SKILL.md` has to
        /// reproduce it byte for byte, so the output encoding is pinned before anything is written.
        /// </summary>
        private static void UseUtf8Output()
        {
            try
            {
                Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            }
            catch (IOException)
            {
                // Some redirected handles refuse the change; plain ASCII output still works.
            }
        }
    }  
}
