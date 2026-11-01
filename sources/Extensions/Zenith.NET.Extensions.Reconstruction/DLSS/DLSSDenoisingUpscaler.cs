using NGX.NET;

namespace Zenith.NET.Extensions.Reconstruction.DLSS;

internal class DLSSDenoisingUpscaler : DenoisingUpscaler
{
    private readonly DLSSBackend backend;
    private readonly DLSSFeature feature;

    public DLSSDenoisingUpscaler(DLSSBackend backend, DenoisingUpscalerDesc desc) : base(desc)
    {
        this.backend = backend;

        feature = new(backend);

        NGXDLSSDCreateParams createParams = new()
        {
            DenoiseMode = NGXDLSSDenoiseMode.DlUnified,
            RoughnessMode = NGXDLSSRoughnessMode.Unpacked,
            UseHWDepth = NGXDLSSDepthType.Hw,
            Width = desc.InputWidth,
            Height = desc.InputHeight,
            TargetWidth = desc.OutputWidth,
            TargetHeight = desc.OutputHeight,
            PerfQualityValue = backend.FindPerfQualityValue(NGXFeature.RayReconstruction, desc.InputWidth, desc.InputHeight, desc.OutputWidth, desc.OutputHeight),
            FeatureCreateFlags = (int)DLSSFormats.NGX(desc.Flags & ReconstructionFlags.DepthReversed)
        };

        backend.Create(feature, createParams);
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, DenoisingUpscalerArgs args)
    {
        backend.Evaluate(commandBuffer, feature, args);
    }

    protected override void Destroy()
    {
        feature.Dispose();
    }
}
