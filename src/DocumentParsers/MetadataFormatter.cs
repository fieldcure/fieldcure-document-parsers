using System.Globalization;
using System.Text;

namespace FieldCure.DocumentParsers;

/// <summary>
/// Converts document metadata properties to a YAML front matter string.
/// </summary>
internal static class MetadataFormatter
{
    /// <summary>
    /// Generates a YAML front matter block from the given metadata values.
    /// Only non-null, non-empty values are included. Returns empty string if all values are null/empty.
    /// </summary>
    public static string FormatYamlFrontMatter(
        string? title = null,
        string? author = null,
        DateTime? created = null,
        DateTime? modified = null,
        string? subject = null,
        string? keywords = null,
        string? description = null)
    {
        var sb = new StringBuilder();

        AppendField(sb, "title", title);
        AppendField(sb, "author", author);
        if (created.HasValue)
            sb.AppendLine($"created: {created.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (modified.HasValue)
            sb.AppendLine($"modified: {modified.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        AppendField(sb, "subject", subject);
        AppendField(sb, "keywords", keywords);
        AppendField(sb, "description", description);

        if (sb.Length == 0) return "";

        return $"---\n{sb}---\n\n";
    }

    /// <summary>
    /// Appends a YAML key-value field to the builder if the value is non-empty.
    /// </summary>
    private static void AppendField(StringBuilder sb, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        sb.AppendLine($"{key}: {EscapeYamlValue(value)}");
    }

    /// <summary>
    /// Wraps the value in double quotes if it contains YAML special characters.
    /// </summary>
    private static string EscapeYamlValue(string value)
    {
        // Quote if value contains YAML special characters
        if (value.Contains(':') || value.Contains('#') || value.Contains('{') ||
            value.Contains('}') || value.Contains('[') || value.Contains(']') ||
            value.Contains(',') || value.Contains('&') || value.Contains('*') ||
            value.Contains('?') || value.Contains('|') || value.Contains('-') ||
            value.Contains('<') || value.Contains('>') || value.Contains('=') ||
            value.Contains('!') || value.Contains('%') || value.Contains('@') ||
            value.Contains('`') || value.Contains('"') || value.Contains('\'') ||
            value.StartsWith(' ') || value.EndsWith(' '))
        {
            // Use double quotes, escaping internal double quotes
            return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
        }

        return value;
    }
}
