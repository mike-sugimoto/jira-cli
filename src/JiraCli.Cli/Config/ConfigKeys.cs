using System.Globalization;
using System.Text.RegularExpressions;
using Atlassian.Common.Config;
using JiraCli.Domain;

namespace JiraCli.Cli.Config;

/// <summary>Config key names, as written in the file and in messages.</summary>
internal static partial class ConfigKeys
{
    public const string Section = "jira-cli";
    public const string Site = "site";
    public const string Email = "email";
    public const string ApiToken = "apiToken";
    public const string CloudId = "cloudId";
    public const string AllowedProjects = Section + ".allowedProjects";
    public const string CustomFieldPassthrough = Section + ".customFieldPassthrough";
    public const string DefaultPageSize = Section + ".defaultPageSize";
    public const string MaxPageSize = Section + ".maxPageSize";

    /// <summary>Shown in place of a key name jira-cli does not know: such a name may be a pasted secret.</summary>
    public const string UnknownKey = "<unknown key>";

    private static readonly HashSet<string> KnownNames = new(StringComparer.Ordinal)
    {
        Section, Site, Email, ApiToken, CloudId,
        "allowedProjects", "customFieldPassthrough", "defaultPageSize", "maxPageSize",
    };

    public static string Item(string key, int index) => $"{key}[{index.ToString(CultureInfo.InvariantCulture)}]";

    /// <summary>
    /// Keeps a key path such as <c>jira-cli.allowedProjects[0]</c> as long as every name in it is one
    /// jira-cli knows. From the first unknown name on, the rest is replaced by <see cref="UnknownKey"/>,
    /// so no text written in the file reaches a message.
    /// </summary>
    public static string? MaskUnknownNames(string? keyPath)
    {
        if (keyPath is null)
        {
            return null;
        }

        var kept = new List<string>();
        foreach (var segment in keyPath.Split('.'))
        {
            if (!KnownSegment().IsMatch(segment) || !KnownNames.Contains(segment[..IndexStart(segment)]))
            {
                kept.Add(UnknownKey);
                break;
            }

            kept.Add(segment);
        }

        return string.Join('.', kept);
    }

    private static int IndexStart(string segment) => segment.IndexOf('[') is var bracket and >= 0 ? bracket : segment.Length;

    [GeneratedRegex(@"^[A-Za-z-]+(?:\[[0-9]+\])*\z", RegexOptions.CultureInvariant)]
    private static partial Regex KnownSegment();
}
