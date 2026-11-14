using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace JiraCli.Domain;

/// <summary>A Jira project key such as <c>ABC</c>, canonicalized to upper case with invariant rules.</summary>
public sealed partial record ProjectKey
{
    private ProjectKey(string value) => Value = value;

    public string Value { get; }

    /// <summary>
    /// Accepts an ASCII letter followed by one or more ASCII letters, digits, or underscores, in any
    /// case, and upper-cases it. Non-ASCII look-alikes (such as the Kelvin sign) are refused before
    /// case mapping, so they cannot turn into ASCII letters.
    /// </summary>
    public static bool TryParse(string? text, [NotNullWhen(true)] out ProjectKey? key)
    {
        key = null;
        if (text is null || !AnyCaseKey().IsMatch(text))
        {
            return false;
        }

        var canonical = text.ToUpperInvariant();
        if (!CanonicalKey().IsMatch(canonical))
        {
            return false;
        }

        key = new ProjectKey(canonical);
        return true;
    }

    public static ProjectKey Parse(string text) =>
        TryParse(text, out var key) ? key : throw new FormatException("The text is not a valid project key.");

    public override string ToString() => Value;

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex AnyCaseKey();

    [GeneratedRegex(@"^[A-Z][A-Z0-9_]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalKey();
}
