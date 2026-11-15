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
