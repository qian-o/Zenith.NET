using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal class DLSSRayReconstruction(DLSSVendor vendor, RayReconstructionDesc desc) : RayReconstruction(vendor.Context, desc)
{
    private readonly NGXPerfQualityValue perfQualityValue = vendor.FindPerfQualityValue(NGXFeature.RayReconstruction, desc.RenderWidth, desc.RenderHeight, desc.OutputWidth, desc.OutputHeight);

    private DLSSFeature? feature;
    private NGXDLSSFeatureFlags flags;
    private bool recreated;

    protected override void DispatchImpl(CommandBuffer commandBuffer, RayReconstructionArgs args)
    {
        (_, _, _, _, bool depthReversed, _) = Vendor.DecomposePerspective(args.Projection);
        EnsureFeature(DLSSFormats.NGX(args.Color.Desc.Format.IsHdr(), depthReversed, args.Exposure is null));

        if (recreated)
        {
            args.Reset = true;
            recreated = false;
        }

        vendor.Evaluate(commandBuffer, feature!, args, Output);
    }

    protected override void Destroy()
    {
        feature?.Dispose();

        base.Destroy();
    }

    private void EnsureFeature(NGXDLSSFeatureFlags nextFlags)
    {
        if (feature is not null && flags == nextFlags)
        {
            return;
        }

        if (feature is not null)
        {
            vendor.Context.GraphicsQueue.Timeline.Signal().Wait();
            feature.Dispose();
        }

        flags = nextFlags;
        feature = new(vendor);

        NGXDLSSDCreateParams createParams = new()
        {
            DenoiseMode = NGXDLSSDenoiseMode.DlUnified,
            RoughnessMode = NGXDLSSRoughnessMode.Unpacked,
            UseHWDepth = NGXDLSSDepthType.Hw,
            Width = Desc.RenderWidth,
            Height = Desc.RenderHeight,
            TargetWidth = Desc.OutputWidth,
            TargetHeight = Desc.OutputHeight,
            PerfQualityValue = perfQualityValue,
            FeatureCreateFlags = (int)nextFlags
        };

        vendor.Create(feature, createParams);
        recreated = true;
    }
}
