using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal class DLSSSuperResolution(DLSSVendor vendor, SuperResolutionDesc desc) : SuperResolution(desc)
{
    private readonly NGXPerfQualityValue perfQualityValue = vendor.FindPerfQualityValue(NGXFeature.SuperSampling, desc.RenderWidth, desc.RenderHeight, desc.OutputWidth, desc.OutputHeight);

    private DLSSFeature? feature;
    private NGXDLSSFeatureFlags flags;
    private bool recreated;

    protected override void DispatchImpl(CommandBuffer commandBuffer, SuperResolutionArgs args)
    {
        (_, _, _, _, bool depthReversed, _) = Vendor.DecomposePerspective(args.Projection);
        EnsureFeature(DLSSFormats.NGX(args.Color.Desc.Format.IsHdr(), depthReversed, args.Exposure is null));

        if (recreated)
        {
            args.Reset = true;
            recreated = false;
        }

        vendor.Evaluate(commandBuffer, feature!, args);
    }

    protected override void Destroy()
    {
        feature?.Dispose();
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

        if (!nextFlags.HasFlag(NGXDLSSFeatureFlags.AutoExposure))
        {
            feature.SetUI(Ngx.ParameterDLSSHintRenderPresetDLAA, (uint)NGXDLSSHintRenderPreset.K);
            feature.SetUI(Ngx.ParameterDLSSHintRenderPresetQuality, (uint)NGXDLSSHintRenderPreset.K);
            feature.SetUI(Ngx.ParameterDLSSHintRenderPresetBalanced, (uint)NGXDLSSHintRenderPreset.K);
            feature.SetUI(Ngx.ParameterDLSSHintRenderPresetPerformance, (uint)NGXDLSSHintRenderPreset.K);
            feature.SetUI(Ngx.ParameterDLSSHintRenderPresetUltraPerformance, (uint)NGXDLSSHintRenderPreset.K);
        }

        NGXDLSSCreateParams createParams = new()
        {
            Feature = new()
            {
                Width = Desc.RenderWidth,
                Height = Desc.RenderHeight,
                TargetWidth = Desc.OutputWidth,
                TargetHeight = Desc.OutputHeight,
                PerfQualityValue = perfQualityValue
            },
            FeatureCreateFlags = (int)nextFlags
        };

        vendor.Create(feature, createParams);
        recreated = true;
    }
}
