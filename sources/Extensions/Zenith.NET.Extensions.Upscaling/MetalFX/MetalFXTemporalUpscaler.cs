using Metal.NET;

namespace Zenith.NET.Extensions.Upscaling.MetalFX;

internal class MetalFXTemporalUpscaler : TemporalUpscaler
{
    private readonly MTL4FXTemporalScaler scaler;

    public MetalFXTemporalUpscaler(MetalFXVendor vendor, TemporalUpscalerDesc desc) : base(desc)
    {
        MTLFXTemporalScalerDescriptor descriptor = new()
        {
            ColorTextureFormat = MetalFXFormats.Metal(desc.ColorFormat),
            DepthTextureFormat = MetalFXFormats.Metal(desc.DepthFormat),
            MotionTextureFormat = MetalFXFormats.Metal(desc.MotionVectorsFormat),
            OutputTextureFormat = MetalFXFormats.Metal(desc.OutputFormat),
            InputWidth = desc.InputWidth,
            InputHeight = desc.InputHeight,
            OutputWidth = desc.OutputWidth,
            OutputHeight = desc.OutputHeight,
            IsAutoExposureEnabled = desc.Flags.HasFlag(UpscalingInputFlags.AutoExposure),
            IsOutputResolutionMotionVectorsEnabled = desc.Flags.HasFlag(UpscalingInputFlags.OutputResolutionMotionVectors),
            IsJitteredMotionVectorsEnabled = desc.Flags.HasFlag(UpscalingInputFlags.JitteredMotionVectors),
            IsReactiveMaskTextureEnabled = desc.ReactiveMaskFormat is not null,
            ReactiveMaskTextureFormat = MetalFXFormats.Metal(desc.ReactiveMaskFormat)
        };

        scaler = descriptor.MakeTemporalScaler(vendor.Device, vendor.Compiler);

        if (scaler.IsNull)
        {
            throw new InvalidOperationException("MetalFX temporal upscaler creation failed.");
        }
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, TemporalUpscalerArgs args)
    {
        using NSAutoreleasePool _ = new();

        MTL4CommandBuffer mtlCommandBuffer = commandBuffer.Metal();

        scaler.ColorTexture = args.Color.Metal();
        scaler.DepthTexture = args.Depth.Metal();
        scaler.MotionTexture = args.MotionVectors.Metal();
        scaler.ExposureTexture = args.Exposure?.Metal() ?? MTLTexture.Null;
        scaler.ReactiveMaskTexture = args.ReactiveMask?.Metal() ?? MTLTexture.Null;
        scaler.OutputTexture = args.Output.Metal();
        scaler.InputContentWidth = Desc.InputWidth;
        scaler.InputContentHeight = Desc.InputHeight;
        scaler.JitterOffsetX = args.JitterOffset.X;
        scaler.JitterOffsetY = args.JitterOffset.Y;
        scaler.MotionVectorScaleX = args.MotionVectorScale.X;
        scaler.MotionVectorScaleY = args.MotionVectorScale.Y;
        scaler.PreExposure = args.PreExposure;
        scaler.Reset = args.Reset;
        scaler.IsDepthReversed = Desc.Flags.HasFlag(UpscalingInputFlags.DepthReversed);
        scaler.Encode(mtlCommandBuffer);

        MetalFXVendor.EncodeBarrier(mtlCommandBuffer);
    }

    protected override void Destroy()
    {
        scaler?.Dispose();
    }
}
