using System.Text;
using FieldCure.DocumentParsers;

Console.OutputEncoding = Encoding.UTF8;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.Error.WriteLine("Usage: FieldCure.DocumentParsers.Cli <file-path> [output-path]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("  file-path    Document to extract text from");
    Console.Error.WriteLine("  output-path  Optional. Write UTF-8 output to file instead of stdout");
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Supported formats: {string.Join(", ", DocumentParserFactory.SupportedExtensions)}");
    return 1;
}

var filePath = args[0];
if (!File.Exists(filePath))
{
    Console.Error.WriteLine($"File not found: {filePath}");
    return 1;
}

var ext = Path.GetExtension(filePath);
var parser = DocumentParserFactory.GetParser(ext);
if (parser is null)
{
    Console.Error.WriteLine($"Unsupported format: {ext}");
    Console.Error.WriteLine($"Supported: {string.Join(", ", DocumentParserFactory.SupportedExtensions)}");
    return 1;
}

var data = File.ReadAllBytes(filePath);
var text = parser.ExtractText(data);
var outputPath = args.Length > 1 ? args[1] : null;

if (outputPath is not null)
{
    var outExt = Path.GetExtension(outputPath);
    if (outExt is not "" and not ".md" and not ".txt")
    {
        Console.Error.WriteLine($"Warning: output is Markdown text, but extension is '{outExt}'");
    }

    if (Path.GetFullPath(outputPath) == Path.GetFullPath(filePath))
    {
        Console.Error.WriteLine("Error: output path is the same as input file.");
        return 1;
    }

    await File.WriteAllTextAsync(outputPath, text, Encoding.UTF8);
    Console.Error.WriteLine($"Written to {outputPath}");
}
else
{
    Console.WriteLine(text);
}
return 0;
