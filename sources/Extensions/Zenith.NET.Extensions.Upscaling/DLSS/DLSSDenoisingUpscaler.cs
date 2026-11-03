using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal class DLSSDenoisingUpscaler : DenoisingUpscaler
{
    private readonly DLSSFeature feature;

    public DLSSDenoisingUpscaler(DLSSVendor vendor, DenoisingUpscalerDesc desc) : base(desc)
    {
        feature = new(vendor);

        NGXDLSSDCreateParams createParams = new()
        {
            DenoiseMode = NGXDLSSDenoiseMode.DlUnified,
            RoughnessMode = NGXDLSSRoughnessMode.Unpacked,
            UseHWDepth = NGXDLSSDepthType.Hw,
            Width = desc.InputWidth,
            Height = desc.InputHeight,
            TargetWidth = desc.OutputWidth,
            TargetHeight = desc.OutputHeight,
            PerfQualityValue = vendor.FindPerfQualityValue(NGXFeature.RayReconstruction, desc.InputWidth, desc.InputHeight, desc.OutputWidth, desc.OutputHeight),
            FeatureCreateFlags = (int)DLSSFormats.NGX(desc.Flags & UpscalingInputFlags.DepthReversed)
        };

        vendor.Create(feature, createParams);
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, DenoisingUpscalerArgs args)
    {
        feature.Vendor.Evaluate(commandBuffer, feature, args);
    }

    protected override void Destroy()
    {
        feature?.Dispose();
    }
}
