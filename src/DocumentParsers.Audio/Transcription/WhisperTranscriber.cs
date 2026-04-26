using System.Runtime.CompilerServices;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace FieldCure.DocumentParsers.Audio.Transcription;

/// <summary>
/// Whisper.net-based <see cref="IAudioTranscriber"/> implementation.
/// </summary>
public sealed class WhisperTranscriber : IAudioTranscriber
{
    private readonly WhisperModelProvider _modelProvider;
    private readonly SemaphoreSlim _factoryGate = new(1, 1);
    private string? _cachedModelPath;
    private WhisperFactory? _cachedFactory;
    private bool _disposed;

    /// <summary>
    /// Creates a Whisper transcriber with the default model provider.
    /// </summary>
    public WhisperTranscriber()
        : this(new WhisperModelProvider())
    {
    }

    /// <summary>
    /// Creates a Whisper transcriber with a custom model provider.
    /// </summary>
    /// <param name="modelProvider">Model provider used to resolve ggml model files.</param>
    public WhisperTranscriber(WhisperModelProvider modelProvider)
    {
        ArgumentNullException.ThrowIfNull(modelProvider);
        _modelProvider = modelProvider;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TranscriptSegment> TranscribeAsync(
        Stream pcmStream,
        AudioExtractionOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pcmStream);
        ArgumentNullException.ThrowIfNull(options);

        ConfigureRuntimeOrder(options.RuntimeLibraryOrder);

        var modelPath = !string.IsNullOrWhiteSpace(options.ModelPath)
            ? options.ModelPath!
            : await _modelProvider.GetModelPathAsync(options.ModelSize, cancellationToken).ConfigureAwait(false);

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException("Whisper model file was not found.", modelPath);
        }

        if (pcmStream.CanSeek)
        {
            pcmStream.Position = 0;
        }

        var whisperFactory = await GetOrCreateFactoryAsync(modelPath, cancellationToken).ConfigureAwait(false);
        var builder = whisperFactory.CreateBuilder()
            .WithLanguage(string.IsNullOrWhiteSpace(options.Language) ? "auto" : options.Language);

        if (options.TranslateToEnglish)
        {
            builder = builder.WithTranslate();
        }

        if (options.IncludeConfidence)
        {
            builder = builder.WithProbabilities();
        }

        using var processor = builder.Build();
        await foreach (var result in processor.ProcessAsync(pcmStream, cancellationToken)
            .ConfigureAwait(false))
        {
            var text = result.Text?.Trim() ?? string.Empty;
            if (text.Length == 0)
            {
                continue;
            }

            var language = !string.IsNullOrWhiteSpace(result.Language)
                ? result.Language
                : options.Language;

            yield return new TranscriptSegment(
                result.Start,
                result.End,
                text,
                language,
                result.Probability);
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _cachedFactory?.Dispose();
        _cachedFactory = null;
        _factoryGate.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Returns a cached <see cref="WhisperFactory"/> for the given model path,
    /// rebuilding it when the model path changes.
    /// </summary>
    /// <param name="modelPath">Resolved ggml model file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whisper factory bound to <paramref name="modelPath"/>.</returns>
    private async Task<WhisperFactory> GetOrCreateFactoryAsync(string modelPath, CancellationToken cancellationToken)
    {
        await _factoryGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_cachedFactory is not null
                && string.Equals(_cachedModelPath, modelPath, StringComparison.OrdinalIgnoreCase))
            {
                return _cachedFactory;
            }

            _cachedFactory?.Dispose();
            _cachedFactory = WhisperFactory.FromPath(modelPath);
            _cachedModelPath = modelPath;
            return _cachedFactory;
        }
        finally
        {
            _factoryGate.Release();
        }
    }

    /// <summary>
    /// Lock guarding writes to the Whisper.net global runtime selection.
    /// </summary>
    private static readonly object RuntimeOrderGate = new();

    /// <summary>
    /// Applies a caller-specified native runtime loading order when one was provided.
    /// Whisper.net exposes <see cref="RuntimeOptions.RuntimeLibraryOrder"/> as process-global
    /// state, so this assignment is last-writer-wins across the process. Callers that need a
    /// specific order should set it once at startup rather than per call.
    /// </summary>
    /// <param name="runtimeLibraryOrder">Preferred Whisper.net runtime order.</param>
    private static void ConfigureRuntimeOrder(IReadOnlyList<RuntimeLibrary>? runtimeLibraryOrder)
    {
        if (runtimeLibraryOrder is null || runtimeLibraryOrder.Count == 0)
        {
            return;
        }

        lock (RuntimeOrderGate)
        {
            RuntimeOptions.RuntimeLibraryOrder = runtimeLibraryOrder.ToList();
        }
    }
}
