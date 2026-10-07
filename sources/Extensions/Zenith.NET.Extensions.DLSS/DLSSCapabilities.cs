namespace Zenith.NET.Extensions.DLSS;

public readonly struct DLSSCapabilities(bool superResolutionSupported, bool rayReconstructionSupported, bool frameGenerationSupported)
{
    public readonly bool SuperResolutionSupported = superResolutionSupported;

    public readonly bool RayReconstructionSupported = rayReconstructionSupported;

    public readonly bool FrameGenerationSupported = frameGenerationSupported;
}
