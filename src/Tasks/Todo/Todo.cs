
namespace Tasks.Todo;

public class Todo
{
    public string Description { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? Priority { get; set; }

    /// <summary>The <c>{id: abcd-efgh}</c> a managed tasks file carries; null elsewhere.</summary>
    public string? Id { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<string> Projects { get; set; } = new();
}
