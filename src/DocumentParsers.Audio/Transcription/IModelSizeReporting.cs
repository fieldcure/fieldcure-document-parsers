namespace FieldCure.DocumentParsers.Audio.Transcription;

/// <summary>
/// Optional capability for transcribers that can report their effective Whisper model size.
/// </summary>
public interface IModelSizeReporting
{
    /// <summary>
    /// Gets the Whisper model size actually used by the transcriber, or
    /// <see langword="null"/> when the effective size is not known or not size-based.
    /// </summary>
    WhisperModelSize? EffectiveModelSize { get; }
}
