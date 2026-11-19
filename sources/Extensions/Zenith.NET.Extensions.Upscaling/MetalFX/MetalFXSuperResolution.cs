using Metal.NET;

namespace Zenith.NET.Extensions.Upscaling.MetalFX;

internal class MetalFXSuperResolution(MetalFXVendor vendor, SuperResolutionDesc desc) : SuperResolution(vendor.Context, desc)
{
    private MTL4FXTemporalScaler scaler;

    protected override void RebuildImpl(SuperResolutionArgs args)
    {
        using NSAutoreleasePool _ = new();

        using MTLFXTemporalScalerDescriptor descriptor = new()
        {
            ColorTextureFormat = MetalFXFormats.Metal(args.Color.Desc.Format),
            DepthTextureFormat = MetalFXFormats.Metal(args.Depth.Desc.Format),
            MotionTextureFormat = MetalFXFormats.Metal(args.MotionVectors.Desc.Format),
            OutputTextureFormat = MetalFXFormats.Metal(Desc.Format),
            InputWidth = Desc.RenderWidth,
            InputHeight = Desc.RenderHeight,
            OutputWidth = Desc.OutputWidth,
            OutputHeight = Desc.OutputHeight,
            IsAutoExposureEnabled = args.Exposure is null,
            RequiresSynchronousInitialization = true,
            IsReactiveMaskTextureEnabled = args.ReactiveMask is not null,
            ReactiveMaskTextureFormat = MetalFXFormats.Metal(args.ReactiveMask?.Desc.Format)
        };

        scaler.Dispose();
        scaler = descriptor.MakeTemporalScaler(vendor.Device, vendor.Compiler);
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, SuperResolutionArgs args)
    {
        using NSAutoreleasePool _ = new();

        commandBuffer.Transition(Output, default, args.Reset ? TextureLayout.Undefined : TextureLayout.Sampled, TextureLayout.Storage);

        commandBuffer.BeginExternalCommands();

        scaler.ColorTexture = args.Color.Metal();
        scaler.DepthTexture = args.Depth.Metal();
        scaler.MotionTexture = args.MotionVectors.Metal();
        scaler.OutputTexture = Output.Metal();
        scaler.ExposureTexture = args.Exposure?.Metal() ?? default;
        scaler.ReactiveMaskTexture = args.ReactiveMask?.Metal() ?? default;
        scaler.PreExposure = args.PreExposure ?? 1.0f;
        scaler.JitterOffsetX = -args.JitterOffset.X;
        scaler.JitterOffsetY = -args.JitterOffset.Y;
        scaler.MotionVectorScaleX = Desc.RenderWidth;
        scaler.MotionVectorScaleY = Desc.RenderHeight;
        scaler.Reset = args.Reset;
        scaler.IsDepthReversed = UpscalingHelper.IsDepthReversed(args.Projection);
        scaler.Encode(commandBuffer.Metal());

        commandBuffer.EndExternalCommands();

        commandBuffer.Transition(Output, default, TextureLayout.Storage, TextureLayout.Sampled);
    }

    protected override void Destroy()
    {
        scaler.Dispose();

        base.Destroy();
    }
}
