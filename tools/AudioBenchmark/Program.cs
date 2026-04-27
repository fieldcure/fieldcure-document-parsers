using System.Diagnostics;
using System.Globalization;
using System.Text;
using FieldCure.DocumentParsers.Audio;
using FieldCure.DocumentParsers.Audio.Conversion;
using FieldCure.DocumentParsers.Audio.Transcription;
using NAudio.Wave;
using Whisper.net.Ggml;

namespace FieldCure.Tools.AudioBenchmark;

/// <summary>
/// Runs a parameterized benchmark across audio files and Whisper model sizes
/// to validate the recommendation matrix in <c>WhisperEnvironment</c>.
/// </summary>
/// <remarks>
/// This is an internal diagnostic tool — not shipped in release artifacts.
/// Results are written to CSV with immediate flush to survive interruption
/// during long batch runs. Driven directly via <see cref="WhisperTranscriber"/>
/// rather than <c>LazyAudioTranscriber</c> so each model size is exercised
/// explicitly, with a fresh transcriber per model to isolate cold-start cost.
/// </remarks>
internal static class Program
{
    private static readonly string[] DefaultModels = ["Tiny", "Base", "Small", "Medium", "Large"];

    private static readonly string[] SupportedExtensions =
        [".mp3", ".wav", ".m4a", ".ogg", ".flac", ".webm"];

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        if (args[0] == "--probe-api")
        {
            return ApiProbe.Run();
        }

        BenchmarkOptions options;
        try
        {
            options = BenchmarkOptions.Parse(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Argument error: {ex.Message}");
            PrintUsage();
            return 1;
        }

        if (!Directory.Exists(options.InputDirectory))
        {
            Console.Error.WriteLine($"Input directory not found: {options.InputDirectory}");
            return 1;
        }

        EmitProbeBanner(options);

        var files = Directory.EnumerateFiles(options.InputDirectory)
            .Where(p => SupportedExtensions.Contains(Path.GetExtension(p).ToLowerInvariant()))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
        {
            Console.Error.WriteLine($"No audio files found in {options.InputDirectory}.");
            Console.Error.WriteLine($"Supported extensions: {string.Join(", ", SupportedExtensions)}");
            return 1;
        }

        Console.Error.WriteLine($"[Benchmark] {files.Count} audio file(s) × {options.Models.Count} model(s) = {files.Count * options.Models.Count} measurements queued.");
        Console.Error.WriteLine($"[Benchmark] Output: {options.OutputCsv}");
        Console.Error.WriteLine($"[Benchmark] Transcripts: {options.TranscriptsDirectory}");
        Console.Error.WriteLine();

        Directory.CreateDirectory(options.TranscriptsDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(options.OutputCsv) ?? ".");

        await using var csv = new CsvWriter(options.OutputCsv);
        var summaries = new List<ModelSummary>();

        foreach (var modelName in options.Models)
        {
            if (!Enum.TryParse<WhisperModelSize>(modelName, ignoreCase: true, out var modelSize))
            {
                Console.Error.WriteLine($"[Benchmark] Skipping unknown model: {modelName}");
                continue;
            }

            var summary = await RunModelAsync(modelSize, files, options, csv);
            summaries.Add(summary);
        }

        EmitSummary(summaries);
        return 0;
    }

