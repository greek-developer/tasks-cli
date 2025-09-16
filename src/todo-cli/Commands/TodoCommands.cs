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
        var todosListCommand = new Command("list", "list all todos");
        todosListCommand.SetAction(_ =>
        {
            Console.WriteLine("");
            Console.WriteLine("Todos:");
            Console.WriteLine("");

            Console.WriteLine(
                string.Join(
                    Environment.NewLine,
                    TodoManager.Todos
                        .Where(t => t.DueDate != null)
                        .OrderBy(t => t.DueDate)
                        .Select(t => $"{t.Description} (-> {t.FilePath}:{t.LineNumber})")
                )

            );
            Console.WriteLine("");

            Console.WriteLine(
                string.Join(
                    Environment.NewLine,
                    TodoManager.Todos
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

        var todosCommand = new Command("todos", "Manage todos")
        {
            todosListCommand,
            todoAddCommand
        };

        return new[] { todosCommand };
    }
}