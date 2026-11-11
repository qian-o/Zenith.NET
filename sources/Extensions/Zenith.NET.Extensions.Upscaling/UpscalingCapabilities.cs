namespace Zenith.NET.Extensions.Upscaling;

public class UpscalingCapabilities(bool superResolutionSupported, bool rayReconstructionSupported, bool frameGenerationSupported)
{
    public bool SuperResolutionSupported { get; } = superResolutionSupported;

    public bool RayReconstructionSupported { get; } = rayReconstructionSupported;

    public bool FrameGenerationSupported { get; } = frameGenerationSupported;
}
