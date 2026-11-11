using System.Numerics;
using System.Runtime.CompilerServices;
using Metal.NET;

namespace Zenith.NET.Extensions.Upscaling.MetalFX;

internal class MetalFXFrameGeneration(MetalFXVendor vendor, FrameGenerationDesc desc) : FrameGeneration(desc)
{
    private MTL4FXFrameInterpolator interpolator;
    private (PixelFormat Color, PixelFormat Depth, PixelFormat MotionVectors, PixelFormat? Hudless, PixelFormat Output) key;
    private bool recreated;

    protected override void DispatchImpl(CommandBuffer commandBuffer, FrameGenerationArgs args)
    {
        using NSAutoreleasePool pool = new();

        EnsureInterpolator((args.Color.Desc.Format, args.Depth.Desc.Format, args.MotionVectors.Desc.Format, args.Hudless?.Desc.Format, args.Output.Desc.Format));

        if (recreated)
        {
            args.Reset = true;
            recreated = false;
        }

        commandBuffer.BeginExternalCommands();

        Encode(commandBuffer.Metal(), args);

        commandBuffer.EndExternalCommands();
    }

    protected override void Destroy()
    {
        if (!interpolator.IsNull)
        {
            interpolator.Dispose();
        }
    }

    private void EnsureInterpolator((PixelFormat Color, PixelFormat Depth, PixelFormat MotionVectors, PixelFormat? Hudless, PixelFormat Output) nextKey)
    {
        if (!interpolator.IsNull && key == nextKey)
        {
            return;
        }

        if (!interpolator.IsNull)
        {
            vendor.Context.GraphicsQueue.Timeline.Signal().Wait();
            vendor.Context.PresentQueue.Timeline.Signal().Wait();
            interpolator.Dispose();
        }

        key = nextKey;

        using MTLFXFrameInterpolatorDescriptor descriptor = new()
        {
            ColorTextureFormat = MetalFXFormats.Metal(nextKey.Hudless ?? nextKey.Color),
            OutputTextureFormat = MetalFXFormats.Metal(nextKey.Output),
            DepthTextureFormat = MetalFXFormats.Metal(nextKey.Depth),
            MotionTextureFormat = MetalFXFormats.Metal(nextKey.MotionVectors),
            UiTextureFormat = MetalFXFormats.Metal(nextKey.Hudless is not null ? nextKey.Color : null),
            InputWidth = Desc.RenderWidth,
            InputHeight = Desc.RenderHeight,
            OutputWidth = Desc.OutputWidth,
            OutputHeight = Desc.OutputHeight,
            RequiresPrevColorTexture = false
        };
        interpolator = descriptor.MakeFrameInterpolator(vendor.Device, vendor.Compiler);

        WarmUp(nextKey);
        recreated = true;
    }

    private void Encode(MTL4CommandBuffer commandBuffer, FrameGenerationArgs args)
    {
        (float nearPlane, float farPlane, float verticalFieldOfViewInRadians, float aspectRatio, bool depthReversed, bool depthInfinite) = Vendor.DecomposePerspective(args.Projection);

        interpolator.ColorTexture = (args.Hudless ?? args.Color).Metal();
        interpolator.DepthTexture = args.Depth.Metal();
        interpolator.MotionTexture = args.MotionVectors.Metal();
        interpolator.UiTexture = args.Hudless is not null ? args.Color.Metal() : default;
        interpolator.OutputTexture = args.Output.Metal();
        interpolator.ContentWidth = Desc.RenderWidth;
        interpolator.ContentHeight = Desc.RenderHeight;
        interpolator.DeltaTime = args.DeltaTimeInSeconds;
        interpolator.JitterOffsetX = -args.JitterOffset.X;
        interpolator.JitterOffsetY = -args.JitterOffset.Y;
        interpolator.MotionVectorScaleX = Desc.RenderWidth;
        interpolator.MotionVectorScaleY = Desc.RenderHeight;
        interpolator.WorldToViewMatrix = Unsafe.BitCast<Matrix4x4, SimdFloat4x4>(args.View);
        interpolator.ViewToClipMatrix = Unsafe.BitCast<Matrix4x4, SimdFloat4x4>(args.Projection);
        interpolator.NearPlane = nearPlane;
        interpolator.FarPlane = depthInfinite ? float.PositiveInfinity : farPlane;
        interpolator.FieldOfView = float.RadiansToDegrees(verticalFieldOfViewInRadians);
        interpolator.AspectRatio = aspectRatio;
        interpolator.IsUITextureComposited = args.Hudless is not null;
        interpolator.ShouldResetHistory = args.Reset;
        interpolator.IsDepthReversed = depthReversed;
        interpolator.Encode(commandBuffer);
    }

    private void WarmUp((PixelFormat Color, PixelFormat Depth, PixelFormat MotionVectors, PixelFormat? Hudless, PixelFormat Output) nextKey)
    {
        using Texture color = vendor.CreateTexture(nextKey.Color, Desc.OutputWidth, Desc.OutputHeight);
        using Texture depth = vendor.CreateTexture(nextKey.Depth, Desc.RenderWidth, Desc.RenderHeight);
        using Texture motionVectors = vendor.CreateTexture(nextKey.MotionVectors, Desc.RenderWidth, Desc.RenderHeight);
        using Texture? hudless = nextKey.Hudless is PixelFormat hudlessFormat ? vendor.CreateTexture(hudlessFormat, Desc.OutputWidth, Desc.OutputHeight) : null;
        using Texture output = vendor.CreateTexture(nextKey.Output, Desc.OutputWidth, Desc.OutputHeight);

        vendor.WarmUp(commandBuffer => Encode(commandBuffer, new()
        {
            Color = color,
            Depth = depth,
            MotionVectors = motionVectors,
            Hudless = hudless,
            Output = output,
            View = Matrix4x4.Identity,
            Projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4.0f, (float)Desc.OutputWidth / Desc.OutputHeight, 0.1f, 100.0f),
            DeltaTimeInSeconds = 1.0f / 60.0f,
            Reset = true
        }));
    }
}
