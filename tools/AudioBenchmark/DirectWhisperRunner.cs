using System.Runtime.CompilerServices;
using FieldCure.DocumentParsers.Audio;
using FieldCure.DocumentParsers.Audio.Transcription;
using Whisper.net;

namespace FieldCure.Tools.AudioBenchmark;

/// <summary>
/// Lightweight diagnostic-only transcriber that bypasses
/// <see cref="WhisperTranscriber"/> so the benchmark can toggle Whisper.net
/// builder knobs (currently <c>WithNoContext</c>) that the production
/// transcriber does not expose. Implements <see cref="IAudioTranscriber"/> so
/// the rest of the benchmark code path is identical for A vs. B comparisons.
/// </summary>
/// <remarks>
/// Intentionally minimal. Re-creates the processor for each call rather than
/// caching, since the diagnostic runs are short and isolation between runs
/// outweighs the extra builder cost.
/// </remarks>
internal sealed class DirectWhisperRunner : IAudioTranscriber
{
    private readonly WhisperFactory _factory;
    private readonly bool _noContext;
    private readonly FallbackMode _fallbackMode;

    /// <summary>Decoding-failure fallback configuration applied to each builder.</summary>
    internal enum FallbackMode
    {
        /// <summary>No fallback; matches Whisper.net default.</summary>
        None,
        /// <summary>WithTemperatureInc(0.2) only — minimal escalation.</summary>
        TempIncOnly,
        /// <summary>TempInc + LogProbThreshold(-1.0) + EntropyThreshold(2.4) — full whisper.cpp standard.</summary>
        Full,
    }

    public DirectWhisperRunner(string modelPath, bool noContext, FallbackMode fallback = FallbackMode.None)
    {
        _factory = WhisperFactory.FromPath(modelPath);
        _noContext = noContext;
        _fallbackMode = fallback;
    }

    public async IAsyncEnumerable<TranscriptSegment> TranscribeAsync(
        Stream pcmStream,
        AudioExtractionOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (pcmStream.CanSeek) pcmStream.Position = 0;

        var builder = _factory.CreateBuilder()
            .WithLanguage(string.IsNullOrWhiteSpace(options.Language) ? "auto" : options.Language);

        if (_noContext) builder = builder.WithNoContext();
        switch (_fallbackMode)
        {
            case FallbackMode.TempIncOnly:
                // Minimal escalation: enable the temperature-increment knob
                // but leave logprob/entropy triggers at Whisper.net defaults.
                // If this alone breaks the loop, the fallback retry path
                // does not actually need the threshold knobs to fire.
                builder = builder
                    .WithTemperature(0.0f)
                    .WithTemperatureInc(0.2f);
                break;
            case FallbackMode.Full:
                // Whisper.cpp standard fallback chain. When the greedy decode
                // yields a segment whose avg log-prob < -1.0 OR token entropy
                // > 2.4, the segment is retried with temperature += 0.2 up to
                // 1.0. Without these triggers, Whisper.net runs at temp 0
                // with no escape from beam-search loops.
                builder = builder
                    .WithTemperature(0.0f)
                    .WithTemperatureInc(0.2f)
                    .WithLogProbThreshold(-1.0f)
                    .WithEntropyThreshold(2.4f);
                break;
        }
        if (options.TranslateToEnglish) builder = builder.WithTranslate();
        if (options.IncludeConfidence) builder = builder.WithProbabilities();

        using var processor = builder.Build();
        await foreach (var result in processor.ProcessAsync(pcmStream, cancellationToken))
        {
            var text = result.Text?.Trim() ?? string.Empty;
            if (text.Length == 0) continue;

            yield return new TranscriptSegment(
                result.Start,
                result.End,
                text,
                string.IsNullOrWhiteSpace(result.Language) ? options.Language : result.Language,
                result.Probability);
        }
    }

    public ValueTask DisposeAsync()
    {
        _factory.Dispose();
        return ValueTask.CompletedTask;
    }
}
