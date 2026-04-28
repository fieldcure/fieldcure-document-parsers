namespace FieldCure.DocumentParsers.Audio.Runtime;

/// <summary>
/// Phase reported by <see cref="WhisperRuntimeProgress"/> during provisioning.
/// </summary>
public enum WhisperRuntimePhase
{
    /// <summary>Fetching and parsing the manifest.</summary>
    Resolving,

    /// <summary>Downloading a binary file from GitHub Releases.</summary>
    Downloading,

    /// <summary>Computing SHA-256 of a downloaded file and comparing to manifest.</summary>
    Verifying,

    /// <summary>Setting <c>RuntimeOptions.LibraryPath</c> on the Whisper.net loader.</summary>
    Activating,
}
