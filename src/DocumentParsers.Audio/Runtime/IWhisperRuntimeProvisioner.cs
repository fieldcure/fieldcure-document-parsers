namespace FieldCure.DocumentParsers.Audio.Runtime;

/// <summary>
/// Acquires Whisper native runtime binaries on demand. Implementations download
/// from <see href="https://github.com/fieldcure/fieldcure-whisper-runtimes">fieldcure-whisper-runtimes</see>
/// (<see cref="GitHubReleasesWhisperRuntimeProvisioner"/>) or read from a pre-staged
/// directory in offline mode.
/// </summary>
/// <remarks>
/// <para>Provisioners are <b>idempotent</b>: calling <see cref="ProvisionAsync"/>
/// for an already-cached variant returns immediately without network I/O.</para>
/// <para>Provisioners are <b>concurrency-safe</b>: parallel calls for the same
/// variant share work via per-file <c>SemaphoreSlim</c> gating; both observers
/// see the file present on completion.</para>
/// </remarks>
public interface IWhisperRuntimeProvisioner
{
    /// <summary>Local directory under which provisioned variants are cached.
    /// Layout: <c>{CacheDirectory}/runtimes/&lt;flavor&gt;/&lt;rid&gt;/&lt;file&gt;</c>.
    /// On Windows the default is <c>%LOCALAPPDATA%\FieldCure\WhisperRuntimes\</c>.</summary>
    string CacheDirectory { get; }

    /// <summary>Returns <see langword="true"/> if every file declared by the manifest for
    /// <paramref name="variant"/> on the current RID is already present in the cache.
    /// Hash is NOT re-verified here — verification happens at download time, and the
    /// atomic <c>File.Move</c> guarantees only verified bytes ever land in the cache.</summary>
    /// <remarks>This method may perform a lightweight manifest read on first call but
    /// is not expected to do network I/O. Callers should invoke it before
    /// <see cref="ProvisionAsync"/> to skip the async path when the cache is warm.</remarks>
    bool IsProvisioned(WhisperRuntimeVariant variant);

    /// <summary>Returns the loaded manifest. Performs a one-time fetch (online mode) or
    /// load (offline mode) on first access. Callers use this to read
    /// <see cref="WhisperRuntimeManifestVariant.MinDriverVersion"/> for activation gating
    /// and the manifest's <c>whisperNetRuntimeVersion</c> for compatibility checks.</summary>
    /// <param name="cancellationToken">Cancellation token for the manifest fetch.</param>
    Task<WhisperRuntimeManifest> GetManifestAsync(CancellationToken cancellationToken = default);

    /// <summary>Ensures every file declared by the manifest for <paramref name="variant"/>
    /// is present in the cache, downloading any missing ones. Idempotent.</summary>
    /// <param name="variant">Runtime variant to provision.</param>
    /// <param name="cancellationToken">Propagated through HTTP and file I/O.
    /// Mid-download cancel orphans a <c>.download.&lt;guid&gt;</c> temp file; subsequent
    /// retries clean it up implicitly.</param>
    /// <param name="progress">Optional reporter; fires per-file (Resolving, Verifying)
    /// and chunked during Downloading (~256 KB granularity). Bounded — won't flood.</param>
    /// <exception cref="WhisperRuntimeIntegrityException">A downloaded file's SHA-256
    /// did not match the manifest. The temp file is deleted before propagation.</exception>
    /// <exception cref="WhisperRuntimeMissingException">Offline override mode and the
    /// pre-staged directory does not contain every required file.</exception>
    Task ProvisionAsync(
        WhisperRuntimeVariant variant,
        CancellationToken cancellationToken = default,
        IProgress<WhisperRuntimeProgress>? progress = null);
}
