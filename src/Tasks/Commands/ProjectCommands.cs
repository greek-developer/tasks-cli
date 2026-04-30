using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Runtime.CompilerServices;

using Tasks.Config;
using Tasks.Todo;

namespace Tasks.Commands;

public static class ProjectCommands
{
    public static IEnumerable<Command> ProjectTagCommands()
    {
        var projectListCommand = new Command("list", "list all projects");
        projectListCommand.SetAction(_ =>
        {
            Console.WriteLine("");
            Console.WriteLine("Projects:");
            Console.WriteLine("");

            Console.WriteLine(
                string.Join(
                    Environment.NewLine,
                    TodoManager.Todos
                        .SelectMany(t => t.Projects)
                        .Distinct()
                        .OrderBy(t => t)
                    )

            );
            
            Console.WriteLine("");
        });        

        return new[]
        {
            new Command("project", "Manage Projects")
            {
                projectListCommand,
            }
        };
    }
}