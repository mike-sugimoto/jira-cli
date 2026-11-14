using System.Text.RegularExpressions;

namespace JiraCli.Domain;

/// <summary>
/// What jira-cli may read and how much per page, from the <c>jira-cli</c> config section.
/// Duplicates are removed (first occurrence wins); the guards here back up the config mapper,
/// which reports problems by key before this type is built.
/// </summary>
public sealed partial class AccessSettings
{
    public const int DefaultDefaultPageSize = 20;
    public const int DefaultMaxPageSize = 100;

    public AccessSettings(
        IEnumerable<ProjectKey> allowedProjects,
        IEnumerable<string> customFieldPassthrough,
        int defaultPageSize = DefaultDefaultPageSize,
        int maxPageSize = DefaultMaxPageSize)
    {
        ArgumentNullException.ThrowIfNull(allowedProjects);
        ArgumentNullException.ThrowIfNull(customFieldPassthrough);

        var projects = allowedProjects.Distinct().ToArray();
        if (projects.Length == 0 || projects.Any(project => project is null))
        {
            throw new ArgumentException("At least one allowed project is required, and none may be null.", nameof(allowedProjects));
        }

        var passthrough = customFieldPassthrough.Distinct(StringComparer.Ordinal).ToArray();
        if (!passthrough.All(IsValidCustomFieldId))
        {
            throw new ArgumentException("Every passthrough entry must be a custom field id.", nameof(customFieldPassthrough));
        }

        if (maxPageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPageSize), "The maximum page size must be at least 1.");
        }

        if (defaultPageSize < 1 || defaultPageSize > maxPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultPageSize), "The default page size must be between 1 and the maximum page size.");
        }

        AllowedProjects = projects;
        CustomFieldPassthrough = passthrough;
        DefaultPageSize = defaultPageSize;
        MaxPageSize = maxPageSize;
    }

    public IReadOnlyList<ProjectKey> AllowedProjects { get; }

    /// <summary>Custom field ids (<c>customfield_&lt;digits&gt;</c>) whose values pass through to output.</summary>
    public IReadOnlyList<string> CustomFieldPassthrough { get; }

    public int DefaultPageSize { get; }

    public int MaxPageSize { get; }

    public static bool IsValidCustomFieldId(string? id) => id is not null && CustomFieldId().IsMatch(id);

    [GeneratedRegex(@"^customfield_[0-9]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex CustomFieldId();
}
