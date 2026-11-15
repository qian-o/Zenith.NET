using System.Numerics;
using System.Runtime.CompilerServices;
using Metal.NET;

namespace Zenith.NET.Extensions.Upscaling.MetalFX;

internal class MetalFXFrameGeneration(MetalFXVendor vendor, FrameGenerationDesc desc) : FrameGeneration(vendor.Context, desc)
{
    private Key key;
    private MTL4FXFrameInterpolator interpolator;

    protected override void DispatchImpl(CommandBuffer commandBuffer, FrameGenerationArgs args)
    {
        using NSAutoreleasePool _ = new();

        if (Ensure(args))
        {
            args.Reset = true;
        }

        commandBuffer.Transition(Output, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);

        commandBuffer.BeginExternalCommands();

        UpscalingHelper.DecomposePerspective(args.Projection, out float nearPlane, out float farPlane, out float verticalFieldOfViewInRadians, out float aspectRatio, out bool depthReversed, out bool depthInfinite);

        interpolator.ColorTexture = (args.Hudless ?? args.Color).Metal();
        interpolator.DepthTexture = args.Depth.Metal();
        interpolator.MotionTexture = args.MotionVectors.Metal();
        interpolator.MotionVectorScaleX = Desc.RenderWidth;
        interpolator.MotionVectorScaleY = Desc.RenderHeight;
        interpolator.DeltaTime = args.DeltaTimeInSeconds;
        interpolator.NearPlane = nearPlane;
        interpolator.FarPlane = depthInfinite ? float.PositiveInfinity : farPlane;
        interpolator.FieldOfView = float.RadiansToDegrees(verticalFieldOfViewInRadians);
        interpolator.AspectRatio = aspectRatio;
        interpolator.WorldToViewMatrix = Unsafe.BitCast<Matrix4x4, SimdFloat4x4>(args.View);
        interpolator.ViewToClipMatrix = Unsafe.BitCast<Matrix4x4, SimdFloat4x4>(args.Projection);
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

    private bool Ensure(FrameGenerationArgs args)
    {
        Key newKey = new(args);

        if (key.Equals(newKey))
        {
            return false;
        }

        key = newKey;

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

        return true;
    }

    private readonly struct Key(FrameGenerationArgs args)
    {
        public readonly MTLPixelFormat Color = MetalFXFormats.Metal(args.Color.Desc.Format);

        public readonly MTLPixelFormat Depth = MetalFXFormats.Metal(args.Depth.Desc.Format);

        public readonly MTLPixelFormat MotionVectors = MetalFXFormats.Metal(args.MotionVectors.Desc.Format);

        public readonly MTLPixelFormat Hudless = MetalFXFormats.Metal(args.Hudless?.Desc.Format);

        public bool Equals(Key other)
        {
            return Color == other.Color && Depth == other.Depth && MotionVectors == other.MotionVectors && Hudless == other.Hudless;
        }
    }
}
