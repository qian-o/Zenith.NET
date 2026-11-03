using NGX.NET;

namespace Zenith.NET.Extensions.Reconstruction.DLSS;

internal class DLSSCapabilities(DLSSBackend backend) : ReconstructionCapabilities
{
    public override bool TemporalUpscalerSupported { get; } = backend.IsAvailable(Ngx.ParameterSuperSamplingAvailable);

    public override bool DenoisingUpscalerSupported { get; } = backend.IsAvailable(Ngx.ParameterSuperSamplingDenoisingAvailable);

    public override bool FrameGeneratorSupported { get; } = backend.IsAvailable(Ngx.ParameterFrameGenerationAvailable);
}
