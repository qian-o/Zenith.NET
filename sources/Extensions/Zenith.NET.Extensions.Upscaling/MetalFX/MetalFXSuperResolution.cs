using Metal.NET;

namespace Zenith.NET.Extensions.Upscaling.MetalFX;

internal class MetalFXSuperResolution(MetalFXVendor vendor, SuperResolutionDesc desc) : SuperResolution(vendor.Context, desc)
{
    private Key key;
    private MTL4FXTemporalScaler scaler;

    protected override void DispatchImpl(CommandBuffer commandBuffer, SuperResolutionArgs args)
    {
        using NSAutoreleasePool _ = new();

        if (Ensure(args))
        {
            args.Reset = true;
        }

        commandBuffer.Transition(Output, default, TextureLayout.Undefined, TextureLayout.Storage);

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

    private bool Ensure(SuperResolutionArgs args)
    {
        Key newKey = new(args);

        if (key.Equals(newKey))
        {
            return false;
        }

        key = newKey;

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

        return true;
    }

    private readonly struct Key(SuperResolutionArgs args)
    {
        public readonly PixelFormat Color = args.Color.Desc.Format;

        public readonly PixelFormat Depth = args.Depth.Desc.Format;

        public readonly PixelFormat MotionVectors = args.MotionVectors.Desc.Format;

        public readonly PixelFormat ReactiveMask = args.ReactiveMask?.Desc.Format ?? default;

        public readonly PixelFormat Exposure = args.Exposure?.Desc.Format ?? default;

        public bool Equals(Key other)
        {
            return Color == other.Color && Depth == other.Depth && MotionVectors == other.MotionVectors && ReactiveMask == other.ReactiveMask && Exposure == other.Exposure;
        }
    }
}
