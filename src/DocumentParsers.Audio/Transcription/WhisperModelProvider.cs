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
        WhisperModelSize.Large => "ggml-large-v3.bin",
        _ => "ggml-base.bin"
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
        WhisperModelSize.Large => GgmlType.LargeV3,
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
