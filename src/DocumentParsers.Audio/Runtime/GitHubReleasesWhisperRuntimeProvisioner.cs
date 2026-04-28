using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace FieldCure.DocumentParsers.Audio.Runtime;

/// <summary>
/// Default <see cref="IWhisperRuntimeProvisioner"/>. Fetches binaries from
/// <see href="https://github.com/fieldcure/fieldcure-whisper-runtimes/releases">fieldcure-whisper-runtimes</see>
/// when online; reads from <c>FIELDCURE_WHISPER_RUNTIME_DIR</c> when set.
/// </summary>
public sealed class GitHubReleasesWhisperRuntimeProvisioner : IWhisperRuntimeProvisioner
{
    /// <summary>Environment variable that, when set, overrides the cache directory and
    /// forces offline mode. The directory must contain <c>manifest.json</c> at its root
    /// plus a <c>runtimes/</c> tree mirroring the online cache layout.</summary>
    public const string OverrideEnvironmentVariable = "FIELDCURE_WHISPER_RUNTIME_DIR";

    /// <summary>Default manifest URL. Pinned to the Whisper.net version Audio v0.3
    /// is built against (1.9.0).</summary>
    public const string DefaultManifestUrl =
        "https://github.com/fieldcure/fieldcure-whisper-runtimes/releases/download/v1.9.0/manifest.json";

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> s_fileGates =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> s_redistAttributionLogged =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly object s_redistLock = new();

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly string _manifestUrl;
    private readonly bool _isOfflineOverride;

    private WhisperRuntimeManifest? _cachedManifest;
    private readonly SemaphoreSlim _manifestGate = new(1, 1);

    /// <summary>Creates a provisioner using the default cache directory and manifest URL.</summary>
    public GitHubReleasesWhisperRuntimeProvisioner()
        : this(cacheDirectory: null, manifestUrl: null, httpClient: null)
    {
    }

    /// <summary>Creates a provisioner with custom configuration. All parameters optional —
    /// pass <see langword="null"/> to use defaults.</summary>
    /// <param name="cacheDirectory">Override cache root. <see langword="null"/> falls back to
    /// the <see cref="OverrideEnvironmentVariable"/> environment variable, then to
    /// <c>%LOCALAPPDATA%\FieldCure\WhisperRuntimes\</c>.</param>
    /// <param name="manifestUrl">Override manifest URL. <see langword="null"/> uses
    /// <see cref="DefaultManifestUrl"/>. Ignored in offline override mode.</param>
    /// <param name="httpClient">Inject a custom <see cref="HttpClient"/>. <see langword="null"/>
    /// causes the provisioner to construct its own (and dispose it on
    /// <see cref="Dispose"/>).</param>
    public GitHubReleasesWhisperRuntimeProvisioner(
        string? cacheDirectory,
        string? manifestUrl,
        HttpClient? httpClient)
    {
        var envOverride = Environment.GetEnvironmentVariable(OverrideEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envOverride))
        {
            CacheDirectory = envOverride;
            _isOfflineOverride = true;
        }
        else
        {
            CacheDirectory = !string.IsNullOrWhiteSpace(cacheDirectory)
                ? cacheDirectory
                : GetDefaultCacheDirectory();
            _isOfflineOverride = false;
        }

        _manifestUrl = !string.IsNullOrWhiteSpace(manifestUrl) ? manifestUrl : DefaultManifestUrl;

