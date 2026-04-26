using FieldCure.DocumentParsers.Audio.Formatting;
using FieldCure.DocumentParsers.Audio.Transcription;

namespace FieldCure.DocumentParsers.Audio.Tests;

/// <summary>
/// Unit tests for <see cref="MarkdownFormatter"/>.
/// </summary>
[TestClass]
public class MarkdownFormatterTests
{
    /// <summary>
    /// Verifies that segment ordering follows timestamps, not input order.
    /// </summary>
    [TestMethod]
    public void Format_OrdersSegmentsByStartTime()
    {
        var markdown = MarkdownFormatter.Format(
            [
                new TranscriptSegment(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(12), "later"),
                new TranscriptSegment(TimeSpan.Zero, TimeSpan.FromSeconds(2), "first")
            ],
            AudioExtractionOptions.Default);

        Assert.IsTrue(
            markdown.IndexOf("first", StringComparison.Ordinal) <
            markdown.IndexOf("later", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies that the metadata table can be omitted.
    /// </summary>
    [TestMethod]
    public void Format_WithMetadataDisabled_OmitsPropertyTable()
    {
        var markdown = MarkdownFormatter.Format(
            [new TranscriptSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "text")],
            new AudioExtractionOptions { IncludeMetadata = false });

        Assert.IsFalse(markdown.Contains("| Property | Value |"));
        Assert.IsTrue(markdown.Contains("[00:00:00] text"));
    }
}
