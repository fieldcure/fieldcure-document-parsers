using FieldCure.DocumentParsers.Audio.Conversion;
using NAudio.Wave;

namespace FieldCure.DocumentParsers.Audio.Tests;

/// <summary>
/// Unit tests for <see cref="AudioConverter"/> — the input-format normalization step that
/// runs before Whisper transcription. Covers the WAVE_FORMAT_EXTENSIBLE re-labelling path
/// added in the EXTENSIBLE fix; transcription itself is covered by the opt-in integration
/// suites.
/// </summary>
[TestClass]
public class AudioConverterTests
{
    /// <summary>
    /// Verifies the converter accepts a multi-channel WAVE_FORMAT_EXTENSIBLE PCM input —
    /// the format used by whisper.net's <c>multichannel.wav</c> sample and by most
    /// modern Windows capture pipelines — and produces a 16 kHz mono PCM output without
    /// any byte-level conversion of the underlying samples.
    /// </summary>
    [TestMethod]
    public void ToPcm16kMono_AcceptsExtensiblePcmInput()
    {
        var input = BuildExtensiblePcmWav(
            channels: 2,
            sampleRate: 44100,
            bitsPerSample: 16,
            seconds: 1);

        using var output = AudioConverter.ToPcm16kMono(new MemoryStream(input), ".wav");
        using var reader = new WaveFileReader(output);

        Assert.AreEqual(16000, reader.WaveFormat.SampleRate);
        Assert.AreEqual(1, reader.WaveFormat.Channels);
        Assert.AreEqual(16, reader.WaveFormat.BitsPerSample);
        Assert.AreEqual(WaveFormatEncoding.Pcm, reader.WaveFormat.Encoding);
        Assert.IsTrue(reader.Length > 0, "Resampled output should contain audio data.");
    }

    /// <summary>
    /// Verifies the converter still works for a plain (non-extensible) PCM WAV — the
    /// regression guard that the normalization step is a no-op for sources that already
    /// expose a standard format tag.
    /// </summary>
    [TestMethod]
    public void ToPcm16kMono_AcceptsStandardPcmInput()
    {
        var input = BuildStandardPcmWav(
            channels: 1,
            sampleRate: 22050,
            bitsPerSample: 16,
            seconds: 1);

        using var output = AudioConverter.ToPcm16kMono(new MemoryStream(input), ".wav");
        using var reader = new WaveFileReader(output);

        Assert.AreEqual(16000, reader.WaveFormat.SampleRate);
        Assert.AreEqual(1, reader.WaveFormat.Channels);
        Assert.AreEqual(16, reader.WaveFormat.BitsPerSample);
        Assert.AreEqual(WaveFormatEncoding.Pcm, reader.WaveFormat.Encoding);
    }

    /// <summary>
    /// Builds a minimal silent WAVE_FORMAT_EXTENSIBLE PCM WAV with the given channel
    /// layout and duration. SubFormat GUID is fixed to KSDATAFORMAT_SUBTYPE_PCM, channel
    /// mask is set to a generic stereo/multi-channel layout (0x3 for two channels,
    /// otherwise SPEAKER_FRONT_CENTER + SPEAKER_FRONT_LEFT + ...).
    /// </summary>
    /// <param name="channels">Number of channels.</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="bitsPerSample">Bits per sample (typically 16).</param>
    /// <param name="seconds">Duration in seconds.</param>
    /// <returns>WAV file bytes.</returns>
    private static byte[] BuildExtensiblePcmWav(short channels, int sampleRate, short bitsPerSample, int seconds)
    {
        var blockAlign = (short)(channels * (bitsPerSample / 8));
        var byteRate = sampleRate * blockAlign;
        var dataLength = byteRate * seconds;

        // KSDATAFORMAT_SUBTYPE_PCM: {00000001-0000-0010-8000-00aa00389b71}
        var subFormatPcm = new Guid("00000001-0000-0010-8000-00aa00389b71").ToByteArray();

        const int fmtChunkSize = 40; // 18 (cbSize-prefixed common block) + 22 extension bytes
        var totalSize = 4 /* WAVE */ + (8 + fmtChunkSize) + (8 + dataLength);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write("RIFF"u8);
        writer.Write(totalSize);
        writer.Write("WAVE"u8);

        writer.Write("fmt "u8);
        writer.Write(fmtChunkSize);
        writer.Write(unchecked((short)0xFFFE)); // formatTag = WAVE_FORMAT_EXTENSIBLE
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write((short)22);                // cbSize
        writer.Write(bitsPerSample);            // validBitsPerSample
        writer.Write(GetGenericChannelMask(channels));
        writer.Write(subFormatPcm);

        writer.Write("data"u8);
        writer.Write(dataLength);
        writer.Write(new byte[dataLength]);

        return stream.ToArray();
    }

    /// <summary>
    /// Builds a minimal silent WAV with the standard <c>WAVE_FORMAT_PCM</c> (formatTag = 1)
    /// header. Used as the non-extensible regression guard.
    /// </summary>
    /// <param name="channels">Number of channels.</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="bitsPerSample">Bits per sample (typically 16).</param>
    /// <param name="seconds">Duration in seconds.</param>
    /// <returns>WAV file bytes.</returns>
    private static byte[] BuildStandardPcmWav(short channels, int sampleRate, short bitsPerSample, int seconds)
    {
        var blockAlign = (short)(channels * (bitsPerSample / 8));
        var byteRate = sampleRate * blockAlign;
        var dataLength = byteRate * seconds;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write("RIFF"u8);
        writer.Write(36 + dataLength);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write("data"u8);
        writer.Write(dataLength);
        writer.Write(new byte[dataLength]);

        return stream.ToArray();
    }

    /// <summary>
    /// Returns a generic Windows channel mask for the given channel count
    /// (front-left + front-right + ... up to surround). Exact mask value is irrelevant
    /// for these tests — the converter ignores spatial layout — but a non-zero mask
    /// keeps the WAV header well-formed for any decoder that validates it.
    /// </summary>
    /// <param name="channels">Channel count.</param>
    /// <returns>Channel mask suitable for the EXTENSIBLE fmt chunk.</returns>
    private static int GetGenericChannelMask(short channels)
    {
        // SPEAKER_FRONT_LEFT(0x1) + SPEAKER_FRONT_RIGHT(0x2) +
        // SPEAKER_FRONT_CENTER(0x4) + SPEAKER_LOW_FREQUENCY(0x8) + ...
        return channels switch
        {
            1 => 0x4,                  // mono → front center
            2 => 0x3,                  // stereo → FL + FR
            3 => 0x7,                  // FL + FR + FC
            4 => 0x33,                 // FL + FR + back L/R
            6 => 0x3F,                 // 5.1
            _ => (1 << channels) - 1,  // first N speaker bits
        };
    }
}
