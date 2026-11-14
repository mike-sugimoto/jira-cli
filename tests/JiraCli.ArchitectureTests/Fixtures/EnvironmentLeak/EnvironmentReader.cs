namespace JiraCli.ArchitectureTests.Fixtures.EnvironmentLeak;

/// <summary>
/// Self-test fixture for <see cref="EnvironmentUseTests"/>: one call per banned member, the folder
/// lookup inside an async lambda so the compiler moves it into a nested state machine. Never used at run time.
/// </summary>
internal static class EnvironmentReader
{
    public static string? Home() => Environment.GetEnvironmentVariable("HOME");

    public static int Count() => Environment.GetEnvironmentVariables().Count;

    public static string Expand() => Environment.ExpandEnvironmentVariables("%APPDATA%");

    public static Func<Task<string>> Folder() => async () =>
    {
        await Task.Yield();
        return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    };
}
