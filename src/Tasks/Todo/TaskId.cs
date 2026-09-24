using System.Security.Cryptography;
using System.Text;

namespace Tasks.Todo;

/// <summary>
/// The id written onto a line in a managed tasks file: eight characters hashed from the task's
/// text, shown as <c>abcd-efgh</c>. It is computed once, when the task is added, and stays put
/// when the text is later edited - the id names the task, not its current wording.
/// </summary>
internal static class TaskId
{
    /// <summary>Crockford's base32, lower-cased: no i, l, o or u to misread.</summary>
    private const string Alphabet = "0123456789abcdefghjkmnpqrstvwxyz";

    internal const int MinimumPrefix = 4;

    /// <summary>
    /// Hashes the text into an id not already in <paramref name="taken"/>. Identical text
    /// would hash identically, so a collision is resolved by salting and hashing again.
    /// </summary>
    internal static string Create(string text, ISet<string> taken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var source = attempt == 0 ? text.Trim() : $"{text.Trim()}\n{attempt}";
            var id = Format(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
            if (!taken.Contains(id))
            {
                return id;
            }
        }
    }

    /// <summary>
    /// What a person typed, reduced to the comparable form: lower-case, dash removed. Null when
    /// it cannot be an id or a prefix of one.
    /// </summary>
    internal static string? NormalizeReference(string raw)
    {
        var value = raw.Trim().ToLowerInvariant().Replace("-", string.Empty, StringComparison.Ordinal);
        return value.Length >= MinimumPrefix && value.Length <= 8 && value.All(Alphabet.Contains)
            ? value
            : null;
    }

    /// <summary>True when the id starts with the normalized reference.</summary>
    internal static bool Matches(string id, string normalizedReference) =>
        id.Replace("-", string.Empty, StringComparison.Ordinal)
            .StartsWith(normalizedReference, StringComparison.Ordinal);

    private static string Format(byte[] hash)
    {
        // 40 bits make eight base32 characters.
        ulong bits = 0;
        for (var i = 0; i < 5; i++)
        {
            bits = (bits << 8) | hash[i];
        }

        var chars = new char[8];
        for (var i = 7; i >= 0; i--)
        {
            chars[i] = Alphabet[(int)(bits & 31)];
            bits >>= 5;
        }

        return $"{new string(chars, 0, 4)}-{new string(chars, 4, 4)}";
    }
}
