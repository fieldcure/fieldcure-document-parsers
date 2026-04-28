using System.Text.Json;

namespace FieldCure.DocumentParsers.Audio.Runtime;

/// <summary>
/// Deserialized form of <c>manifest.json</c> as published in
/// <see href="https://github.com/fieldcure/fieldcure-whisper-runtimes/releases">fieldcure-whisper-runtimes</see>.
/// Manifest schema is documented in that repo's README and the design spec at
/// <c>poc/audio-runtime-download/README.md</c>.
/// </summary>
public sealed class WhisperRuntimeManifest
{
    /// <summary>Manifest schema version. Only <c>1</c> is recognized in v0.3.</summary>
    public int SchemaVersion { get; init; }

    /// <summary>Whisper.net version this manifest's binaries were built against
    /// (e.g., <c>"1.9.0"</c>). Compared at startup against the version of
    /// <c>Whisper.net</c> linked into the consumer process; mismatch produces a
    /// warning log, not a hard fail.</summary>
    public string WhisperNetRuntimeVersion { get; init; } = "";

    /// <summary>One entry per variant flavor (<c>cpu</c>, <c>cuda</c>, <c>vulkan</c>).</summary>
    public IReadOnlyDictionary<string, WhisperRuntimeManifestVariant> Variants { get; init; }
        = new Dictionary<string, WhisperRuntimeManifestVariant>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Parses a manifest JSON document. Throws <see cref="JsonException"/> on
    /// structural errors and <see cref="NotSupportedException"/> on unknown schema versions.</summary>
    public static WhisperRuntimeManifest Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var schema = root.GetProperty("schemaVersion").GetInt32();
        if (schema != 1)
        {
            throw new NotSupportedException(
                $"Manifest schemaVersion {schema} is not supported by this Audio package. " +
                $"Expected 1.");
        }

        var variants = new Dictionary<string, WhisperRuntimeManifestVariant>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("variants", out var variantsElem))
        {
            foreach (var variantProp in variantsElem.EnumerateObject())
            {
                variants[variantProp.Name] = WhisperRuntimeManifestVariant.Parse(variantProp.Value);
            }
        }

        return new WhisperRuntimeManifest
        {
            SchemaVersion = schema,
            WhisperNetRuntimeVersion = root.GetProperty("whisperNetRuntimeVersion").GetString() ?? "",
            Variants = variants,
        };
    }

    /// <summary>Returns the variant entry for <paramref name="variant"/>, or
    /// <see langword="null"/> if the manifest does not declare it.</summary>
    public WhisperRuntimeManifestVariant? GetVariant(WhisperRuntimeVariant variant)
    {
        var key = variant.ToString().ToLowerInvariant();
        return Variants.TryGetValue(key, out var spec) ? spec : null;
    }
}

/// <summary>
/// Per-variant manifest section. Carries optional policy fields
/// (<see cref="MinDriverVersion"/>) and per-RID file lists.
/// </summary>
public sealed class WhisperRuntimeManifestVariant
{
    /// <summary>Optional minimum driver version policy (currently used only by
    /// the <c>cuda</c> variant). Encoded in <c>cuDriverGetVersion</c> integer form
    /// (e.g., <c>12000</c> = CUDA 12.0). <see langword="null"/> means "no policy".</summary>
    public int? MinDriverVersion { get; init; }

    /// <summary>File lists keyed by Runtime Identifier (e.g., <c>"win-x64"</c>).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<WhisperRuntimeManifestFile>> Platforms { get; init; }
        = new Dictionary<string, IReadOnlyList<WhisperRuntimeManifestFile>>(StringComparer.OrdinalIgnoreCase);

    internal static WhisperRuntimeManifestVariant Parse(JsonElement v)
    {
        int? minDriverVersion = null;
        var platforms = new Dictionary<string, IReadOnlyList<WhisperRuntimeManifestFile>>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in v.EnumerateObject())
        {
            if (string.Equals(prop.Name, "minDriverVersion", StringComparison.OrdinalIgnoreCase))
            {
                minDriverVersion = prop.Value.GetInt32();
                continue;
            }

            // Treat every other property as a RID-keyed file list.
            if (prop.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var files = new List<WhisperRuntimeManifestFile>();
            foreach (var fileElem in prop.Value.EnumerateArray())
            {
                files.Add(WhisperRuntimeManifestFile.Parse(fileElem));
            }
            platforms[prop.Name] = files;
        }

        return new WhisperRuntimeManifestVariant
        {
            MinDriverVersion = minDriverVersion,
            Platforms = platforms,
        };
    }
}

/// <summary>
/// One file entry inside a per-RID file list. The repo does NOT redistribute
/// <c>nvcuda.dll</c>, <c>vulkan-1.dll</c>, or any other driver-shipped library —
/// those are loaded from the host's <c>System32</c>.
/// </summary>
public sealed class WhisperRuntimeManifestFile
{
    /// <summary>File name as it should appear under
    /// <c>runtimes/&lt;flavor&gt;/&lt;rid&gt;/</c> in the cache.</summary>
    public string Name { get; init; } = "";

    /// <summary>Direct download URL (typically a GitHub Releases asset URL).</summary>
    public string Url { get; init; } = "";

    /// <summary>Lowercase hex SHA-256 of the file's bytes, used for integrity verification.</summary>
    public string Sha256 { get; init; } = "";

    /// <summary>Authoritative file size in bytes. Used for sanity checks; the streaming
    /// downloader prefers the HTTP <c>Content-Length</c> header for progress reporting.</summary>
    public long Bytes { get; init; }

    /// <summary>If true, the file is governed by NVIDIA CUDA Toolkit EULA Attachment A.
    /// The provisioner emits a one-line stderr attribution on first download of any
    /// such file, satisfying the redistribution-notice expectation. See
    /// <c>fieldcure-whisper-runtimes/NOTICE</c> for full text.</summary>
    public bool NvidiaRedist { get; init; }

    internal static WhisperRuntimeManifestFile Parse(JsonElement f)
    {
        return new WhisperRuntimeManifestFile
        {
            Name = f.GetProperty("name").GetString() ?? "",
            Url = f.GetProperty("url").GetString() ?? "",
            Sha256 = f.GetProperty("sha256").GetString() ?? "",
            Bytes = f.TryGetProperty("bytes", out var bytesElem) ? bytesElem.GetInt64() : 0,
            NvidiaRedist = f.TryGetProperty("nvidiaRedist", out var nvElem) && nvElem.GetBoolean(),
        };
    }
}
