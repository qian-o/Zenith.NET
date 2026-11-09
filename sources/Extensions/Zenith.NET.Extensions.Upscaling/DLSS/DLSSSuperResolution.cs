using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal class DLSSSuperResolution : SuperResolution
{
    public DLSSFeature Feature;

    public DLSSSuperResolution(DLSSVendor vendor, SuperResolutionDesc desc) : base(desc)
    {
        Feature = new(vendor);

        if (!desc.Flags.HasFlag(UpscalingInputFlags.AutoExposure))
        {
            Feature.SetUI(Ngx.ParameterDLSSHintRenderPresetDLAA, (uint)NGXDLSSHintRenderPreset.K);
            Feature.SetUI(Ngx.ParameterDLSSHintRenderPresetQuality, (uint)NGXDLSSHintRenderPreset.K);
            Feature.SetUI(Ngx.ParameterDLSSHintRenderPresetBalanced, (uint)NGXDLSSHintRenderPreset.K);
            Feature.SetUI(Ngx.ParameterDLSSHintRenderPresetPerformance, (uint)NGXDLSSHintRenderPreset.K);
            Feature.SetUI(Ngx.ParameterDLSSHintRenderPresetUltraPerformance, (uint)NGXDLSSHintRenderPreset.K);
        }

        NGXDLSSCreateParams createParams = new()
        {
            Feature = new()
            {
                Width = desc.InputWidth,
                Height = desc.InputHeight,
                TargetWidth = desc.OutputWidth,
                TargetHeight = desc.OutputHeight,
                PerfQualityValue = vendor.FindPerfQualityValue(NGXFeature.SuperSampling, desc.InputWidth, desc.InputHeight, desc.OutputWidth, desc.OutputHeight)
            },
            FeatureCreateFlags = (int)DLSSFormats.NGX(desc.Flags, desc.IsHdr)
        };

        vendor.Create(Feature, createParams);
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, SuperResolutionArgs args)
    {
        Feature.Vendor.Evaluate(commandBuffer, Feature, args);
    }

    protected override void Destroy()
    {
        Feature.Dispose();
    }
}
