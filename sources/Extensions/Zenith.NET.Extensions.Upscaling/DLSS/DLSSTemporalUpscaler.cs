using NGX.NET;

namespace Zenith.NET.Extensions.Reconstruction.DLSS;

internal class DLSSTemporalUpscaler : TemporalUpscaler
{
    private readonly DLSSBackend backend;
    private readonly DLSSFeature feature;

    public DLSSTemporalUpscaler(DLSSBackend backend, TemporalUpscalerDesc desc) : base(desc)
    {
        this.backend = backend;

        feature = new(backend);

        if (!desc.Flags.HasFlag(ReconstructionFlags.AutoExposure))
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
        backend.Evaluate(commandBuffer, feature, args);
    }

    protected override void Destroy()
    {
        feature.Dispose();
    }
}
