namespace Zenith.NET.Extensions.DLSS;

public class DLSSCapabilities(bool superResolutionSupported, bool rayReconstructionSupported, bool frameGenerationSupported)
{
    public bool SuperResolutionSupported { get; } = superResolutionSupported;

    public bool RayReconstructionSupported { get; } = rayReconstructionSupported;

    public bool FrameGenerationSupported { get; } = frameGenerationSupported;
}
