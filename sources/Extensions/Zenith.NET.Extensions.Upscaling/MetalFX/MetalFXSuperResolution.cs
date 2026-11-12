using Metal.NET;

namespace Zenith.NET.Extensions.Upscaling.MetalFX;

internal class MetalFXSuperResolution(MetalFXVendor vendor, SuperResolutionDesc desc) : SuperResolution(vendor.Context, desc)
{
    private MTL4FXTemporalScaler scaler;
    private (PixelFormat Color, PixelFormat Depth, PixelFormat MotionVectors, PixelFormat? ReactiveMask, bool AutoExposure) key;
    private bool recreated;

    protected override void DispatchImpl(CommandBuffer commandBuffer, SuperResolutionArgs args)
    {
        using NSAutoreleasePool pool = new();

        (_, _, _, _, bool depthReversed, _) = Vendor.DecomposePerspective(args.Projection);

        EnsureScaler((args.Color.Desc.Format, args.Depth.Desc.Format, args.MotionVectors.Desc.Format, args.ReactiveMask?.Desc.Format, args.Exposure is null), depthReversed);

        if (recreated)
        {
            args.Reset = true;
            recreated = false;
        }

        commandBuffer.Transition(Output, default, TextureLayout.Undefined, TextureLayout.Storage);

        commandBuffer.BeginExternalCommands();

        Encode(commandBuffer.Metal(), args, depthReversed);

        commandBuffer.EndExternalCommands();

        commandBuffer.Transition(Output, default, TextureLayout.Storage, TextureLayout.Sampled);
    }

    protected override void Destroy()
    {
        if (!scaler.IsNull)
        {
            scaler.Dispose();
        }

        base.Destroy();
    }

    private void EnsureScaler((PixelFormat Color, PixelFormat Depth, PixelFormat MotionVectors, PixelFormat? ReactiveMask, bool AutoExposure) nextKey, bool depthReversed)
    {
        if (!scaler.IsNull && key == nextKey)
        {
            return;
        }

        if (!scaler.IsNull)
        {
            vendor.Context.GraphicsQueue.Timeline.Signal().Wait();
            scaler.Dispose();
        }

        key = nextKey;

        using MTLFXTemporalScalerDescriptor descriptor = new()
        {
            ColorTextureFormat = MetalFXFormats.Metal(nextKey.Color),
            DepthTextureFormat = MetalFXFormats.Metal(nextKey.Depth),
            MotionTextureFormat = MetalFXFormats.Metal(nextKey.MotionVectors),
            OutputTextureFormat = MetalFXFormats.Metal(Desc.Format),
            InputWidth = Desc.RenderWidth,
            InputHeight = Desc.RenderHeight,
            OutputWidth = Desc.OutputWidth,
            OutputHeight = Desc.OutputHeight,
            IsAutoExposureEnabled = nextKey.AutoExposure,
            IsReactiveMaskTextureEnabled = nextKey.ReactiveMask is not null,
            ReactiveMaskTextureFormat = MetalFXFormats.Metal(nextKey.ReactiveMask)
        };
        scaler = descriptor.MakeTemporalScaler(vendor.Device, vendor.Compiler);

        WarmUp(nextKey, depthReversed);
        recreated = true;
    }

    private void Encode(MTL4CommandBuffer commandBuffer, SuperResolutionArgs args, bool depthReversed)
    {
        scaler.ColorTexture = args.Color.Metal();
        scaler.DepthTexture = args.Depth.Metal();
        scaler.MotionTexture = args.MotionVectors.Metal();
        scaler.ExposureTexture = args.Exposure?.Metal() ?? default;
        scaler.ReactiveMaskTexture = args.ReactiveMask?.Metal() ?? default;
        scaler.OutputTexture = Output.Metal();
        scaler.InputContentWidth = Desc.RenderWidth;
        scaler.InputContentHeight = Desc.RenderHeight;
        scaler.JitterOffsetX = -args.JitterOffset.X;
        scaler.JitterOffsetY = -args.JitterOffset.Y;
        scaler.MotionVectorScaleX = Desc.RenderWidth;
        scaler.MotionVectorScaleY = Desc.RenderHeight;
        scaler.PreExposure = args.PreExposure ?? 1.0f;
        scaler.Reset = args.Reset;
        scaler.IsDepthReversed = depthReversed;
        scaler.Encode(commandBuffer);
    }

    private void WarmUp((PixelFormat Color, PixelFormat Depth, PixelFormat MotionVectors, PixelFormat? ReactiveMask, bool AutoExposure) nextKey, bool depthReversed)
    {
        using Texture color = vendor.CreateTexture(nextKey.Color, Desc.RenderWidth, Desc.RenderHeight);
        using Texture depth = vendor.CreateTexture(nextKey.Depth, Desc.RenderWidth, Desc.RenderHeight);
        using Texture motionVectors = vendor.CreateTexture(nextKey.MotionVectors, Desc.RenderWidth, Desc.RenderHeight);
        using Texture? exposure = nextKey.AutoExposure ? null : vendor.CreateTexture(PixelFormat.R16Float, 1, 1);
        using Texture? reactiveMask = nextKey.ReactiveMask is PixelFormat reactiveMaskFormat ? vendor.CreateTexture(reactiveMaskFormat, Desc.RenderWidth, Desc.RenderHeight) : null;

        vendor.WarmUp(commandBuffer => Encode(commandBuffer, new()
        {
            Color = color,
            Depth = depth,
            MotionVectors = motionVectors,
            ReactiveMask = reactiveMask,
            Exposure = exposure,
            PreExposure = 1.0f,
            Reset = true
        }, depthReversed));
    }
}
