namespace Zenith.NET.Extensions.Reconstruction;

public abstract class ReconstructionCapabilities
{
    public abstract bool TemporalUpscalerSupported { get; }

    public abstract bool DenoisingUpscalerSupported { get; }

    public abstract bool FrameGeneratorSupported { get; }
}
