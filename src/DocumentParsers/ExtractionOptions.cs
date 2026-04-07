namespace FieldCure.DocumentParsers;

/// <summary>
/// Options to control which elements are included in the extracted text output.
/// All options default to <c>true</c> (include everything).
/// </summary>
public sealed class ExtractionOptions
{
    /// <summary>
    /// Include YAML front matter with document metadata (title, author, dates, etc.).
    /// </summary>
    public bool IncludeMetadata { get; init; } = true;

    /// <summary>
    /// Include header text at the beginning of each section.
    /// </summary>
    public bool IncludeHeaders { get; init; } = true;

    /// <summary>
    /// Include footer text at the end of each section.
    /// </summary>
    public bool IncludeFooters { get; init; } = true;

    /// <summary>
    /// Include footnote references in body text and definitions at the end.
    /// </summary>
    public bool IncludeFootnotes { get; init; } = true;

    /// <summary>
    /// Include endnote references in body text and definitions at the end.
    /// </summary>
    public bool IncludeEndnotes { get; init; } = true;

    /// <summary>
    /// Include comment/memo annotations inline with body text.
    /// </summary>
    public bool IncludeComments { get; init; } = true;

    /// <summary>
    /// Default instance with all options enabled.
    /// </summary>
    public static ExtractionOptions Default { get; } = new();
}
