namespace FieldCure.DocumentParsers.Audio.Transcription;

/// <summary>
/// Represents a single transcribed segment with timing information.
/// </summary>
/// <param name="Start">Segment start time.</param>
/// <param name="End">Segment end time.</param>
/// <param name="Text">Transcribed text content.</param>
/// <param name="Language">Detected or hinted language code.</param>
/// <param name="Probability">Confidence probability from 0.0 to 1.0, if available.</param>
public readonly record struct TranscriptSegment(
    TimeSpan Start,
    TimeSpan End,
    string Text,
    string? Language = null,
    float Probability = 0f);