    /// <summary>
    /// Drives one model over the full file set: cold-start warmup, then per-file
    /// warm transcription with measurement. Disposes the transcriber before
    /// returning so the next model gets a clean memory baseline.
    /// </summary>
    private static async Task<ModelSummary> RunModelAsync(
        WhisperModelSize modelSize,
        List<string> files,
        BenchmarkOptions options,
        CsvWriter csv)
    {
        var directMode = options.NoContext
            || options.Fallback != DirectWhisperRunner.FallbackMode.None
            || options.GgmlOverride is not null;
        var directLabel = directMode
            ? $" (direct{(options.NoContext ? ", no-context" : "")}{(options.Fallback != DirectWhisperRunner.FallbackMode.None ? $", fallback={options.Fallback}" : "")}{(options.GgmlOverride is not null ? $", ggml={options.GgmlOverride}" : "")})"
            : "";
        Console.Error.WriteLine($"[{modelSize}] Initializing transcriber{directLabel}.");

        string modelPath;
        if (options.GgmlOverride is { } ggmlType)
        {
            modelPath = await new WhisperModelProvider().GetModelPathByGgmlTypeAsync(ggmlType, CancellationToken.None);
        }
        else
        {
            modelPath = await new WhisperModelProvider().GetModelPathAsync(modelSize, CancellationToken.None);
        }

        IAudioTranscriber transcriber = directMode
            ? new DirectWhisperRunner(
                modelPath: modelPath,
                noContext: options.NoContext,
                fallback: options.Fallback)
            : new WhisperTranscriber();

        var coldStartSeconds = 0.0;
        try
        {
            // Pre-convert the warmup file once to keep the cold-start
            // measurement focused on Whisper init, not container decode.
            if (options.Warmup && files.Count > 0)
            {
                var warmupPcm = ConvertToPcm(files[0]);
                Console.Error.WriteLine($"[{modelSize}] Cold-start warmup using {Path.GetFileName(files[0])}.");
                var coldSw = Stopwatch.StartNew();
                await DiscardAsync(transcriber.TranscribeAsync(
                    new MemoryStream(warmupPcm), BuildOptions(modelSize, files[0])));
                coldSw.Stop();
                coldStartSeconds = coldSw.Elapsed.TotalSeconds;
                Console.Error.WriteLine($"[{modelSize}] Cold-start: {coldStartSeconds:F2}s");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[{modelSize}] Cold-start failed: {ex.Message}");
            await transcriber.DisposeAsync();
            return new ModelSummary(modelSize, 0, 0.0, 0.0, 0L, Errored: true);
        }

        var fileCount = 0;
        var rtfSum = 0.0;
        var transcriptionSecondsSum = 0.0;
        long peakWorkingSetMax = 0;

        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            try
            {
                var row = await MeasureFileAsync(
                    file, transcriber, modelSize,
                    coldStartSecondsForFirst: i == 0 ? coldStartSeconds : 0.0,
                    options);

                csv.WriteRow(row);
                fileCount++;
                rtfSum += row.RtfOrZero;
                transcriptionSecondsSum += row.TranscriptionSeconds;
                if (row.PeakWorkingSetMb > peakWorkingSetMax)
                    peakWorkingSetMax = row.PeakWorkingSetMb;

                EmitSanityWarnings(row);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[{modelSize}] {Path.GetFileName(file)} ERROR: {ex.Message}");
                csv.WriteRow(BenchmarkRow.Failed(file, modelSize, ex.Message));
            }
        }

        await transcriber.DisposeAsync();

        var avgRtf = fileCount > 0 ? rtfSum / fileCount : 0.0;
        return new ModelSummary(modelSize, fileCount, avgRtf, transcriptionSecondsSum, peakWorkingSetMax, Errored: false);
    }

