namespace Tasks;

/// <summary>
/// The outcome of validating input: either a value, or the reasons it was rejected.
/// Never both.
/// </summary>
internal sealed class Validated<T>
    where T : class
{
    private Validated(T? value, IReadOnlyList<string> errors)
    {
        Value = value;
        Errors = errors;
    }

    internal T? Value { get; }

    internal IReadOnlyList<string> Errors { get; }

    internal bool IsValid => Errors.Count == 0;

    internal static Validated<T> Success(T value) => new(value, []);

    internal static Validated<T> Failure(params string[] errors) => new(null, errors);

    internal static Validated<T> Failure(IReadOnlyList<string> errors) => new(null, errors);
}
