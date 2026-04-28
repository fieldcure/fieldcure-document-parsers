namespace FieldCure.DocumentParsers.Audio.Runtime;

/// <summary>
/// Progress event reported during <see cref="IWhisperRuntimeProvisioner.ProvisionAsync"/>.
/// </summary>
/// <param name="Phase">Current phase of the provisioning lifecycle.</param>
/// <param name="CurrentFile">Name of the file being acted on (e.g., <c>cudart64_12.dll</c>).
/// Empty for phase events that aren't file-specific.</param>
/// <param name="BytesReceived">Bytes downloaded for <see cref="CurrentFile"/> so far.
/// 0 outside <see cref="WhisperRuntimePhase.Downloading"/>.</param>
/// <param name="BytesTotal">Total expected bytes for <see cref="CurrentFile"/>, taken from
/// the HTTP <c>Content-Length</c> header. <see langword="null"/> when the response
/// is chunked-encoded and the server does not declare a length up front.</param>
public sealed record WhisperRuntimeProgress(
    WhisperRuntimePhase Phase,
    string CurrentFile,
    long BytesReceived,
    long? BytesTotal);
