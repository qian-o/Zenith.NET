using Metal.NET;

namespace Zenith.NET.Extensions.Upscaling.MetalFX;

internal class MetalFXFrameGeneration(MetalFXVendor vendor, FrameGenerationDesc desc) : FrameGeneration(vendor.Context, desc)
{
    private MTL4FXFrameInterpolator interpolator;

    protected override void RebuildImpl(FrameGenerationArgs args)
    {
        using NSAutoreleasePool _ = new();

        using MTLFXFrameInterpolatorDescriptor descriptor = new()
        {
            ColorTextureFormat = MetalFXFormats.Metal((args.Hudless ?? args.Color).Desc.Format),
            OutputTextureFormat = MetalFXFormats.Metal(Desc.Format),
            DepthTextureFormat = MetalFXFormats.Metal(args.Depth.Desc.Format),
            MotionTextureFormat = MetalFXFormats.Metal(args.MotionVectors.Desc.Format),
            UiTextureFormat = MetalFXFormats.Metal(args.Hudless is not null ? args.Color.Desc.Format : null),
            InputWidth = Desc.RenderWidth,
            InputHeight = Desc.RenderHeight,
            OutputWidth = Desc.OutputWidth,
            OutputHeight = Desc.OutputHeight,
            RequiresPrevColorTexture = false
        };

        interpolator.Dispose();
        interpolator = descriptor.MakeFrameInterpolator(vendor.Device, vendor.Compiler);
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, FrameGenerationArgs args)
    {
        using NSAutoreleasePool _ = new();

        commandBuffer.Transition(Output, default, args.Reset ? TextureLayout.Undefined : TextureLayout.Sampled, TextureLayout.ColorAttachment);

        commandBuffer.BeginExternalCommands();

        UpscalingHelper.DecomposePerspective(args.Projection, out float nearPlane, out float farPlane, out float verticalFieldOfViewInRadians, out float aspectRatio, out bool depthReversed, out bool _);

        interpolator.ColorTexture = (args.Hudless ?? args.Color).Metal();
        interpolator.DepthTexture = args.Depth.Metal();
        interpolator.MotionTexture = args.MotionVectors.Metal();
        interpolator.MotionVectorScaleX = Desc.RenderWidth;
        interpolator.MotionVectorScaleY = Desc.RenderHeight;
        interpolator.DeltaTime = args.DeltaTimeInSeconds;
        interpolator.NearPlane = nearPlane;
        interpolator.FarPlane = farPlane;
        interpolator.FieldOfView = float.RadiansToDegrees(verticalFieldOfViewInRadians);
        interpolator.AspectRatio = aspectRatio;
        interpolator.WorldToViewMatrix = MetalFXFormats.Metal(args.View);
        interpolator.ViewToClipMatrix = MetalFXFormats.Metal(args.Projection);
        interpolator.UiTexture = args.Hudless is not null ? args.Color.Metal() : default;
        interpolator.JitterOffsetX = -args.JitterOffset.X;
        interpolator.JitterOffsetY = -args.JitterOffset.Y;
        interpolator.IsUITextureComposited = args.Hudless is not null;
        interpolator.ShouldResetHistory = args.Reset;
        interpolator.OutputTexture = Output.Metal();
        interpolator.IsDepthReversed = depthReversed;
        interpolator.Encode(commandBuffer.Metal());

        commandBuffer.EndExternalCommands();

        commandBuffer.Transition(Output, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
    }

    protected override void Destroy()
    {
        interpolator.Dispose();

        base.Destroy();
    }
}
