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

/// <summary>
/// The only reader of the schema object: validates it and maps it to <see cref="ConnectionSettings"/>
/// and <see cref="AccessSettings"/>. Every failure names the key and the file; no message or hint
/// ever includes a value read from the file. The first problem found is reported.
/// </summary>
internal static class ConfigMapper
{
    public static ConfigLoadResult Map(ConfigFileSchema schema, string path)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (string.IsNullOrWhiteSpace(schema.Site))
        {
            return Missing(ConfigKeys.Site, path, "your Atlassian site name, the part before .atlassian.net (for example \"your-company\")");
        }

        if (!ConnectionSettings.IsValidSite(schema.Site))
        {
            return Invalid(
                ConfigKeys.Site,
                path,
                "is not a single DNS label",
                "Set \"site\" to the site name only, the part before .atlassian.net (for example \"your-company\"), without a scheme, dots, or a path.");
        }

        if (string.IsNullOrWhiteSpace(schema.Email))
        {
            return Missing(ConfigKeys.Email, path, "the email address of the Atlassian account that owns the API token");
        }

        if (HasSurroundingWhitespace(schema.Email))
        {
            return SurroundingWhitespace(ConfigKeys.Email, path);
        }

        if (string.IsNullOrWhiteSpace(schema.ApiToken))
        {
            return Missing(ConfigKeys.ApiToken, path, "an Atlassian API token created at id.atlassian.com for that account");
        }

        // A pasted token often carries a trailing space or newline; it would only fail later as a 401.
        if (HasSurroundingWhitespace(schema.ApiToken))
        {
            return SurroundingWhitespace(ConfigKeys.ApiToken, path);
        }

        Guid? cloudId = null;
        if (schema.CloudId is not null)
        {
            if (!Guid.TryParseExact(schema.CloudId, "D", out var parsed) || parsed == Guid.Empty)
            {
                return Invalid(
                    ConfigKeys.CloudId,
                    path,
                    "is not a GUID",
                    "Set \"cloudId\" to your site's cloud id in the form xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx, or remove it when you use a classic API token.");
            }

            cloudId = parsed;
        }

        if (schema.JiraCli is not { } section)
        {
            return Missing(ConfigKeys.Section, path, "a section listing at least \"allowedProjects\"");
        }

        if (section.UnknownKeys is { Count: > 0 })
        {
            // The key name is text from the file and may be a pasted secret, so it is never shown.
            return Invalid(
                $"{ConfigKeys.Section}.{ConfigKeys.UnknownKey}",
                path,
                "is not a known setting",
                "Remove it or fix its spelling; its name is not shown because a key name may be a pasted secret. The \"jira-cli\" section accepts allowedProjects, customFieldPassthrough, defaultPageSize, and maxPageSize.");
        }

        if (section.AllowedProjects is null)
        {
            return Missing(ConfigKeys.AllowedProjects, path, "the project keys jira-cli may read (for example [\"ABC\"])");
        }

        if (section.AllowedProjects.Count == 0)
        {
            return Invalid(
                ConfigKeys.AllowedProjects,
                path,
                "is empty",
                "List at least one project key jira-cli may read (for example [\"ABC\"]).");
        }

        var projects = new List<ProjectKey>(section.AllowedProjects.Count);
        for (var index = 0; index < section.AllowedProjects.Count; index++)
        {
            if (!ProjectKey.TryParse(section.AllowedProjects[index], out var project))
            {
                return Invalid(
                    ConfigKeys.Item(ConfigKeys.AllowedProjects, index),
                    path,
                    "is not a project key",
                    "Use a project key: a letter followed by letters, digits, or underscores, at least two characters (for example \"ABC\").");
            }

            projects.Add(project);
        }

        var passthrough = section.CustomFieldPassthrough ?? [];
        for (var index = 0; index < passthrough.Count; index++)
        {
            if (!AccessSettings.IsValidCustomFieldId(passthrough[index]))
            {
                return Invalid(
                    ConfigKeys.Item(ConfigKeys.CustomFieldPassthrough, index),
                    path,
                    "is not a custom field id",
                    "Use the field id in the form customfield_<digits> (for example \"customfield_10010\").");
            }
        }

        var maxPageSize = section.MaxPageSize ?? AccessSettings.DefaultMaxPageSize;
        if (maxPageSize < 1)
        {
            return Invalid(ConfigKeys.MaxPageSize, path, "is less than 1", $"Set \"maxPageSize\" to 1 or more, or remove it to use {AccessSettings.DefaultMaxPageSize.ToString(CultureInfo.InvariantCulture)}.");
        }

