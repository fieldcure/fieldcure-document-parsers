using System.Globalization;
using System.Text;
using FieldCure.DocumentParsers.Audio.Transcription;

namespace FieldCure.DocumentParsers.Audio.Formatting;

/// <summary>
/// Converts transcript segments to Markdown.
/// </summary>
public static class MarkdownFormatter
{
    /// <summary>
    /// Formats transcript segments using the supplied output options.
    /// </summary>
    /// <param name="segments">Transcript segments to render.</param>
    /// <param name="options">Formatting options.</param>
    /// <returns>Markdown transcript.</returns>
    public static string Format(IEnumerable<TranscriptSegment> segments, AudioExtractionOptions options)
    {
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(options);

        var segmentList = segments
            .Where(s => !string.IsNullOrWhiteSpace(s.Text))
            .OrderBy(s => s.Start)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("# Audio Transcript");

        if (options.IncludeMetadata)
        {
            sb.AppendLine();
            sb.AppendLine("| Property | Value |");
            sb.AppendLine("| --- | --- |");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"| Duration | {FormatTimestamp(GetDuration(segmentList))} |");
            sb.AppendLine($"| Language | {GetLanguage(segmentList, options)} |");
            sb.AppendLine($"| Model | {GetModelName(options)} |");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"| Segments | {segmentList.Count} |");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        foreach (var segment in segmentList)
        {
            if (options.IncludeTimestamps)
            {
                sb.Append('[')
                    .Append(FormatTimestamp(segment.Start))
                    .Append("] ");
            }

            sb.Append(segment.Text.Trim());

            if (options.IncludeConfidence && segment.Probability > 0f)
            {
                sb.Append(CultureInfo.InvariantCulture,
                    $" (confidence: {segment.Probability:0.00})");
            }

            sb.AppendLine();
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Returns transcript duration based on the latest segment end time.
    /// </summary>
    /// <param name="segments">Ordered or unordered transcript segments.</param>
    /// <returns>Transcript duration.</returns>
    private static TimeSpan GetDuration(IReadOnlyList<TranscriptSegment> segments)
        => segments.Count == 0 ? TimeSpan.Zero : segments.Max(s => s.End);

    /// <summary>
    /// Selects the best language label for the metadata table.
    /// </summary>
    /// <param name="segments">Transcript segments that may carry detected language.</param>
    /// <param name="options">Formatting options that may specify a language hint.</param>
    /// <returns>Language label.</returns>
    private static string GetLanguage(
        IReadOnlyList<TranscriptSegment> segments,
        AudioExtractionOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Language))
        {
            return options.Language!;
        }

        return segments
            .Select(s => s.Language)
            .FirstOrDefault(language => !string.IsNullOrWhiteSpace(language))
            ?? "auto";
    }

    /// <summary>
    /// Returns the model label to include in transcript metadata.
    /// </summary>
    /// <param name="options">Audio extraction options.</param>
    /// <returns>Model display name.</returns>
    private static string GetModelName(AudioExtractionOptions options)
        => !string.IsNullOrWhiteSpace(options.ModelPath)
            ? Path.GetFileName(options.ModelPath)
            : options.ModelSize.ToString().ToLowerInvariant();

    /// <summary>
    /// Formats a timestamp as HH:MM:SS with hours allowed to exceed 24.
    /// </summary>
    /// <param name="timestamp">Timestamp to format.</param>
    /// <returns>Formatted timestamp.</returns>
    private static string FormatTimestamp(TimeSpan timestamp)
    {
        var totalHours = (int)Math.Floor(timestamp.TotalHours);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{totalHours:00}:{timestamp.Minutes:00}:{timestamp.Seconds:00}");
    }
}
