using System.Text.Json.Serialization;

public class TodoCliConfig
{
    [JsonPropertyName("global")]
    public GlobalConfig Global { get; set; } = new();

    [JsonPropertyName("folders")]
    public List<MonitoredFolder> Folders { get; set; } = new();
}

public class GlobalConfig
{
    [JsonPropertyName("fileNamePatterns")]
    public List<string> FileNamePatterns { get; set; } = new() { "*.txt", "*.md", "*.todo" };

    [JsonPropertyName("todoPrefixes")]
    public List<string> todoPrefixes { get; set; } = new() { "- [ ]", "[ ]", "//TODO", "TODO" };

    [JsonPropertyName("dueDatePattern")]
    public string DueDatePattern { get; set; } = "{due: (\\d{4}[-/]\\d{2}[-/]\\d{2})}";

    [JsonPropertyName("tagPattern")]
    public string TagPattern { get; set; } = "#\\w+";

    [JsonPropertyName("projectPattern")]
    public string ProjectPattern { get; set; } = "@\\w+";

    [JsonPropertyName("priorityPattern")]
    public string PriorityPattern { get; set; } = "{priority:([A-Z])}";

    [JsonPropertyName("defaultTodoFilename")]
    public string DefaultTodoFilename { get; set; } = "todo.md";

    [JsonPropertyName("ignoreHiddenFolders")]
    public bool IgnoreHiddenFolders { get; set; } = true;
}

public class MonitoredFolder : GlobalConfig
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("friendlyName")]
    public string FriendlyName { get; set; } = string.Empty;
}
