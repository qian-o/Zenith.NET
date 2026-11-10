using Metal.NET;

namespace Zenith.NET.Extensions.Upscaling.MetalFX;

internal class MetalFXSuperResolution : SuperResolution
{
    public MTL4FXTemporalScaler Scaler;

    public MetalFXSuperResolution(MetalFXVendor vendor, SuperResolutionDesc desc) : base(desc)
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
            IsReactiveMaskTextureEnabled = desc.ReactiveMaskFormat is not null,
            ReactiveMaskTextureFormat = MetalFXFormats.Metal(desc.ReactiveMaskFormat)
        };

        Scaler = descriptor.MakeTemporalScaler(vendor.Device, vendor.Compiler);
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, SuperResolutionArgs args)
    {
        using NSAutoreleasePool _ = new();

        MTL4CommandBuffer mtlCommandBuffer = commandBuffer.Metal();

        Scaler.ColorTexture = args.Color.Metal();
        Scaler.DepthTexture = args.Depth.Metal();
        Scaler.MotionTexture = args.MotionVectors.Metal();
        Scaler.ExposureTexture = args.Exposure?.Metal() ?? MTLTexture.Null;
        Scaler.ReactiveMaskTexture = args.ReactiveMask?.Metal() ?? MTLTexture.Null;
        Scaler.OutputTexture = args.Output.Metal();
        Scaler.InputContentWidth = Desc.InputWidth;
        Scaler.InputContentHeight = Desc.InputHeight;
        Scaler.JitterOffsetX = args.JitterOffset.X;
        Scaler.JitterOffsetY = args.JitterOffset.Y;
        Scaler.MotionVectorScaleX = Desc.InputWidth;
        Scaler.MotionVectorScaleY = Desc.InputHeight;
        Scaler.PreExposure = args.PreExposure;
        Scaler.Reset = args.Reset;
        Scaler.IsDepthReversed = Desc.Flags.HasFlag(UpscalingInputFlags.DepthReversed);
        Scaler.Encode(mtlCommandBuffer);

        MetalFXVendor.EncodeBarrier(mtlCommandBuffer);
    }

    protected override void Destroy()
    {
        Scaler.Dispose();
    }
}
