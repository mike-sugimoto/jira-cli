using System.Text;

namespace JiraCli.Cli;

/// <summary>
/// The plain-text stdout sink used only for <c>--help</c> and <c>--version</c> (AD-13).
/// Writes UTF-8 without BOM straight to the standard output stream, so the user's
/// console code page (<c>Console.OutputEncoding</c>) is never changed.
/// </summary>
internal static class PlainTextOutput
{
    public static TextWriter Create() =>
        new StreamWriter(
            Console.OpenStandardOutput(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 4096,
            leaveOpen: true);
}
