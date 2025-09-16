
namespace todocli.Todo;

public class Todo
{
    public string Description { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? Priority { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<string> Projects { get; set; } = new();
}