        if (httpClient is null)
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            _ownsHttpClient = true;
        }
        else
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }

        Directory.CreateDirectory(CacheDirectory);
        SweepOrphanDownloads();
    }

    /// <inheritdoc />
    public string CacheDirectory { get; }

    /// <summary>True when <see cref="OverrideEnvironmentVariable"/> is set; the provisioner
    /// will not perform network I/O and treats the cache directory as authoritative.</summary>
    public bool IsOfflineOverride => _isOfflineOverride;

    /// <inheritdoc />
    public async Task<WhisperRuntimeManifest> GetManifestAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedManifest is not null) return _cachedManifest;

        await _manifestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cachedManifest is not null) return _cachedManifest;

            string json;
            if (_isOfflineOverride)
            {
                var localPath = Path.Combine(CacheDirectory, "manifest.json");
                if (!File.Exists(localPath))
                {
                    throw new WhisperRuntimeException(
                        $"Offline override directory '{CacheDirectory}' does not contain manifest.json. " +
                        $"Place a manifest.json at the directory root before invoking the provisioner.");
                }
                json = await File.ReadAllTextAsync(localPath, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                using var resp = await _httpClient.GetAsync(_manifestUrl,
                    HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                // Cache a local copy for offline-after-online resilience. Best-effort.
                try
                {
                    var localPath = Path.Combine(CacheDirectory, "manifest.json");
                    await File.WriteAllTextAsync(localPath, json, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Disk full / permission denied — non-fatal.
                }
            }

            _cachedManifest = WhisperRuntimeManifest.Parse(json);
            return _cachedManifest;
        }
        finally
        {
            _manifestGate.Release();
        }
    }

    /// <inheritdoc />
    public bool IsProvisioned(WhisperRuntimeVariant variant)
    {
        // Synchronous probe — load manifest if not yet, but only the parsed version
        // (offline mode reads from disk, online mode does the network fetch).
        var manifest = GetManifestAsync().GetAwaiter().GetResult();
        var spec = manifest.GetVariant(variant);
        if (spec is null) return false;

        var rid = GetCurrentRid();
        if (!spec.Platforms.TryGetValue(rid, out var files) || files.Count == 0) return false;

        var dir = GetVariantDirectory(variant);
        foreach (var file in files)
        {
            var path = Path.Combine(dir, file.Name);
            if (!File.Exists(path)) return false;
            // Spec contract: hash is NOT re-verified here. The atomic File.Move + verification
            // at download time guarantees only valid bytes are in the cache. This keeps
            // IsProvisioned cheap (file existence check only).
        }
        return true;
    }

    /// <inheritdoc />
    public async Task ProvisionAsync(
        WhisperRuntimeVariant variant,
        CancellationToken cancellationToken = default,
        IProgress<WhisperRuntimeProgress>? progress = null)
    {
        progress?.Report(new WhisperRuntimeProgress(WhisperRuntimePhase.Resolving, "manifest.json", 0, null));
        var manifest = await GetManifestAsync(cancellationToken).ConfigureAwait(false);

        var spec = manifest.GetVariant(variant)
            ?? throw new WhisperRuntimeException(
                $"Manifest does not declare variant '{variant}'.");

        var rid = GetCurrentRid();
        if (!spec.Platforms.TryGetValue(rid, out var files) || files.Count == 0)
        {
            throw new WhisperRuntimeException(
                $"Manifest does not declare any files for variant '{variant}' on RID '{rid}'.");
        }

        var targetDir = GetVariantDirectory(variant);
        Directory.CreateDirectory(targetDir);

        if (_isOfflineOverride)
        {
            var missing = new List<string>();
            foreach (var file in files)
            {
                if (!File.Exists(Path.Combine(targetDir, file.Name)))
                {
                    missing.Add(file.Name);
                }
            }
            if (missing.Count > 0)
            {
                throw new WhisperRuntimeMissingException(missing);
            }
            return;
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetPath = Path.Combine(targetDir, file.Name);

            // Per-target SemaphoreSlim serializes concurrent ProvisionAsync calls in the
            // same process; cross-process races resolve via byte-identical File.Move overwrite.
            var gate = s_fileGates.GetOrAdd(targetPath, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (File.Exists(targetPath) && new FileInfo(targetPath).Length > 0)
                {
                    continue;
                }

                if (file.NvidiaRedist)
                {
                    EmitNvidiaAttributionOnce(file.Name);
                }

                await DownloadFileAsync(file, targetPath, cancellationToken, progress)
                    .ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }
    }

    /// <summary>Disposes the internal <see cref="HttpClient"/> if owned.</summary>
    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
        _manifestGate.Dispose();
    }

    private async Task DownloadFileAsync(
        WhisperRuntimeManifestFile file,
        string targetPath,
        CancellationToken cancellationToken,
        IProgress<WhisperRuntimeProgress>? progress)
    {
        var tempPath = $"{targetPath}.download.{Guid.NewGuid():N}";

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, file.Url);
            using var resp = await _httpClient.SendAsync(req,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var totalBytes = resp.Content.Headers.ContentLength;

            using (var sourceStream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None,
                       bufferSize: 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                long received = 0;
                long lastReported = 0;
                int read;
                while ((read = await sourceStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                                                  .ConfigureAwait(false)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    received += read;

                    // Throttle progress to ~once per 256 KB to avoid flooding consumers.
                    if (received - lastReported >= 262144 || (totalBytes is long t && received == t))
                    {
                        progress?.Report(new WhisperRuntimeProgress(
                            WhisperRuntimePhase.Downloading, file.Name, received, totalBytes));
                        lastReported = received;
                    }
                }
            }

            progress?.Report(new WhisperRuntimeProgress(WhisperRuntimePhase.Verifying, file.Name, 0, null));
            await VerifySha256Async(tempPath, file, cancellationToken).ConfigureAwait(false);

            File.Move(tempPath, targetPath, overwrite: true);
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* swallow */ }
            throw;
        }
    }

    private static async Task VerifySha256Async(
        string filePath,
        WhisperRuntimeManifestFile fileSpec,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(fileSpec.Sha256))
        {
            // Manifest didn't declare a hash — skip verification (acceptable in dev/test
            // manifests). Production manifests should always carry hashes.
            return;
        }

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        var expected = fileSpec.Sha256.ToLowerInvariant();

        if (!string.Equals(hex, expected, StringComparison.Ordinal))
        {
            throw new WhisperRuntimeIntegrityException(fileSpec.Name, expected, hex);
        }
    }

    private string GetVariantDirectory(WhisperRuntimeVariant variant)
    {
        var rid = GetCurrentRid();
        return variant switch
        {
            WhisperRuntimeVariant.Cpu => Path.Combine(CacheDirectory, "runtimes", rid),
            WhisperRuntimeVariant.Cuda => Path.Combine(CacheDirectory, "runtimes", "cuda", rid),
            WhisperRuntimeVariant.Vulkan => Path.Combine(CacheDirectory, "runtimes", "vulkan", rid),
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "Unknown variant.")
        };
    }

    private static string GetCurrentRid()
    {
        // Audio is Windows-only by package gate; v0.3 manifest only ships win-x64.
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException(
                $"Unsupported process architecture for Whisper runtime: {RuntimeInformation.ProcessArchitecture}.")
        };
        return CultureInfo.InvariantCulture.TextInfo.ToLower($"win-{arch}");
    }

    private static string GetDefaultCacheDirectory()
    {
        // %LOCALAPPDATA%\FieldCure\WhisperRuntimes\ on Windows, matching the model cache
        // sibling at %LOCALAPPDATA%\FieldCure\WhisperModels\.
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            return Path.Combine(localAppData, "FieldCure", "WhisperRuntimes");
        }

        // Unlikely fallback. Audio is Windows-only so non-Windows hosts shouldn't reach this code.
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".fieldcure", "whisper-runtimes");
    }

    private static void EmitNvidiaAttributionOnce(string fileName)
    {
        lock (s_redistLock)
        {
            if (!s_redistAttributionLogged.Add(fileName)) return;
        }
        try
        {
            Console.Error.WriteLine(
                $"[whisper-runtime] {fileName}: NVIDIA CUDA Toolkit redistributable component " +
                $"(EULA: https://docs.nvidia.com/cuda/eula/)");
        }
        catch
        {
            // stderr unavailable (rare; non-console host) — drop the notice; license posture
            // is still satisfied via the NOTICE file in the upstream whisper-runtimes repo.
        }
    }

    private void SweepOrphanDownloads()
    {
        // Catch leftover .download.<guid> temp files from a process that was killed mid-fetch.
        // Cheap (file enumeration only on a small directory), fail-soft on permission errors.
        try
        {
            var runtimesRoot = Path.Combine(CacheDirectory, "runtimes");
            if (!Directory.Exists(runtimesRoot)) return;

            foreach (var orphan in Directory.EnumerateFiles(runtimesRoot, "*.download.*", SearchOption.AllDirectories))
            {
                try { File.Delete(orphan); } catch { /* swallow */ }
            }
        }
        catch
        {
            // Permission denied / unavailable — proceed; per-file cleanup at retry handles the rest.
        }
    }
}
