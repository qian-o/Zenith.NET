using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal class DLSSRayReconstruction : RayReconstruction
{
    public DLSSFeature Feature;

    public DLSSRayReconstruction(DLSSVendor vendor, RayReconstructionDesc desc) : base(desc)
    {
        Feature = new(vendor);

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
            FeatureCreateFlags = (int)DLSSFormats.NGX(desc.Flags, desc.IsHdr)
        };

        vendor.Create(Feature, createParams);
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, RayReconstructionArgs args)
    {
        Feature.Vendor.Evaluate(commandBuffer, Feature, args);
    }

    protected override void Destroy()
    {
        Feature.Dispose();
    }
}
