using System.Runtime.InteropServices;
using InteropDotNet;

namespace FieldCure.DocumentParsers.Ocr;

/// <summary>
/// One-shot native-library bootstrap for the Tesseract.NET wrapper. Routes
/// the wrapper's hard-coded <c>x64\</c> DLL lookup at the right architecture's
/// binaries when the package is consumed inside a multi-arch tool layout
/// (typically <c>PackAsTool</c> consumers like the FieldCure MCP RAG server
/// running under <c>dnx</c>).
/// </summary>
/// <remarks>
/// <para>
/// Background. The wrapper's <see cref="LibraryLoader"/> looks for natives at
/// <c>&lt;base&gt;\x64\tesseract50.dll</c> on every 64-bit process — there is
/// no architecture detection (see
/// <c>InteropDotNet.SystemManager.GetPlatformName</c>). For library consumers
/// the build/*.targets file solves this at consumer build time by selecting
/// the right-arch DLLs from <c>x64\</c> or <c>arm64\</c> in this package and
/// landing them at the consumer output's <c>x64\</c> folder.
/// </para>
/// <para>
/// PackAsTool consumers cannot do that — the tool nupkg they publish is
/// produced once on a single host (typically x64 CI), but it can later be
/// run by <c>dnx</c> on either x64 or ARM64. So the tool nupkg has to carry
/// BOTH arches. We pack:
/// </para>
/// <list type="bullet">
///   <item><c>tools/&lt;tfm&gt;/any/x64/</c> — x64 binaries (wrapper default).</item>
///   <item><c>tools/&lt;tfm&gt;/any/arm64-platform/x64/</c> — ARM64 binaries.</item>
/// </list>
/// <para>
/// At runtime, when this package's first OCR engine is created on an ARM64
/// process, this bootstrap sets <see cref="LibraryLoader.CustomSearchPath"/>
/// to <c>&lt;base&gt;/arm64-platform/</c>. The wrapper's first probe checks
/// custom search before falling through to the executing-assembly directory,
/// so it loads <c>&lt;base&gt;/arm64-platform/x64/tesseract50.dll</c> — the
/// ARM64 binary, named to satisfy the wrapper's hard-coded "x64" subfolder
/// expectation.
/// </para>
/// <para>
/// Idempotent and thread-safe; safe to call from every <see cref="TesseractOcrEngine"/>
/// constructor. The arm64-platform/ directory check makes it a silent no-op
/// for library deployments where only the consumer's chosen arch landed in
/// <c>&lt;base&gt;\x64\</c> directly.
/// </para>
/// </remarks>
internal static class NativeLibraryBootstrap
{
    /// <summary>
    /// Lock guarding <see cref="_initialized"/>; ensures the configuration
    /// runs once even under concurrent first-use of <see cref="TesseractOcrEngine"/>.
    /// </summary>
    private static readonly object _gate = new();

    /// <summary>
    /// Whether <see cref="EnsureInitialized"/> has already run. Volatile because
    /// it is read outside the lock on the fast path.
    /// </summary>
    private static volatile bool _initialized;

    /// <summary>
    /// Subfolder name (under the assembly directory) where ARM64 natives live
    /// when this package is packed into a PackAsTool consumer. The trailing
    /// <c>x64/</c> segment under it is the wrapper's hard-coded platform
    /// folder, so the on-disk layout is <c>arm64-platform/x64/&lt;dll&gt;</c>.
    /// </summary>
    private const string Arm64PlatformSubdir = "arm64-platform";

    /// <summary>
    /// Configures <see cref="LibraryLoader.CustomSearchPath"/> on first call
    /// when running on an ARM64 process and an ARM64 native bundle is colocated
    /// with this assembly. No-op otherwise.
    /// </summary>
    public static void EnsureInitialized()
    {
        if (_initialized) return;
        lock (_gate)
        {
            if (_initialized) return;
            try
            {
                ConfigureArm64SearchPath();
            }
            finally
            {
                _initialized = true;
            }
        }
    }

    /// <summary>
    /// Sets <see cref="LibraryLoader.CustomSearchPath"/> to the colocated
    /// <c>arm64-platform/</c> directory when (a) the process is ARM64 and
    /// (b) the directory actually contains an x64-named subfolder with the
    /// expected DLLs. The directory probe avoids polluting the wrapper's
    /// search state in library-consumer scenarios where the right-arch
    /// binaries already sit in <c>&lt;base&gt;\x64\</c>.
    /// </summary>
    private static void ConfigureArm64SearchPath()
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.Arm64) return;

        var asmDir = Path.GetDirectoryName(typeof(NativeLibraryBootstrap).Assembly.Location);
        if (string.IsNullOrEmpty(asmDir)) return;

        var arm64Root = Path.Combine(asmDir, Arm64PlatformSubdir);
        var arm64Natives = Path.Combine(arm64Root, "x64");
        if (!Directory.Exists(arm64Natives)) return;

        LibraryLoader.Instance.CustomSearchPath = arm64Root;
    }
}
