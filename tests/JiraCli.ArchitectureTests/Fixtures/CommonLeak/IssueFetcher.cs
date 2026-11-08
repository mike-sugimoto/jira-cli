using System.ComponentModel;

namespace JiraCli.ArchitectureTests.Fixtures.CommonLeak;

/// <summary>
/// Self-test fixture for <see cref="CommonIsJiraFreeTests"/>: each member carries a forbidden
/// token in a different place (name, literal, constant, default value, attribute argument,
/// parameter name, generic parameter name). Never used at run time.
/// </summary>
internal sealed class IssueFetcher
{
    public const string SelfPath = "/rest/api/3/myself";

    public string CustomFieldId = string.Empty;

    public static string BuildJql() => string.Empty;

    public static string Path() => "/REST/API/3/search";

    public static string Find(string query = "order by JQL") => query;

    [Description("Jira helper")]
    public static void Describe()
    {
    }

    public static string Load(string issueKey) => issueKey;

    public static Type Map<TIssue>() => typeof(TIssue);
}
