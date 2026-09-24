using System.Text.Json.Serialization;

public class TasksConfig
{
    [JsonPropertyName("folders")]
    public List<MonitoredFolder> Folders { get; set; } = new();

    /// <summary>
    /// The friendly name of the managed folder a new task goes to when no folder is named.
    /// Unset is fine while exactly one folder is managed.
    /// </summary>
    [JsonPropertyName("defaultFolder")]
    public string? DefaultFolder { get; set; }
}


public class MonitoredFolder 
{  
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("friendlyName")]
    public string FriendlyName { get; set; } = string.Empty;

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
    public string PriorityPattern { get; set; } = "{pri: ([A-Z])}";

    [JsonPropertyName("defaultTodoFilename")]
    public string DefaultTodoFilename { get; set; } = "todo.md";

    [JsonPropertyName("ignoreHiddenFolders")]
    public bool IgnoreHiddenFolders { get; set; } = true;

    [JsonPropertyName("excludedFolders")]
    public List<string> ExcludedFolders { get; set; } = new() { "node_modules", ".git" };

    /// <summary>
    /// Present only on a folder the tool may write to: tasks are added to, edited in and
    /// completed in its tasks file, and every change is committed and pushed. Null leaves the
    /// folder read-only, as every folder was before managed folders existed.
    /// </summary>
    [JsonPropertyName("managed")]
    public ManagedSettings? Managed { get; set; }
}

public class ManagedSettings
{
    /// <summary>The tasks file, relative to the folder.</summary>
    [JsonPropertyName("tasksFile")]
    public string TasksFile { get; set; } = "tasks/tasks.md";

    [JsonPropertyName("openSection")]
    public string OpenSection { get; set; } = "## Open";

    [JsonPropertyName("doneSection")]
    public string DoneSection { get; set; } = "## Done";

    [JsonPropertyName("git")]
    public GitSettings Git { get; set; } = new();
}

public class GitSettings
{
    /// <summary>Pull (rebase, autostash) before every write, so the change lands on the latest file.</summary>
    [JsonPropertyName("pullBeforeWrite")]
    public bool PullBeforeWrite { get; set; } = true;

    [JsonPropertyName("push")]
    public bool Push { get; set; } = true;
}
