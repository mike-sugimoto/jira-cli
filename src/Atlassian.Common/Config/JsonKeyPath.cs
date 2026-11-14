using System.Globalization;
using System.Text;

namespace Atlassian.Common.Config;

/// <summary>
/// Turns a <see cref="System.Text.Json.JsonException.Path"/> such as <c>$['my-app'].items[0]</c>
/// into the key path people write, such as <c>my-app.items[0]</c>.
/// </summary>
internal static class JsonKeyPath
{
    public static string? Readable(string? jsonPath)
    {
        if (string.IsNullOrEmpty(jsonPath) || jsonPath == "$")
        {
            return null;
        }

        if (!jsonPath.StartsWith('$'))
        {
            return null;
        }

        var builder = new StringBuilder();
        var index = 1;
        while (index < jsonPath.Length)
        {
            if (jsonPath[index] == '.')
            {
                var end = index + 1;
                while (end < jsonPath.Length && jsonPath[end] is not ('.' or '['))
                {
                    end++;
                }

                AppendName(builder, jsonPath[(index + 1)..end]);
                index = end;
            }
            else if (jsonPath.AsSpan(index).StartsWith("['"))
            {
                var end = jsonPath.IndexOf("']", index + 2, StringComparison.Ordinal);
                if (end < 0)
                {
                    return null;
                }

                AppendName(builder, jsonPath[(index + 2)..end]);
                index = end + 2;
            }
            else if (jsonPath[index] == '[')
            {
                var end = jsonPath.IndexOf(']', index);
                if (end < 0 || !int.TryParse(jsonPath.AsSpan(index + 1, end - index - 1), NumberStyles.None, CultureInfo.InvariantCulture, out _))
                {
                    return null;
                }

                builder.Append(jsonPath, index, end - index + 1);
                index = end + 1;
            }
            else
            {
                return null;
            }
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    private static void AppendName(StringBuilder builder, string name)
    {
        if (builder.Length > 0)
        {
            builder.Append('.');
        }

        builder.Append(name);
    }
}