    private static async Task<BenchmarkRow> MeasureFileAsync(
        string file,
        IAudioTranscriber transcriber,
        WhisperModelSize modelSize,
        double coldStartSecondsForFirst,
        BenchmarkOptions options)
    {
        var fileName = Path.GetFileName(file);
        Console.Error.WriteLine($"[{modelSize}] Measuring {fileName}.");

        var pcmBytes = ConvertToPcm(file);
        var durationSeconds = ComputeWavDurationSeconds(pcmBytes);

        // Reset peak so this file's measurement is isolated. Empty() is the
        // documented way to reset PeakWorkingSet64; subsequent reads pick up
        // the new high-water mark for just this transcription.
        Process.GetCurrentProcess().Refresh();

        var sw = Stopwatch.StartNew();
        var transcript = await CollectAsync(transcriber.TranscribeAsync(
            new MemoryStream(pcmBytes), BuildOptions(modelSize, file)));
        sw.Stop();

        var process = Process.GetCurrentProcess();
        process.Refresh();
        var peakWorkingSetMb = process.PeakWorkingSet64 / (1024L * 1024L);

        var transcriptText = transcript.ToString();

        // Persist transcript for qualitative review and downstream WER/CER reuse.
        var suffix = string.IsNullOrEmpty(options.TranscriptSuffix) ? "" : $"__{options.TranscriptSuffix}";
        var transcriptPath = Path.Combine(
            options.TranscriptsDirectory,
            $"{Path.GetFileNameWithoutExtension(file)}__{modelSize}{suffix}.txt");
        await File.WriteAllTextAsync(transcriptPath, transcriptText, Encoding.UTF8);

        // Optional ground-truth pairing: <basename>.gt.txt next to the audio.
        var gtPath = Path.Combine(
            Path.GetDirectoryName(file)!,
            Path.GetFileNameWithoutExtension(file) + ".gt.txt");
        var gtAvailable = File.Exists(gtPath);
        double? wer = null;
        double? cer = null;
        int? gtWordCount = null;
        if (gtAvailable)
        {
            var reference = await File.ReadAllTextAsync(gtPath, Encoding.UTF8);
            wer = TranscriptionAccuracy.ComputeWer(reference, transcriptText);
            cer = TranscriptionAccuracy.ComputeCer(reference, transcriptText);
            gtWordCount = TranscriptionAccuracy.CountWords(reference);
        }

        return new BenchmarkRow
        {
            Config = DescribeConfig(options),
            Timestamp = DateTime.UtcNow,
            FileName = fileName,
            AudioDurationSeconds = durationSeconds,
            ModelSize = modelSize,
            ColdStartSeconds = coldStartSecondsForFirst,
            TranscriptionSeconds = sw.Elapsed.TotalSeconds,
            PeakWorkingSetMb = peakWorkingSetMb,
            TranscriptCharCount = transcriptText.Length,
            RuntimeUsedFlags = DescribeRuntime(),
            Error = string.Empty,
            GtAvailable = gtAvailable,
            Wer = wer,
            Cer = cer,
            GtWordCount = gtWordCount,
        };
    }

    /// <summary>
    /// Derives a stable, short config label from the active CLI options so
    /// every row in the output CSV carries a self-describing experiment arm.
    /// Order of precedence reflects what most affects measurement semantics:
    /// model variant (<c>--ggml</c>) outranks decoder tweaks
    /// (<c>--no-context</c> / <c>--fallback</c>), which outrank the implicit
    /// default.
    /// </summary>
    private static string DescribeConfig(BenchmarkOptions options)
    {
        if (options.GgmlOverride is { } g)
            return $"ggml_{g.ToString().ToLowerInvariant()}";
        var parts = new List<string>();
        if (options.NoContext) parts.Add("no_context");
        if (options.Fallback != DirectWhisperRunner.FallbackMode.None)
            parts.Add($"fallback_{options.Fallback.ToString().ToLowerInvariant()}");
        return parts.Count == 0 ? "default" : string.Join("+", parts);
    }

    private static AudioExtractionOptions BuildOptions(WhisperModelSize modelSize, string filePath)
        => new()
        {
            ModelSize = modelSize,
            SourceExtension = Path.GetExtension(filePath).ToLowerInvariant(),
            // Auto-detect language; benchmark spans Korean and English.
            Language = null,
            IncludeTimestamps = false,
            IncludeConfidence = false,
        };

    /// <summary>
    /// Decodes any supported container to 16 kHz mono 16-bit PCM WAV bytes,
    /// reusing the production conversion path so measurements reflect what
    /// real consumers see.
    /// </summary>
    private static byte[] ConvertToPcm(string path)
    {
        using var input = File.OpenRead(path);
        using var pcm = AudioConverter.ToPcm16kMono(input, Path.GetExtension(path));
        if (pcm is MemoryStream ms) return ms.ToArray();
        using var copy = new MemoryStream();
        pcm.CopyTo(copy);
        return copy.ToArray();
    }

