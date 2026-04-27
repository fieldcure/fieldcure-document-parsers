using System.Collections.Concurrent;
using Whisper.net.Ggml;

namespace FieldCure.DocumentParsers.Audio.Transcription;

/// <summary>
/// Manages Whisper ggml model download and local caching.
/// </summary>
public class WhisperModelProvider
{
    /// <summary>
    /// Per-cache-path semaphores that serialize concurrent downloads of the same model.
    /// </summary>
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> DownloadGates =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a model provider that stores models under
    /// <c>{UserProfile}/.fieldcure/whisper-models</c>.
    /// </summary>
    public WhisperModelProvider()
        : this(GetDefaultCacheDirectory())
    {
    }

    /// <summary>
    /// Creates a model provider with a custom cache directory.
    /// </summary>
    /// <param name="cacheDirectory">Directory where downloaded ggml models are cached.</param>
    public WhisperModelProvider(string cacheDirectory)
    {
        if (string.IsNullOrWhiteSpace(cacheDirectory))
            throw new ArgumentException("Cache directory must not be empty.", nameof(cacheDirectory));

        CacheDirectory = cacheDirectory;
    }

    /// <summary>
    /// Directory where ggml model files are cached.
    /// </summary>
    public string CacheDirectory { get; }

    /// <summary>
    /// Returns a local model path, downloading the model if needed.
    /// </summary>
    /// <param name="modelSize">Whisper model size to resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Local model file path.</returns>
    public virtual async Task<string> GetModelPathAsync(
        WhisperModelSize modelSize,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(CacheDirectory);

        var modelName = GetModelFileName(modelSize);
        var modelPath = Path.Combine(CacheDirectory, modelName);
        if (File.Exists(modelPath) && new FileInfo(modelPath).Length > 0)
        {
            return modelPath;
        }

        var gate = DownloadGates.GetOrAdd(modelPath, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(modelPath) && new FileInfo(modelPath).Length > 0)
            {
                return modelPath;
            }

            var tempPath = modelPath + ".download";
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            using (var modelStream = await WhisperGgmlDownloader.Default
                .GetGgmlModelAsync(ToGgmlType(modelSize))
                .ConfigureAwait(false))
            await using (var fileWriter = File.Create(tempPath))
            {
                await modelStream.CopyToAsync(fileWriter, cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, modelPath, overwrite: true);
            return modelPath;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Returns the cache file name for a model size.
    /// </summary>
    /// <param name="modelSize">Whisper model size.</param>
    /// <returns>Model cache file name.</returns>
    public static string GetModelFileName(WhisperModelSize modelSize) => modelSize switch
    {
        WhisperModelSize.Tiny => "ggml-tiny.bin",
        WhisperModelSize.Base => "ggml-base.bin",
        WhisperModelSize.Small => "ggml-small.bin",
        WhisperModelSize.Medium => "ggml-medium.bin",
        // Mapped to large-v2 instead of large-v3 due to large-v3's documented
        // long-form repetition-loop instability. See baseline-2026-04-27.md
        // for the measurements behind this decision (v0.2.1).
        WhisperModelSize.Large => "ggml-large-v2.bin",
        _ => "ggml-base.bin"
    };

    /// <summary>
    /// Resolves a local model path for an arbitrary <see cref="GgmlType"/>,
    /// downloading on first use. Exposed for the benchmark tool only — the
    /// public API stays size-based (<see cref="WhisperModelSize"/>) so
    /// production callers don't have to track every Whisper variant.
    /// </summary>
    /// <param name="ggmlType">Whisper.net ggml model variant.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task<string> GetModelPathByGgmlTypeAsync(
        GgmlType ggmlType,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(CacheDirectory);

        var modelName = GetGgmlTypeFileName(ggmlType);
        var modelPath = Path.Combine(CacheDirectory, modelName);
        if (File.Exists(modelPath) && new FileInfo(modelPath).Length > 0)
        {
            return modelPath;
        }

        var gate = DownloadGates.GetOrAdd(modelPath, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(modelPath) && new FileInfo(modelPath).Length > 0)
            {
                return modelPath;
            }

            var tempPath = modelPath + ".download";
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            using (var modelStream = await WhisperGgmlDownloader.Default
                .GetGgmlModelAsync(ggmlType)
                .ConfigureAwait(false))
            await using (var fileWriter = File.Create(tempPath))
            {
                await modelStream.CopyToAsync(fileWriter, cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, modelPath, overwrite: true);
            return modelPath;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Cache filename for a specific <see cref="GgmlType"/>. Variant suffixes
    /// (<c>-v2</c>, <c>-v3-turbo</c>, etc.) keep different Large weights from
    /// colliding in the cache directory.
    /// </summary>
    internal static string GetGgmlTypeFileName(GgmlType type) => type switch
    {
        GgmlType.LargeV1 => "ggml-large-v1.bin",
        GgmlType.LargeV2 => "ggml-large-v2.bin",
        GgmlType.LargeV3 => "ggml-large-v3.bin",
        GgmlType.LargeV3Turbo => "ggml-large-v3-turbo.bin",
        _ => $"ggml-{type.ToString().ToLowerInvariant()}.bin",
    };

    /// <summary>
    /// Converts the public model-size enum to Whisper.net's downloader enum.
    /// </summary>
    /// <param name="modelSize">Public model size.</param>
    /// <returns>Whisper.net ggml model type.</returns>
    private static GgmlType ToGgmlType(WhisperModelSize modelSize) => modelSize switch
    {
        WhisperModelSize.Tiny => GgmlType.Tiny,
        WhisperModelSize.Base => GgmlType.Base,
        WhisperModelSize.Small => GgmlType.Small,
        WhisperModelSize.Medium => GgmlType.Medium,
        // Mapped to LargeV2 instead of LargeV3 due to long-form hallucination
        // instability; see baseline-2026-04-27.md.
        WhisperModelSize.Large => GgmlType.LargeV2,
        _ => GgmlType.Base
    };

    /// <summary>
    /// Returns the default per-user model cache directory.
    /// </summary>
    /// <returns>Default model cache directory.</returns>
    private static string GetDefaultCacheDirectory()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
        {
            userProfile = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        return Path.Combine(userProfile, ".fieldcure", "whisper-models");
    }
}
