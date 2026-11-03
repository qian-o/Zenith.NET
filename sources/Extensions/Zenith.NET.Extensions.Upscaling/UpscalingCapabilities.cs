namespace Zenith.NET.Extensions.Upscaling;

public abstract class UpscalingCapabilities
{
    public abstract bool TemporalUpscalerSupported { get; }

    public abstract bool DenoisingUpscalerSupported { get; }

    public abstract bool FrameGeneratorSupported { get; }
}
