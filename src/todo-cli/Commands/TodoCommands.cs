using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Runtime.CompilerServices;

using todocli.Config;
using todocli.Todo;

namespace todocli.Commands;

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

        var todoAddCommand = new Command("add", "add a todo")
        {
            new Argument<string>("description"),
            new Argument<string>("filePath"),
        };

        todoAddCommand.SetAction(parseResult =>
        {

        });

        return new[]
        {
            new Command("todo", "Manage Todos")
            {
                todoListCommand,
                todoAddCommand
            }
        };
    }
}