    /// <summary>
    /// Reads the duration of a 16 kHz mono 16-bit PCM WAV byte buffer.
    /// </summary>
    private static double ComputeWavDurationSeconds(byte[] wavBytes)
    {
        using var ms = new MemoryStream(wavBytes, writable: false);
        using var reader = new WaveFileReader(ms);
        return reader.TotalTime.TotalSeconds;
    }

    private static async Task<StringBuilder> CollectAsync(IAsyncEnumerable<TranscriptSegment> segments)
    {
        var sb = new StringBuilder();
        await foreach (var segment in segments)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(segment.Text);
        }
        return sb;
    }

    private static async Task DiscardAsync(IAsyncEnumerable<TranscriptSegment> segments)
    {
        await foreach (var _ in segments) { /* intentionally discarded */ }
    }

    private static string DescribeRuntime()
    {
        // Whisper.net does not expose its resolved runtime at the IAudioTranscriber
        // contract level. We record the Probe flags so analysis can correlate
        // measured RTF with CUDA/Vulkan availability without extra plumbing.
        var probe = WhisperEnvironment.Probe();
        return $"reported_cuda={probe.CudaAvailable.ToString().ToLowerInvariant()};reported_vulkan={probe.VulkanAvailable.ToString().ToLowerInvariant()}";
    }

    private static void EmitProbeBanner(BenchmarkOptions options)
    {
        var probe = WhisperEnvironment.Probe();
        var recommended = WhisperEnvironment.RecommendModelSize();
        Console.Error.WriteLine("=== Whisper Environment Probe ===");
        Console.Error.WriteLine($"  CUDA reported  : {probe.CudaAvailable}");
        Console.Error.WriteLine($"  Vulkan reported: {probe.VulkanAvailable}");
        Console.Error.WriteLine($"  System RAM     : {probe.SystemRamBytes / (1024L * 1024 * 1024)} GB");
        Console.Error.WriteLine($"  Logical cores  : {probe.LogicalCores}");
        Console.Error.WriteLine($"  Recommend()    : {recommended} (QualityBias.Accuracy default)");
        Console.Error.WriteLine($"  Models in run  : {string.Join(", ", options.Models)}");
        Console.Error.WriteLine($"  Warmup enabled : {options.Warmup}");
        Console.Error.WriteLine();
    }

    private static void EmitSanityWarnings(BenchmarkRow row)
    {
        if (row.AudioDurationSeconds > 0
            && row.TranscriptionSeconds / row.AudioDurationSeconds > 10.0)
        {
            Console.Error.WriteLine(
                $"  ⚠ {row.ModelSize}/{row.FileName}: RTF={row.RtfOrZero:F2} (>10) — abnormally slow.");
        }
        if (row.TranscriptCharCount == 0)
        {
            Console.Error.WriteLine(
                $"  ⚠ {row.ModelSize}/{row.FileName}: empty transcript — possible failure.");
        }
        if (row.PeakWorkingSetMb > 16384)
        {
            Console.Error.WriteLine(
                $"  ⚠ {row.ModelSize}/{row.FileName}: peak={row.PeakWorkingSetMb} MB (>16 GB) — memory leak suspected.");
        }
    }

    private static void EmitSummary(List<ModelSummary> summaries)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine("=== Summary ===");
        Console.Error.WriteLine($"  {"Model",-8} {"Files",6} {"AvgRTF",10} {"TotalSec",10} {"PeakMB",8}");
        foreach (var s in summaries)
        {
            if (s.Errored)
                Console.Error.WriteLine($"  {s.ModelSize,-8} {"ERROR",6} {"-",10} {"-",10} {"-",8}");
            else
                Console.Error.WriteLine($"  {s.ModelSize,-8} {s.FileCount,6} {s.AvgRtf,10:F3} {s.TotalSeconds,10:F1} {s.PeakWorkingSetMaxMb,8}");
        }
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("AudioBenchmark — Whisper.net model-size benchmark");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  AudioBenchmark <input-directory> [options]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Options:");
        Console.Error.WriteLine("  --models <list>      Comma-separated WhisperModelSize values.");
        Console.Error.WriteLine("                       Default: Tiny,Base,Small,Medium,Large");
        Console.Error.WriteLine("  --output <path>      CSV output path. Default: ./results.csv");
        Console.Error.WriteLine("  --transcripts <dir>  Per-(file,model) transcript directory.");
        Console.Error.WriteLine("                       Default: ./transcripts");
        Console.Error.WriteLine("  --no-warmup          Disable cold-start warmup throwaway pass.");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Ground truth:");
        Console.Error.WriteLine("  Place <basename>.gt.txt next to <basename>.<ext> to enable WER/CER.");
    }

    /// <summary>
    /// Lightweight option container parsed from positional + named flags.
    /// </summary>
    private sealed record BenchmarkOptions(
        string InputDirectory,
        IReadOnlyList<string> Models,
        string OutputCsv,
        string TranscriptsDirectory,
        bool Warmup,
        bool NoContext,
        DirectWhisperRunner.FallbackMode Fallback,
        GgmlType? GgmlOverride,
        string TranscriptSuffix)
    {
        public static BenchmarkOptions Parse(string[] args)
        {
            var input = Path.GetFullPath(args[0]);
            var models = (IReadOnlyList<string>)DefaultModels;
            var output = Path.GetFullPath("results.csv");
            var transcripts = Path.GetFullPath("transcripts");
            var warmup = true;
            var noContext = false;
            var fallback = DirectWhisperRunner.FallbackMode.None;
            GgmlType? ggmlOverride = null;
            var suffix = "";

            for (var i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--models" when i + 1 < args.Length:
                        models = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        break;
                    case "--output" when i + 1 < args.Length:
                        output = Path.GetFullPath(args[++i]);
                        break;
                    case "--transcripts" when i + 1 < args.Length:
                        transcripts = Path.GetFullPath(args[++i]);
                        break;
                    case "--no-warmup":
                        warmup = false;
                        break;
                    case "--no-context":
                        // Diagnostic-only: bypass WhisperTranscriber and call
                        // Whisper.net directly with WithNoContext() to test the
                        // long-form repetition-loop hypothesis.
                        noContext = true;
                        break;
                    case "--ggml" when i + 1 < args.Length:
                        // Diagnostic-only: override which Whisper.net GgmlType
                        // backs the chosen WhisperModelSize. Lets us A/B
                        // LargeV2 vs LargeV3 vs LargeV3Turbo without changing
                        // the public WhisperModelSize enum. Accepts any
                        // GgmlType name (case-insensitive).
                        if (!Enum.TryParse<GgmlType>(args[++i], ignoreCase: true, out var parsed))
                            throw new ArgumentException(
                                $"--ggml must be a GgmlType value (e.g. LargeV2, LargeV3, LargeV3Turbo). Got: {args[i]}");
                        ggmlOverride = parsed;
                        break;
                    case "--fallback" when i + 1 < args.Length:
                        // Diagnostic-only fallback configuration:
                        //   inc  = WithTemperatureInc(0.2) only
                        //   full = TempInc + LogProbThreshold(-1.0) + EntropyThreshold(2.4)
                        //          (whisper.cpp standard)
                        fallback = args[++i].ToLowerInvariant() switch
                        {
                            "inc" => DirectWhisperRunner.FallbackMode.TempIncOnly,
                            "full" => DirectWhisperRunner.FallbackMode.Full,
                            "none" => DirectWhisperRunner.FallbackMode.None,
                            _ => throw new ArgumentException(
                                $"--fallback must be one of: inc, full, none. Got: {args[i]}"),
                        };
                        break;
                    case "--transcript-suffix" when i + 1 < args.Length:
                        // Useful for A/B tests so the second run does not
                        // overwrite the first run's transcript files.
                        suffix = args[++i];
                        break;
                    default:
                        throw new ArgumentException($"Unknown or incomplete argument: {args[i]}");
                }
            }

            return new BenchmarkOptions(input, models, output, transcripts, warmup, noContext, fallback, ggmlOverride, suffix);
        }
    }

    /// <summary>One row of the benchmark CSV.</summary>
    internal sealed class BenchmarkRow
    {
        /// <summary>
        /// Configuration label that produced this row (e.g. <c>default</c>,
        /// <c>no_context</c>, <c>temp_fallback_full</c>, <c>ggml_large_v2</c>).
        /// Lets a single results CSV mix multiple experiment arms without
        /// losing provenance of each measurement.
        /// </summary>
        public string Config { get; set; } = "default";
        public DateTime Timestamp { get; set; }
        public string FileName { get; set; } = "";
        public double AudioDurationSeconds { get; set; }
        public WhisperModelSize ModelSize { get; set; }
        public double ColdStartSeconds { get; set; }
        public double TranscriptionSeconds { get; set; }
        public long PeakWorkingSetMb { get; set; }
        public int TranscriptCharCount { get; set; }
        public string RuntimeUsedFlags { get; set; } = "";
        public string Error { get; set; } = "";
        public bool GtAvailable { get; set; }
        public double? Wer { get; set; }
        public double? Cer { get; set; }
        public int? GtWordCount { get; set; }

        public double RtfOrZero
            => AudioDurationSeconds > 0 ? TranscriptionSeconds / AudioDurationSeconds : 0.0;

        public static BenchmarkRow Failed(string file, WhisperModelSize modelSize, string error) => new()
        {
            Timestamp = DateTime.UtcNow,
            FileName = Path.GetFileName(file),
            ModelSize = modelSize,
            Error = error,
            RuntimeUsedFlags = DescribeRuntime(),
        };
    }

    private sealed record ModelSummary(
        WhisperModelSize ModelSize,
        int FileCount,
        double AvgRtf,
        double TotalSeconds,
        long PeakWorkingSetMaxMb,
        bool Errored);

    /// <summary>
    /// Append-and-flush CSV writer. Intentionally simple — quotes any field
    /// containing comma, quote, or newline; flushes after every row so a
    /// long batch run that crashes still leaves partial measurements on disk.
    /// </summary>
    private sealed class CsvWriter : IAsyncDisposable
    {
        private readonly StreamWriter _writer;

        public CsvWriter(string path)
        {
            _writer = new StreamWriter(path, append: false, Encoding.UTF8);
            _writer.WriteLine(string.Join(",",
                "config",
                "timestamp",
                "file_name",
                "audio_duration_seconds",
                "model_size",
                "cold_start_seconds",
                "transcription_seconds",
                "rtf",
                "peak_working_set_mb",
                "transcript_char_count",
                "runtime_used",
                "error",
                "gt_available",
                "wer",
                "cer",
                "gt_word_count"));
            _writer.Flush();
        }

        public void WriteRow(BenchmarkRow row)
        {
            var fields = new[]
            {
                row.Config,
                row.Timestamp.ToString("o", CultureInfo.InvariantCulture),
                row.FileName,
                row.AudioDurationSeconds.ToString("F3", CultureInfo.InvariantCulture),
                row.ModelSize.ToString(),
                row.ColdStartSeconds.ToString("F3", CultureInfo.InvariantCulture),
                row.TranscriptionSeconds.ToString("F3", CultureInfo.InvariantCulture),
                row.RtfOrZero.ToString("F4", CultureInfo.InvariantCulture),
                row.PeakWorkingSetMb.ToString(CultureInfo.InvariantCulture),
                row.TranscriptCharCount.ToString(CultureInfo.InvariantCulture),
                row.RuntimeUsedFlags,
                row.Error,
                row.GtAvailable ? "true" : "false",
                row.Wer?.ToString("F4", CultureInfo.InvariantCulture) ?? "",
                row.Cer?.ToString("F4", CultureInfo.InvariantCulture) ?? "",
                row.GtWordCount?.ToString(CultureInfo.InvariantCulture) ?? "",
            };
            _writer.WriteLine(string.Join(",", fields.Select(EscapeCsv)));
            _writer.Flush();
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.IndexOfAny([',', '"', '\n', '\r']) < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        public async ValueTask DisposeAsync()
        {
            await _writer.DisposeAsync();
        }
    }
}
