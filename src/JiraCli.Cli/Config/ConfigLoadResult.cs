using Atlassian.Common.Config;
using JiraCli.Domain;

namespace JiraCli.Cli.Config;

/// <summary>
/// Why the config could not be used. <see cref="Message"/> and <see cref="Hint"/> name the cause
/// or key and the file path, never a value from the file.
/// </summary>
internal sealed record ConfigLoadFailure(
    ConfigFileFailureKind Kind,
    string? Key,
    string Message,
    string Hint,
    string? Path);

/// <summary>
/// The config load outcome, loaded once per invocation and registered as a singleton. A failure is
/// a value: Story 1.4 turns it into the <c>config</c> category and <c>doctor</c> reports it as a check.
/// </summary>
internal sealed class ConfigLoadResult
{
    private ConfigLoadResult(ConnectionSettings? connection, AccessSettings? access, ConfigLoadFailure? failure)
    {
        Connection = connection;
        Access = access;
        Failure = failure;
    }

    public bool IsSuccess => Failure is null;

    public ConnectionSettings? Connection { get; }

    public AccessSettings? Access { get; }

    public ConfigLoadFailure? Failure { get; }

    public static ConfigLoadResult Success(ConnectionSettings connection, AccessSettings access)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(access);
        return new ConfigLoadResult(connection, access, null);
    }

    public static ConfigLoadResult Failed(ConfigLoadFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new ConfigLoadResult(null, null, failure);
    }

    public override string ToString() =>
        Failure is null ? $"{nameof(ConfigLoadResult)} {{ success }}" : $"{nameof(ConfigLoadResult)} {{ {Failure.Kind}: {Failure.Message} }}";
}
