namespace Zenith.NET.Extensions.Reconstruction;

public class ReconstructionCapabilities
{
    internal ReconstructionCapabilities(bool temporalUpscalerSupported, bool denoisingUpscalerSupported, bool frameGeneratorSupported)
    {
        TemporalUpscalerSupported = temporalUpscalerSupported;
        DenoisingUpscalerSupported = denoisingUpscalerSupported;
        FrameGeneratorSupported = frameGeneratorSupported;
    }

    public bool TemporalUpscalerSupported { get; }

    public bool DenoisingUpscalerSupported { get; }

    public bool FrameGeneratorSupported { get; }
}
