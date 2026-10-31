using NGX.NET;

namespace Zenith.NET.Extensions.Reconstruction.DLSS;

internal class DLSSDenoisingUpscaler : DenoisingUpscaler
{
    private readonly DLSSContext context;
    private readonly DLSSFeature feature;

    public DLSSDenoisingUpscaler(DLSSContext context, DenoisingUpscalerDesc desc) : base(desc)
    {
        this.context = context;

        feature = new(context);

        NGXDLSSDCreateParams createParams = new()
        {
            DenoiseMode = NGXDLSSDenoiseMode.DlUnified,
            RoughnessMode = NGXDLSSRoughnessMode.Unpacked,
            UseHWDepth = NGXDLSSDepthType.Hw,
            Width = desc.InputWidth,
            Height = desc.InputHeight,
            TargetWidth = desc.OutputWidth,
            TargetHeight = desc.OutputHeight,
            PerfQualityValue = context.FindPerfQualityValue(NGXFeature.RayReconstruction, desc.InputWidth, desc.InputHeight, desc.OutputWidth, desc.OutputHeight),
            FeatureCreateFlags = (int)DLSSFormats.NGX(desc.Flags & ReconstructionFlags.DepthReversed)
        };

        context.Create(feature, createParams);
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, DenoisingUpscalerArgs args)
    {
        context.Evaluate(commandBuffer, feature, args);
    }

    protected override void Destroy()
    {
        feature.Dispose();
    }
}
