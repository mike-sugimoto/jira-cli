namespace Atlassian.Common.Paths;

/// <summary>
/// The outcome of resolving a per-user path from OS user identity: either an absolute path or
/// a description of why the lookup failed. A failure is a value, never an exception, and never
/// falls back to another location (AD-11).
/// </summary>
public sealed class UserPathLookup
{
    private UserPathLookup(string? path, string? problem)
    {
        Path = path;
        Problem = problem;
    }

    /// <summary>The resolved absolute path, or <see langword="null"/> when the lookup failed.</summary>
    public string? Path { get; }

    /// <summary>Why the lookup failed, or <see langword="null"/> when it succeeded.</summary>
    public string? Problem { get; }

    public bool IsFound => Path is not null;

    public static UserPathLookup FromPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new UserPathLookup(path, null);
    }

    public static UserPathLookup Unavailable(string problem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(problem);
        return new UserPathLookup(null, problem);
    }

    public override string ToString() => Path ?? $"unavailable: {Problem}";
}
