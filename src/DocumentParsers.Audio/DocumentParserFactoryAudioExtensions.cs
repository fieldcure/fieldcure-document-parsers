using FieldCure.DocumentParsers.Audio.Transcription;

namespace FieldCure.DocumentParsers.Audio;

/// <summary>
/// Registers audio transcription support with <see cref="DocumentParserFactory"/>.
/// </summary>
public static class DocumentParserFactoryAudioExtensions
{
    /// <summary>
    /// Registers <see cref="AudioDocumentParser"/> with a caller-supplied transcriber.
    /// The caller owns the transcriber's lifetime.
    /// </summary>
    public static void AddAudioSupport(IAudioTranscriber transcriber)
        => DocumentParserFactory.Register(new AudioDocumentParser(transcriber));

    /// <summary>
    /// Creates and registers a default <see cref="WhisperTranscriber"/>.
    /// Dispose the returned transcriber at shutdown.
    /// </summary>
    public static WhisperTranscriber AddAudioSupport()
    {
        var transcriber = new WhisperTranscriber();
        AddAudioSupport(transcriber);
        return transcriber;
    }
}
