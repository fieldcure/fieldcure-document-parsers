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
        var reader = OpenWaveStream(data, extension);
        // NormalizeFormatForResampler may return `reader` itself or a wrapper that
        // owns it — either way `normalized` is the single disposable that covers
        // the chain.
        using var normalized = NormalizeFormatForResampler(reader);

        var output = new MemoryStream();
        var outputFormat = new WaveFormat(16000, 16, 1);
        using var resampler = new MediaFoundationResampler(normalized, outputFormat)
        {
            ResamplerQuality = 60
        };

        WaveFileWriter.WriteWavFileToStream(output, resampler);
        output.Position = 0;
        return output;
    }

    /// <summary>
    /// SubFormat GUID for <c>KSDATAFORMAT_SUBTYPE_PCM</c> in <c>WAVE_FORMAT_EXTENSIBLE</c>.
    /// </summary>
    private static readonly Guid s_pcmSubFormat = new("00000001-0000-0010-8000-00aa00389b71");

    /// <summary>
    /// SubFormat GUID for <c>KSDATAFORMAT_SUBTYPE_IEEE_FLOAT</c> in <c>WAVE_FORMAT_EXTENSIBLE</c>.
    /// </summary>
    private static readonly Guid s_ieeeFloatSubFormat = new("00000003-0000-0010-8000-00aa00389b71");

    /// <summary>
    /// Ensures the wave stream advertises a <see cref="WaveFormatEncoding.Pcm"/> or
    /// <see cref="WaveFormatEncoding.IeeeFloat"/> format tag. <see cref="MediaFoundationResampler"/>
    /// rejects <see cref="WaveFormatEncoding.Extensible"/> (formatTag <c>0xFFFE</c>) at construction
    /// even when the underlying samples are bit-identical to standard PCM. Common multi-channel
    /// captures (e.g. whisper.net's <c>multichannel.wav</c> sample) ship as
    /// <c>WAVE_FORMAT_EXTENSIBLE</c> with <c>KSDATAFORMAT_SUBTYPE_PCM</c>, so re-labelling the
    /// <see cref="WaveFormat"/> is sufficient — no byte conversion required.
    /// </summary>
    /// <param name="source">Decoded wave stream that may carry an extensible format tag.</param>
    /// <returns>Either <paramref name="source"/> unchanged, or a wrapper exposing a standard
    /// <see cref="WaveFormat"/> over the same byte stream.</returns>
    /// <exception cref="NotSupportedException">Thrown when the extensible SubFormat GUID does not
    /// map to a format the downstream resampler accepts.</exception>
    private static WaveStream NormalizeFormatForResampler(WaveStream source)
    {
        if (source.WaveFormat.Encoding != WaveFormatEncoding.Extensible)
        {
            return source;
        }

        if (source.WaveFormat is not WaveFormatExtensible extensible)
        {
            // Encoding tag says extensible but the format object is the basic WaveFormat — we have
            // no SubFormat to inspect. Conservative fallback: assume PCM, which matches every
            // real-world EXTENSIBLE producer this package has encountered.
            return new RelabelledWaveStream(
                source,
                new WaveFormat(source.WaveFormat.SampleRate, source.WaveFormat.BitsPerSample, source.WaveFormat.Channels));
        }

        WaveFormat standardFormat;
        if (extensible.SubFormat == s_pcmSubFormat)
        {
            standardFormat = new WaveFormat(extensible.SampleRate, extensible.BitsPerSample, extensible.Channels);
        }
        else if (extensible.SubFormat == s_ieeeFloatSubFormat)
        {
            standardFormat = WaveFormat.CreateIeeeFloatWaveFormat(extensible.SampleRate, extensible.Channels);
        }
        else
        {
            throw new NotSupportedException(
                $"WAVE_FORMAT_EXTENSIBLE SubFormat {extensible.SubFormat} is not a PCM or IEEE float layout. " +
                $"This audio container needs a decoder pass before resampling, which is not implemented.");
        }

        return new RelabelledWaveStream(source, standardFormat);
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
    /// Thin <see cref="WaveStream"/> wrapper that overrides only the advertised
    /// <see cref="WaveFormat"/> and forwards every other operation to an inner stream. Used to
    /// re-label <see cref="WaveFormatEncoding.Extensible"/> sources whose underlying samples are
    /// already PCM- or IEEE-float-compatible, without copying or converting any bytes.
    /// </summary>
    private sealed class RelabelledWaveStream : WaveStream
    {
        private readonly WaveStream _inner;
        private readonly WaveFormat _format;

        /// <summary>
        /// Creates a wrapper that exposes <paramref name="format"/> while delegating all reads,
        /// length, and position handling to <paramref name="inner"/>.
        /// </summary>
        /// <param name="inner">Underlying wave stream whose bytes are already in the layout
        /// described by <paramref name="format"/>.</param>
        /// <param name="format">Replacement format the wrapper advertises to consumers.</param>
        public RelabelledWaveStream(WaveStream inner, WaveFormat format)
        {
            _inner = inner;
            _format = format;
        }

        /// <inheritdoc />
        public override WaveFormat WaveFormat => _format;

        /// <inheritdoc />
        public override long Length => _inner.Length;

        /// <inheritdoc />
        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
            => _inner.Read(buffer, offset, count);

        /// <summary>
        /// Disposes the inner stream when the wrapper is disposed.
        /// </summary>
        /// <param name="disposing">Whether managed resources should be disposed.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
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
