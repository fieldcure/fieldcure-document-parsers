using System.Text;

namespace FieldCure.DocumentParsers;

/// <summary>
/// Collects footnote and endnote entries during document traversal
/// and renders them as markdown reference definitions.
/// </summary>
internal sealed class FootnoteCollector
{
    private readonly List<(string Id, string Content)> _footnotes = [];
    private readonly List<(string Id, string Content)> _endnotes = [];

    /// <summary>
    /// Registers a footnote and returns the inline reference marker (e.g., "[^1]").
    /// </summary>
    public string AddFootnote(int id, string content)
    {
        var marker = $"[^{id}]";
        _footnotes.Add((marker, content));
        return marker;
    }

    /// <summary>
    /// Registers an endnote and returns the inline reference marker (e.g., "[^en1]").
    /// </summary>
    public string AddEndnote(int id, string content)
    {
        var marker = $"[^en{id}]";
        _endnotes.Add((marker, content));
        return marker;
    }

    /// <summary>
    /// Renders all collected footnotes and endnotes as markdown sections.
    /// Returns empty string if none were collected.
    /// </summary>
    public string RenderAll()
    {
        if (_footnotes.Count == 0 && _endnotes.Count == 0) return "";

        var sb = new StringBuilder();

        if (_footnotes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Footnotes");
            sb.AppendLine();
            foreach (var (id, content) in _footnotes)
            {
                sb.AppendLine($"{id}: {content}");
                sb.AppendLine();
            }
        }

        if (_endnotes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Endnotes");
            sb.AppendLine();
            foreach (var (id, content) in _endnotes)
            {
                sb.AppendLine($"{id}: {content}");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
