namespace Zenith.NET.Extensions.Upscaling;

public class UpscalingCapabilities
{
    internal UpscalingCapabilities(bool superResolutionSupported, bool rayReconstructionSupported, bool frameGenerationSupported)
    {
        SuperResolutionSupported = superResolutionSupported;
        RayReconstructionSupported = rayReconstructionSupported;
        FrameGenerationSupported = frameGenerationSupported;
    }

    public bool SuperResolutionSupported { get; }

    public bool RayReconstructionSupported { get; }

    public bool FrameGenerationSupported { get; }
}
