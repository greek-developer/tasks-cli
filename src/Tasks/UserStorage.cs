namespace Tasks;

/// <summary>
/// Everything this tool keeps on the machine lives in one hidden folder in the user's
/// profile, named after the package id - see AGENTS.md, "Where a tool stores things". One
/// place the user can find, inspect and delete.
/// </summary>
internal static class UserStorage
{
    /// <summary>`.` + the PackageId. Kept in step with the id in Tasks.csproj.</summary>
    internal const string FolderName = ".grdev.tasks-cli";

    internal static string Folder => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        FolderName);
}
