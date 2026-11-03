namespace Zenith.NET.Extensions.Upscaling;

internal class UpscalingBackendCapabilities(Provider? temporalUpscalerProvider, Provider? denoisingUpscalerProvider, Provider? frameGeneratorProvider) : UpscalingCapabilities
{
    public override bool TemporalUpscalerSupported { get; } = temporalUpscalerProvider is not null;

    public override bool DenoisingUpscalerSupported { get; } = denoisingUpscalerProvider is not null;

    public override bool FrameGeneratorSupported { get; } = frameGeneratorProvider is not null;
}
