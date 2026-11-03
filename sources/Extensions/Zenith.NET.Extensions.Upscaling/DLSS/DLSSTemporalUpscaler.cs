using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal class DLSSTemporalUpscaler : TemporalUpscaler
{
    private readonly DLSSFeature feature;

    public DLSSTemporalUpscaler(DLSSBackend backend, TemporalUpscalerDesc desc) : base(desc)
    {
        feature = new(backend);

        if (!desc.Flags.HasFlag(UpscalingInputFlags.AutoExposure))
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
                Width = desc.InputWidth,
                Height = desc.InputHeight,
                TargetWidth = desc.OutputWidth,
                TargetHeight = desc.OutputHeight,
                PerfQualityValue = backend.FindPerfQualityValue(NGXFeature.SuperSampling, desc.InputWidth, desc.InputHeight, desc.OutputWidth, desc.OutputHeight)
            },
            FeatureCreateFlags = (int)DLSSFormats.NGX(desc.Flags)
        };

        backend.Create(feature, createParams);
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, TemporalUpscalerArgs args)
    {
        feature.Backend.Evaluate(commandBuffer, feature, args);
    }

    protected override void Destroy()
    {
        feature?.Dispose();
    }
}
