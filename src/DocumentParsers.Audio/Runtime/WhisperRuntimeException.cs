namespace FieldCure.DocumentParsers.Audio.Runtime;

/// <summary>
/// Base exception type for runtime provisioning / activation failures.
/// </summary>
public class WhisperRuntimeException : Exception
{
    /// <inheritdoc />
    public WhisperRuntimeException(string message) : base(message) { }

    /// <inheritdoc />
    public WhisperRuntimeException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thrown when a downloaded runtime file's SHA-256 hash does not match the
/// manifest declaration. The temp <c>.download.&lt;guid&gt;</c> file is deleted before
/// this exception propagates; the cache stays consistent.
/// </summary>
public sealed class WhisperRuntimeIntegrityException : WhisperRuntimeException
{
    /// <summary>Name of the file whose hash failed verification.</summary>
    public string FileName { get; }

    /// <summary>Hash declared by the manifest.</summary>
    public string ExpectedSha256 { get; }

    /// <summary>Hash computed from the downloaded bytes.</summary>
    public string ActualSha256 { get; }

    /// <inheritdoc />
    public WhisperRuntimeIntegrityException(string fileName, string expected, string actual)
        : base($"SHA-256 mismatch for '{fileName}': expected {expected}, got {actual}.")
    {
        FileName = fileName;
        ExpectedSha256 = expected;
        ActualSha256 = actual;
    }
}

/// <summary>
/// Thrown in offline override mode (<c>FIELDCURE_WHISPER_RUNTIME_DIR</c> set) when the
/// pre-staged directory does not contain every file declared by the manifest for the
/// requested variant. Online mode would silently re-download; offline mode treats this
/// as a hard configuration error so operators see the missing-file list immediately.
/// </summary>
public sealed class WhisperRuntimeMissingException : WhisperRuntimeException
{
    /// <summary>File names that the manifest requires but the override directory lacks.</summary>
    public IReadOnlyList<string> MissingFiles { get; }

    /// <inheritdoc />
    public WhisperRuntimeMissingException(IReadOnlyList<string> missingFiles)
        : base($"Whisper runtime files missing in offline override: {string.Join(", ", missingFiles)}.")
    {
        MissingFiles = missingFiles;
    }
}