        var defaultPageSize = section.DefaultPageSize ?? AccessSettings.DefaultDefaultPageSize;
        if (defaultPageSize < 1)
        {
            return Invalid(ConfigKeys.DefaultPageSize, path, "is less than 1", $"Set \"defaultPageSize\" to 1 or more, or remove it to use {AccessSettings.DefaultDefaultPageSize.ToString(CultureInfo.InvariantCulture)}.");
        }

        if (defaultPageSize > maxPageSize)
        {
            return Invalid(
                ConfigKeys.DefaultPageSize,
                path,
                "is greater than jira-cli.maxPageSize",
                $"Set \"defaultPageSize\" no higher than \"maxPageSize\" (the defaults are {AccessSettings.DefaultDefaultPageSize.ToString(CultureInfo.InvariantCulture)} and {AccessSettings.DefaultMaxPageSize.ToString(CultureInfo.InvariantCulture)}).");
        }

        var connection = new ConnectionSettings(schema.Site, schema.Email, schema.ApiToken, cloudId);
        var access = new AccessSettings(projects, passthrough.OfType<string>(), defaultPageSize, maxPageSize);
        return ConfigLoadResult.Success(connection, access);
    }

    /// <summary>Turns a file-level failure from <see cref="ConfigFileLoader"/> into a key-named failure.</summary>
    public static ConfigLoadResult FromFileFailure<T>(ConfigFileLoadResult<T> result)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(result);
        var path = result.Path;

        var failure = result.Failure switch
        {
            ConfigFileFailureKind.PathUnavailable => new ConfigLoadFailure(
                ConfigFileFailureKind.PathUnavailable,
                null,
                $"The config file location could not be determined: {result.Problem}.",
                "jira-cli reads its config only from the current OS user's folder (RoamingAppData on Windows, the passwd home directory on Linux); environment variables cannot change it. Run jira-cli as a user with a profile or home directory.",
                null),
            ConfigFileFailureKind.Missing => new ConfigLoadFailure(
                ConfigFileFailureKind.Missing,
                null,
                $"No config file at {path}.",
                $"Create {path} with \"site\", \"email\", \"apiToken\", and a \"jira-cli\" section listing \"allowedProjects\".",
                path),
            ConfigFileFailureKind.Unreadable => new ConfigLoadFailure(
                ConfigFileFailureKind.Unreadable,
                null,
                $"The config file at {path} could not be read.",
                $"Check that {path} is a file the current user can read and that no other program has it locked.",
                path),
            ConfigFileFailureKind.Invalid => InvalidJson(result.KeyPath, result.Line, path),
            _ => throw new ArgumentException("The result is not a failure.", nameof(result)),
        };

        return ConfigLoadResult.Failed(failure);
    }

    private static ConfigLoadFailure InvalidJson(string? rawKey, int? line, string? path)
    {
        var key = ConfigKeys.MaskUnknownNames(rawKey);
        var where = (key, line) switch
        {
            ({ } k, { } l) => $" at \"{k}\" (line {l.ToString(CultureInfo.InvariantCulture)})",
            ({ } k, null) => $" at \"{k}\"",
            (null, { } l) => $" at line {l.ToString(CultureInfo.InvariantCulture)}",
            _ => string.Empty,
        };

        return new ConfigLoadFailure(
            ConfigFileFailureKind.Invalid,
            key,
            $"The config file at {path} is not valid{where}: broken JSON syntax, a duplicate key, or a value of the wrong type.",
            $"Fix {path}{where}. The file must be one JSON object; comments and trailing commas are allowed, duplicate keys are not, page sizes are whole numbers, and the other settings are strings.",
            path);
    }

    private static ConfigLoadResult Missing(string key, string path, string expected) =>
        ConfigLoadResult.Failed(new ConfigLoadFailure(
            ConfigFileFailureKind.Invalid,
            key,
            $"The config file at {path} has no value for \"{key}\".",
            $"Set \"{key}\" in {path}: {expected}.",
            path));

    private static bool HasSurroundingWhitespace(string value) =>
        char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]);

    private static ConfigLoadResult SurroundingWhitespace(string key, string path) =>
        Invalid(
            key,
            path,
            "starts or ends with whitespace",
            $"Remove the spaces, tabs, or line breaks before and after the \"{key}\" value; it is used exactly as written.");

    private static ConfigLoadResult Invalid(string key, string path, string problem, string hint) =>
        ConfigLoadResult.Failed(new ConfigLoadFailure(
            ConfigFileFailureKind.Invalid,
            key,
            $"The config file at {path} has an invalid \"{key}\": it {problem}.",
            $"{hint} (in {path})",
            path));
}
