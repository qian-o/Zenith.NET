using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal class DLSSCapabilities(DLSSProvider provider) : UpscalingCapabilities
{
    public override bool TemporalUpscalerSupported { get; } = provider.IsAvailable(Ngx.ParameterSuperSamplingAvailable);

    public override bool DenoisingUpscalerSupported { get; } = provider.IsAvailable(Ngx.ParameterSuperSamplingDenoisingAvailable);

    public override bool FrameGeneratorSupported { get; } = provider.IsAvailable(Ngx.ParameterFrameGenerationAvailable);
}
