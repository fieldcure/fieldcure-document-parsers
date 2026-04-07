using System.Text;

namespace FieldCure.DocumentParsers;

/// <summary>
/// Extracts readable content from HTML files using SmartReader + ReverseMarkdown.
/// SmartReader extracts main content (removes nav, ads, etc.), then ReverseMarkdown
/// converts the cleaned HTML to GitHub-flavored Markdown.
/// </summary>
public sealed class HtmlParser : IDocumentParser
{
    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions => [".html", ".htm"];

    /// <inheritdoc />
    public string ExtractText(byte[] data)
    {
        var html = Encoding.UTF8.GetString(data);

        // SmartReader: extract main content (removes nav, ads, etc.)
        var article = SmartReader.Reader.ParseArticle("https://localhost", html);

        var content = article.IsReadable
            ? article.Content    // cleaned HTML
            : html;              // fallback: use original HTML

        // ReverseMarkdown: HTML → Markdown
        var converter = new ReverseMarkdown.Converter(new ReverseMarkdown.Config
        {
            GithubFlavored = true,
            RemoveComments = true,
            SmartHrefHandling = true,
        });

        var markdown = converter.Convert(content);
        return markdown.Trim();
    }
}
