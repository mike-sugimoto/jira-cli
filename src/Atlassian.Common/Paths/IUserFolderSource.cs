namespace Atlassian.Common.Paths;

/// <summary>The operating systems whose per-user folders can be resolved.</summary>
internal enum UserPlatform
{
    Unsupported,
    Windows,
    Linux,
}

/// <summary>
/// The native lookup seam behind <see cref="UserPaths"/>. The production implementation asks the
/// OS for the folder of the user the process runs as; tests substitute failures and odd answers.
/// No implementation may read an environment variable.
/// </summary>
internal interface IUserFolderSource
{
    UserPlatform Platform { get; }

    /// <summary>Windows only: <c>FOLDERID_RoamingAppData</c> for the process token's user.</summary>
    UserPathLookup RoamingAppData();

    /// <summary>Linux only: the home directory from the passwd entry of the effective user id.</summary>
    UserPathLookup PasswdHome();
}
