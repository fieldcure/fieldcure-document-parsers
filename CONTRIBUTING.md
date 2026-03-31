# Contributing to FieldCure.DocumentParsers

Thank you for your interest in contributing!

## Adding a New Parser

1. Implement `IDocumentParser` (or `IMediaDocumentParser` for formats with images)
2. Register in `DocumentParserFactory`
3. Add unit tests with sample files in `tests/`
4. Update README — add format to the features list and limitations table

### File Structure

```
src/FieldCure.DocumentParsers/
├── IDocumentParser.cs           ← Interface
├── DocumentParserFactory.cs     ← Extension → Parser mapping
├── DocxParser.cs                ← One file per format
├── HwpxParser.cs
└── ...

tests/FieldCure.DocumentParsers.Tests/
├── DocxParserTests.cs
├── TestFiles/                   ← Minimal sample files for tests
│   ├── simple.docx
│   ├── table.docx
│   └── ...
└── ...
```

### Bug Fixes

- Include a test file (or programmatically generated sample) that reproduces the issue
- Ensure existing tests still pass

### Code Style

- Follow existing patterns (nullable enabled, file-scoped namespaces)
- XML documentation on public APIs
- Comments in English

## Building

```bash
dotnet build
dotnet test
```

## License

By contributing, you agree that your contributions will be licensed under MIT.
