using System.Runtime.InteropServices;
using NAudio.Vorbis;
using NAudio.Wave;

namespace FieldCure.DocumentParsers.Audio.Conversion;

/// <summary>
/// Converts supported audio containers to 16 kHz mono 16-bit PCM WAV.
/// </summary>
public static class AudioConverter
{
    /// <summary>
    /// File extensions to probe when the caller did not provide a reliable source extension.
    /// </summary>
    private static readonly string[] ProbeExtensions =
    [
        ".wav",
        ".mp3",
        ".ogg",
        ".m4a",
        ".flac",
        ".webm"
    ];

    /// <summary>
    /// Converts the input audio stream to a seekable PCM WAV stream suitable for Whisper.
    /// </summary>
    /// <param name="inputStream">Source audio stream.</param>
    /// <param name="extension">Optional file extension used to choose the first decoder to try.</param>
    /// <returns>Seekable 16 kHz mono 16-bit PCM WAV stream.</returns>
    public static Stream ToPcm16kMono(Stream inputStream, string? extension = null)
    {
        ArgumentNullException.ThrowIfNull(inputStream);

        var data = ReadAllBytes(inputStream);
        using var reader = OpenWaveStream(data, extension);

        var output = new MemoryStream();
        var outputFormat = new WaveFormat(16000, 16, 1);
        using var resampler = new MediaFoundationResampler(reader, outputFormat)
        {
            ResamplerQuality = 60
        };

        WaveFileWriter.WriteWavFileToStream(output, resampler);
        output.Position = 0;
        return output;
    }

    /// <summary>
    /// Opens an audio decoder for the supplied bytes by trying the preferred format first.
    /// </summary>
    /// <param name="data">Audio bytes to decode.</param>
    /// <param name="extension">Optional source extension.</param>
    /// <returns>Wave stream for the decoded source audio.</returns>
    private static WaveStream OpenWaveStream(byte[] data, string? extension)
    {
        Exception? lastException = null;
        foreach (var candidate in GetProbeOrder(extension))
        {
            try
            {
                return candidate switch
                {
                    ".wav" => new WaveFileReader(new MemoryStream(data, writable: false)),
                    ".mp3" => new Mp3FileReader(new MemoryStream(data, writable: false)),
                    ".ogg" => new VorbisWaveReader(new MemoryStream(data, writable: false)),
                    _ => OpenMediaFoundationReader(data, candidate)
                };
            }
            catch (Exception ex) when (ex is InvalidDataException
                or COMException
                or NotSupportedException
                or FormatException
                or EndOfStreamException
                or IndexOutOfRangeException)
            {
                lastException = ex;
            }
        }

        throw new InvalidOperationException(
            "Audio data could not be decoded. Provide a supported MP3, WAV, M4A, OGG, FLAC, or WebM file.",
            lastException);
    }

    /// <summary>
    /// Produces the decoder probe order, starting with the caller-provided extension when present.
    /// </summary>
    /// <param name="extension">Optional caller-provided extension.</param>
    /// <returns>Ordered extension candidates.</returns>
    private static IEnumerable<string> GetProbeOrder(string? extension)
    {
        var normalized = NormalizeExtension(extension);
        if (normalized is not null)
        {
            yield return normalized;
        }

        foreach (var candidate in ProbeExtensions)
        {
            if (!string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase))
            {
                yield return candidate;
            }
        }
    }

    /// <summary>
    /// Opens a Media Foundation reader over a temporary file for container formats that need file paths.
    /// </summary>
    /// <param name="data">Audio bytes to write to a temporary file.</param>
    /// <param name="extension">Extension used for the temporary file.</param>
    /// <returns>Wave stream that deletes the temporary file when disposed.</returns>
    private static WaveStream OpenMediaFoundationReader(byte[] data, string extension)
    {
        var tempPath = Path.Combine(
            Path.GetTempPath(),
            "fieldcure-audio-" + Guid.NewGuid().ToString("N") + extension);

        File.WriteAllBytes(tempPath, data);
        try
        {
            return new TemporaryFileWaveStream(new MediaFoundationReader(tempPath), tempPath);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    /// <summary>
    /// Normalizes a file extension to lowercase with a leading dot.
    /// </summary>
    /// <param name="extension">Extension value to normalize.</param>
    /// <returns>Normalized extension, or null when no extension was supplied.</returns>
    private static string? NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        var trimmed = extension.Trim();
        return trimmed.StartsWith(".", StringComparison.Ordinal)
            ? trimmed.ToLowerInvariant()
            : "." + trimmed.ToLowerInvariant();
    }

    /// <summary>
    /// Copies a stream into a byte array from the beginning when the stream supports seeking.
    /// </summary>
    /// <param name="stream">Stream to copy.</param>
    /// <returns>Copied stream bytes.</returns>
    private static byte[] ReadAllBytes(Stream stream)
    {
        if (stream is MemoryStream memoryStream && memoryStream.TryGetBuffer(out var buffer))
        {
            return buffer.AsSpan(0, (int)memoryStream.Length).ToArray();
        }

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    /// <summary>
    /// Deletes a temporary file without surfacing cleanup failures to callers.
    /// </summary>
    /// <param name="path">Temporary file path.</param>
    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup for temporary decode files.
        }
    }

    /// <summary>
    /// Wraps a wave stream and removes its backing temporary file on disposal.
    /// </summary>
    private sealed class TemporaryFileWaveStream : WaveStream
    {
        private readonly WaveStream _inner;
        private readonly string _tempPath;

        /// <summary>
        /// Creates a temporary-file-backed wave stream wrapper.
        /// </summary>
        /// <param name="inner">Inner wave stream.</param>
        /// <param name="tempPath">Temporary file to delete when disposed.</param>
        public TemporaryFileWaveStream(WaveStream inner, string tempPath)
        {
            _inner = inner;
            _tempPath = tempPath;
        }

        public override WaveFormat WaveFormat => _inner.WaveFormat;

        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
            => _inner.Read(buffer, offset, count);

        /// <summary>
        /// Releases the inner stream and removes the temporary file.
        /// </summary>
        /// <param name="disposing">Whether managed resources should be disposed.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                TryDelete(_tempPath);
            }

            base.Dispose(disposing);
        }
    }
}
