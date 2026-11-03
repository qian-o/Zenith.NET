namespace Zenith.NET.Extensions.Upscaling;

public class UpscalingCapabilities
{
    internal UpscalingCapabilities(bool temporalUpscalerSupported, bool denoisingUpscalerSupported, bool frameGeneratorSupported)
    {
        TemporalUpscalerSupported = temporalUpscalerSupported;
        DenoisingUpscalerSupported = denoisingUpscalerSupported;
        FrameGeneratorSupported = frameGeneratorSupported;
    }

    public bool TemporalUpscalerSupported { get; }

    public bool DenoisingUpscalerSupported { get; }

    public bool FrameGeneratorSupported { get; }
}
