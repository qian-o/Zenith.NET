using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal class DLSSDenoisingUpscaler : DenoisingUpscaler
{
    private readonly DLSSProvider provider;
    private readonly DLSSFeature feature;

    public DLSSDenoisingUpscaler(DLSSProvider provider, DenoisingUpscalerDesc desc) : base(desc)
    {
        this.provider = provider;

        feature = new(provider);

        NGXDLSSDCreateParams createParams = new()
        {
            DenoiseMode = NGXDLSSDenoiseMode.DlUnified,
            RoughnessMode = NGXDLSSRoughnessMode.Unpacked,
            UseHWDepth = NGXDLSSDepthType.Hw,
            Width = desc.InputWidth,
            Height = desc.InputHeight,
            TargetWidth = desc.OutputWidth,
            TargetHeight = desc.OutputHeight,
            PerfQualityValue = provider.FindPerfQualityValue(NGXFeature.RayReconstruction, desc.InputWidth, desc.InputHeight, desc.OutputWidth, desc.OutputHeight),
            FeatureCreateFlags = (int)DLSSFormats.NGX(desc.Flags & UpscalingInputFlags.DepthReversed)
        };

        provider.Create(feature, createParams);
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, DenoisingUpscalerArgs args)
    {
        provider.Evaluate(commandBuffer, feature, args);
    }

    protected override void Destroy()
    {
        feature.Dispose();
    }
}
