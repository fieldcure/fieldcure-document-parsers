using System.Reflection;

namespace FieldCure.Tools.AudioBenchmark;

/// <summary>
/// Diagnostic helper that lists the public API surface of
/// <c>Whisper.net.WhisperProcessorBuilder</c> at runtime so we can confirm
/// which knobs (e.g. <c>WithNoContext</c>, temperature fallback) actually
/// exist in the version pulled by FieldCure.DocumentParsers.Audio. Invoked
/// via <c>AudioBenchmark --probe-api</c>; runs no transcription.
/// </summary>
internal static class ApiProbe
{
    public static int Run()
    {
        // Touch a Whisper.net type so the JIT actually resolves and loads the
        // assembly. Bare `typeof(...)` is sometimes elided by the JIT.
        var factoryType = typeof(Whisper.net.WhisperFactory);
        var whisperNetAsm = factoryType.Assembly;

        Console.WriteLine($"Whisper.net version: {whisperNetAsm.GetName().Version}");
        Console.WriteLine();

        var typesOfInterest = new[]
        {
            "Whisper.net.WhisperProcessorBuilder",
            "Whisper.net.WhisperProcessor",
            "Whisper.net.WhisperFactory",
            "Whisper.net.SamplingStrategy.BeamSearchSamplingStrategy",
            "Whisper.net.SamplingStrategy.GreedySamplingStrategy",
            "Whisper.net.Ggml.GgmlType",
        };

        // Special-case enum dump for GgmlType (lists all model variants).
        var ggmlType = whisperNetAsm.GetType("Whisper.net.Ggml.GgmlType");
        if (ggmlType is not null && ggmlType.IsEnum)
        {
            Console.WriteLine("=== Whisper.net.Ggml.GgmlType members ===");
            foreach (var name in Enum.GetNames(ggmlType).OrderBy(n => n))
                Console.WriteLine($"  {name}");
            Console.WriteLine();
        }

        foreach (var typeName in typesOfInterest)
        {
            var t = whisperNetAsm.GetType(typeName);
            if (t is null)
            {
                Console.WriteLine($"=== {typeName} (NOT FOUND) ===");
                continue;
            }

            Console.WriteLine($"=== {t.FullName} ===");
            var methods = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => !m.IsSpecialName)
                .Where(m => m.DeclaringType == t)
                .OrderBy(m => m.Name);

            foreach (var m in methods)
            {
                var paramList = string.Join(", ",
                    m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                Console.WriteLine($"  {m.Name}({paramList}) -> {m.ReturnType.Name}");
            }

            Console.WriteLine();
        }

        // Surface anything that looks relevant to the long-form loop discussion.
        var keywords = new[] { "context", "temperature", "noctx", "nocontext", "suppress",
                                "patience", "beam", "greedy", "compression", "logprob" };
        Console.WriteLine("=== Methods/types matching loop-mitigation keywords ===");
        var allTypes = whisperNetAsm.GetExportedTypes();
        foreach (var t in allTypes)
        {
            var matches = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => keywords.Any(k =>
                    m.Name.Contains(k, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (matches.Count == 0) continue;
            Console.WriteLine($"  {t.FullName}");
            foreach (var m in matches)
            {
                var paramList = string.Join(", ",
                    m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                Console.WriteLine($"    {m.Name}({paramList})");
            }
        }

        return 0;
    }
}
