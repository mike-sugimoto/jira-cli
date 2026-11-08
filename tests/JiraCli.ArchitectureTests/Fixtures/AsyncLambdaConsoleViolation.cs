namespace JiraCli.ArchitectureTests.Fixtures;

/// <summary>
/// Self-test fixture for <see cref="ConsoleUseTests"/>: the console call sits inside an async
/// lambda, so the compiler moves it into a nested closure class and its state machine.
/// The rule must still report this outer type.
/// </summary>
internal static class AsyncLambdaConsoleViolation
{
    public static Func<Task> Create() => async () =>
    {
        await Task.Yield();
        Console.WriteLine("violation");
    };
}
