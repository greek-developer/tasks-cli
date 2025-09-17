using System.CommandLine;
using todocli.Todo;

namespace todocli.Commands;

public static class GTDCommands
{
    private static readonly string[] _gtdTags = ["backlog", "next", "review"];

    public static IEnumerable<Command> GenerateGTDCommands()
    {
        var gtdInboxCommand = new Command("inbox", "Show GTD Inbox tasks");
        gtdInboxCommand.SetAction(_ => RenderTasks("Inbox", tags => !tags.Intersect(_gtdTags).Any(), 365, -1 ));

        var gtdNextCommand = new Command("next", "Show GTD Next tasks");
        gtdNextCommand.SetAction(_ => RenderTasks("Next", tags => tags.Contains("next"), 30, -1));

        var gtdReviewCommand = new Command("review", "Show GTD Review tasks");
        gtdReviewCommand.SetAction(_ => RenderTasks("Review", tags => tags.Contains("review"), 30, -1));

        var gtdBacklogCommand = new Command("backlog", "Show GTD Backlog tasks");
        gtdBacklogCommand.SetAction(_ => RenderTasks("Backlog", tags => tags.Contains("backlog"), 365, -1));

        var gtdCommand = new Command("gtd", "Manage GTD")
        {
            gtdInboxCommand,
            gtdNextCommand,
            gtdReviewCommand,
            gtdBacklogCommand
        };

        gtdCommand.SetAction(parseResult =>
        {
            RenderTasks("Next", tags => tags.Contains("next"), 3, 10);

            RenderTasks("Review", tags => tags.Contains("review"), 7, 10);

            RenderTasks("Inbox", tags => !tags.Intersect(_gtdTags).Any(), 30, 5);

            RenderTasks("Backlog", tags => tags.Contains("backlog"), 365, 5);      
           
            Console.WriteLine("");
        });

        return new[]
        {
           gtdCommand
        };
    }
  
    private static void RenderTasks(string title, Predicate<List<string>> tagsPredicate, int daysAhead = 30, int limit = 10)
    {
        var cutoffDate = DateOnly.FromDateTime(DateTime.Now.AddDays(daysAhead));
        var today = DateOnly.FromDateTime(DateTime.Now);

        var todos = TodoManager.Todos
            .Where(t => tagsPredicate(t.Tags))
            .Where(t => t.DueDate == null || t.DueDate <= cutoffDate)
            .OrderBy(x =>
            {
                if (x.DueDate < today) return 0;  // before today
                if (x.DueDate == today) return 1; // today
                if (x.DueDate == null) return 2;  // after today, before tomorrow                               
                return 3; // after today
            })
            .ThenBy(x => x.DueDate)
            .Select(t => $"{t.Description} (-> {t.FilePath}:{t.LineNumber})")
            .ToList();

        Console.WriteLine($"GTD: {title} ({todos.Count} tasks)");

        if (limit > 0 && todos.Count > limit)
        {
           Console.WriteLine($"Showing {limit} tasks. Use <todo-cli gtd {title.ToLower()}> to see all tasks in {title}.");
        }      

        Console.WriteLine("");
        Console.WriteLine(string.Join(Environment.NewLine, limit > 0 ? todos.Take(limit): todos));
        Console.WriteLine("");
    }    
    
}