using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Runtime.CompilerServices;

using todocli.Config;
using todocli.Todo;

namespace todocli.Commands;

public static class TagCommands
{
    public static IEnumerable<Command> GenerateTagCommands()
    {
        var tagListCommand = new Command("list", "list all tags");
        tagListCommand.SetAction(_ =>
        {
            Console.WriteLine("");
            Console.WriteLine("Tags:");
            Console.WriteLine("");

            Console.WriteLine(
                string.Join(
                    Environment.NewLine,
                    TodoManager.Todos
                        .SelectMany(t => t.Tags)
                        .Distinct()
                        .OrderBy(t => t)
                    )

            );

            Console.WriteLine("");
        });        

        return new[]
        {
            new Command("tag", "Manage Tags")
            {
                tagListCommand,
            }
        };
    }
}