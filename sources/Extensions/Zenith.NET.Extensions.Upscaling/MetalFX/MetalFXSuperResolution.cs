using Metal.NET;

namespace Zenith.NET.Extensions.Upscaling.MetalFX;

internal class MetalFXSuperResolution : SuperResolution
{
    public MTL4FXTemporalScaler Scaler;

    public MetalFXSuperResolution(MetalFXVendor vendor, SuperResolutionDesc desc) : base(desc)
    {
        using MTLFXTemporalScalerDescriptor descriptor = new()
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

        WarmUp(vendor);
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, SuperResolutionArgs args)
    {
        using NSAutoreleasePool _ = new();

        MTL4CommandBuffer mtlCommandBuffer = commandBuffer.Metal();

        Encode(mtlCommandBuffer, args);

        MetalFXVendor.EncodeBarrier(mtlCommandBuffer);
    }

    protected override void Destroy()
    {
        Scaler.Dispose();
    }

    private void Encode(MTL4CommandBuffer commandBuffer, SuperResolutionArgs args)
    {
        Scaler.ColorTexture = args.Color.Metal();
        Scaler.DepthTexture = args.Depth.Metal();
        Scaler.MotionTexture = args.MotionVectors.Metal();
        Scaler.ExposureTexture = args.Exposure?.Metal() ?? default;
        Scaler.ReactiveMaskTexture = args.ReactiveMask?.Metal() ?? default;
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
        Scaler.Encode(commandBuffer);
    }

    private void WarmUp(MetalFXVendor vendor)
    {
        using Texture color = vendor.CreateTexture(Desc.ColorFormat, Desc.InputWidth, Desc.InputHeight);
        using Texture depth = vendor.CreateTexture(Desc.DepthFormat, Desc.InputWidth, Desc.InputHeight);
        using Texture motionVectors = vendor.CreateTexture(Desc.MotionVectorsFormat, Desc.InputWidth, Desc.InputHeight);
        using Texture? exposure = Desc.Flags.HasFlag(UpscalingInputFlags.AutoExposure) ? null : vendor.CreateTexture(PixelFormat.R16Float, 1, 1);
        using Texture? reactiveMask = Desc.ReactiveMaskFormat is PixelFormat reactiveMaskFormat ? vendor.CreateTexture(reactiveMaskFormat, Desc.InputWidth, Desc.InputHeight) : null;
        using Texture output = vendor.CreateTexture(Desc.OutputFormat, Desc.OutputWidth, Desc.OutputHeight);

        vendor.WarmUp(commandBuffer => Encode(commandBuffer, new()
        {
            Color = color,
            Depth = depth,
            MotionVectors = motionVectors,
            Exposure = exposure,
            ReactiveMask = reactiveMask,
            Output = output,
            PreExposure = 1.0f,
            Reset = true
        }));
    }
}
