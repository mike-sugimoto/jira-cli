namespace Atlassian.Common.Paths;

/// <summary>
/// Resolves fixed per-user file locations from OS user identity only (AD-11). Environment
/// variables such as <c>HOME</c>, <c>XDG_*</c>, <c>USERPROFILE</c>, or <c>APPDATA</c> are never
/// consulted, and a failed lookup is reported as a value with no fallback location.
/// </summary>
public static class UserPaths
{
    private const string ConfigFileName = "config.json";

    /// <summary>
    /// The application's config file: <c>&lt;RoamingAppData&gt;\&lt;app&gt;\config.json</c> on Windows,
    /// <c>&lt;passwd home&gt;/.config/&lt;app&gt;/config.json</c> on Linux. Other platforms are unsupported.
    /// </summary>
    public static UserPathLookup ConfigFile(string appName) => ConfigFile(appName, NativeUserFolderSource.Instance);

    internal static UserPathLookup ConfigFile(string appName, IUserFolderSource source)
    {
        ValidateAppName(appName);
        ArgumentNullException.ThrowIfNull(source);

        return source.Platform switch
        {
            UserPlatform.Windows => Under(source.RoamingAppData(), WindowsPath, appName),
            UserPlatform.Linux => Under(source.PasswdHome(), LinuxPath, appName),
            _ => UserPathLookup.Unavailable("this operating system is not supported; only Windows and Linux are"),
        };
    }

    private static UserPathLookup Under(UserPathLookup root, Func<string, string, UserPathLookup> combine, string appName) =>
        root.Path is { } path ? combine(path, appName) : root;

    private static UserPathLookup WindowsPath(string roamingAppData, string appName)
    {
        if (!IsAbsoluteWindowsPath(roamingAppData))
        {
            return UserPathLookup.Unavailable("the roaming application data folder is not an absolute path");
        }

        return UserPathLookup.FromPath($"{roamingAppData.TrimEnd('\\', '/')}\\{appName}\\{ConfigFileName}");
    }

    private static UserPathLookup LinuxPath(string home, string appName)
    {
        if (!home.StartsWith('/'))
        {
            return UserPathLookup.Unavailable("the home directory in the passwd entry is not an absolute path");
        }

        return UserPathLookup.FromPath($"{home.TrimEnd('/')}/.config/{appName}/{ConfigFileName}");
    }

    private static bool IsAbsoluteWindowsPath(string path) =>
        (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] is '\\' or '/')
        || path.StartsWith(@"\\", StringComparison.Ordinal);

    private static void ValidateAppName(string appName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        if (appName is "." or ".." || appName.IndexOfAny(['/', '\\', ':']) >= 0 || appName.Trim() != appName)
        {
            throw new ArgumentException("The application name must be a single plain folder name.", nameof(appName));
        }
    }
}
