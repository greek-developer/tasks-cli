
using Tasks.Config;

namespace Tasks.Todo;


public static class TodoManager
{
    // This class is intentionally left empty for now.   
    // Future methods for managing todos can be added here.
    public static List<Todo> Todos { get; private set; }

    static TodoManager()
    {
        Todos = LoadTodos();
    }    

    private static List<Todo> LoadTodos()
    {
        var config = ConfigurationManager.Config;
        var todos = new List<Todo>();

        foreach (var folderConfiguration in config.Folders)
        {
            if (Directory.Exists(folderConfiguration.Path))
            {
                foreach (var fileNamePattern in folderConfiguration.FileNamePatterns)
                {
                    var todoFiles = Directory.GetFiles(folderConfiguration.Path, fileNamePattern, SearchOption.AllDirectories);

                    foreach (var file in todoFiles)
                    {
                        var lines = File.ReadAllLines(file);
                        for (int i = 0; i < lines.Length; i++)
                        {
                            var line = lines[i].TrimStart();

                            if (folderConfiguration.todoPrefixes.Any(line.StartsWith))
                            {
                                todos.Add(new Todo
                                {
                                    Description = line,
                                    FilePath = file,
                                    LineNumber = i + 1,
                                    DueDate = ExtractDueDate(line, folderConfiguration.DueDatePattern),
                                    Tags = ExtractTags(line, folderConfiguration.TagPattern),
                                    Projects = ExtractProjects(line,folderConfiguration.ProjectPattern),
                                    Priority = null // Priority extraction can be implemented similarly
                                });
                            }
                        }
                    }
                }

                return todos;
            }
        }

        return new List<Todo>();
    }

    private static DateOnly? ExtractDueDate(string line, string dueDatePattern)
    {
        var regex = new System.Text.RegularExpressions.Regex(dueDatePattern);
        var match = regex.Match(line);

        if (match.Success && DateOnly.TryParse(match.Groups[1].Value, out var dueDate))
        {
            return dueDate;
        }

        return null;
    }

    private static List<string> ExtractTags(string line, string tagPattern)
    {
        var regex = new System.Text.RegularExpressions.Regex(tagPattern);
        var matches = regex.Matches(line);

        return matches
            .Select(m => m.Groups[0].Value.TrimStart('#'))
            .ToList();
    } 

    private static List<string> ExtractProjects(string line, string projectPattern)
    {
        var regex = new System.Text.RegularExpressions.Regex(projectPattern);
        var matches = regex.Matches(line);

        return matches.Select(m => m.Groups[0].Value).ToList();
    } 
}
