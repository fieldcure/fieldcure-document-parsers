using System.Reflection;
using Whisper.net.LibraryLoader;

namespace FieldCure.DocumentParsers.Audio.Tests;

/// <summary>
/// Unit tests for <see cref="AudioExtractionOptions"/>.
/// </summary>
[TestClass]
public class AudioExtractionOptionsTests
{
    /// <summary>
    /// Regression guard: ensures that <see cref="AudioExtractionOptions.WithModelSize"/>
    /// copies every <c>init</c>-only property declared on the type and its base.
    /// If a new property is added without being appended to the explicit copy in
    /// <c>WithModelSize</c>, this test fails — preventing silent data loss.
    /// </summary>
    [TestMethod]
    public void WithModelSize_PreservesAllInitProperties()
    {
        var source = new AudioExtractionOptions
        {
            // Base ExtractionOptions: flip from defaults so any missed copy is detectable.
            IncludeMetadata = false,
            IncludeHeaders = false,
            IncludeFooters = false,
            IncludeFootnotes = false,
            IncludeEndnotes = false,
            IncludeComments = false,
            SourceExtension = ".audio-test",

            // AudioExtractionOptions own properties: distinct non-default values.
            Language = "ko",
            ModelSize = WhisperModelSize.Small,
            ModelPath = "/tmp/custom-model.bin",
            TranslateToEnglish = true,
            RuntimeLibraryOrder = new[] { RuntimeLibrary.Cpu },
            IncludeConfidence = true,
            IncludeTimestamps = false,
        };

        var copy = source.WithModelSize(WhisperModelSize.Large);

        Assert.AreEqual(WhisperModelSize.Large, copy.ModelSize, "ModelSize must reflect override.");

        // Reflectively verify every init-only property other than ModelSize is preserved.
        var initProps = typeof(AudioExtractionOptions)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && IsInitOnly(p) && p.Name != nameof(AudioExtractionOptions.ModelSize))
            .ToList();

        // Sanity: catch the case where reflection finds nothing (e.g. binding flags drift).
        Assert.IsTrue(initProps.Count >= 13,
            $"Expected at least 13 init-only properties to verify; found {initProps.Count}. " +
            "If this fails, the reflection filter is wrong or properties were removed.");

        foreach (var prop in initProps)
        {
            var expected = prop.GetValue(source);
            var actual = prop.GetValue(copy);
            Assert.AreEqual(expected, actual,
                $"WithModelSize dropped property '{prop.Name}'. " +
                "Append it to the explicit copy in AudioExtractionOptions.WithModelSize.");
        }
    }

    private static bool IsInitOnly(PropertyInfo prop)
    {
        // init-only setters are encoded as a setter with an IsExternalInit modreq.
        var setter = prop.SetMethod;
        if (setter is null) return false;
        return setter.ReturnParameter
            .GetRequiredCustomModifiers()
            .Any(t => t.FullName == "System.Runtime.CompilerServices.IsExternalInit");
    }
}
