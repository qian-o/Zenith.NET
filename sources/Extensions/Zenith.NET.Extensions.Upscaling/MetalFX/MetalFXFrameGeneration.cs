using System.Numerics;
using System.Runtime.CompilerServices;
using Metal.NET;

namespace Zenith.NET.Extensions.Upscaling.MetalFX;

internal class MetalFXFrameGeneration : FrameGeneration
{
    public MTL4FXFrameInterpolator Interpolator;

    public MetalFXFrameGeneration(MetalFXVendor vendor, FrameGenerationDesc desc) : base(desc)
    {
        MTLFXFrameInterpolatorDescriptor descriptor = new()
        {
            ColorTextureFormat = MetalFXFormats.Metal(desc.HudlessFormat ?? desc.ColorFormat),
            OutputTextureFormat = MetalFXFormats.Metal(desc.OutputFormat),
            DepthTextureFormat = MetalFXFormats.Metal(desc.DepthFormat),
            MotionTextureFormat = MetalFXFormats.Metal(desc.MotionVectorsFormat),
            UiTextureFormat = MetalFXFormats.Metal(desc.HudlessFormat is not null ? desc.ColorFormat : null),
            InputWidth = desc.AuxiliaryWidth,
            InputHeight = desc.AuxiliaryHeight,
            OutputWidth = desc.Width,
            OutputHeight = desc.Height,
            RequiresPrevColorTexture = false
        };

        Interpolator = descriptor.MakeFrameInterpolator(vendor.Device, vendor.Compiler);
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, FrameGenerationArgs args)
    {
        using NSAutoreleasePool _ = new();

        MTL4CommandBuffer mtlCommandBuffer = commandBuffer.Metal();

        (float nearPlane, float farPlane, float verticalFieldOfViewInRadians, float aspectRatio) = UpscalingHelper.DecomposePerspective(args.ViewToClip);

        Interpolator.ColorTexture = (args.Hudless ?? args.Color).Metal();
        Interpolator.DepthTexture = args.Depth.Metal();
        Interpolator.MotionTexture = args.MotionVectors.Metal();
        Interpolator.UiTexture = args.Hudless is not null ? args.Color.Metal() : MTLTexture.Null;
        Interpolator.OutputTexture = args.Output.Metal();
        Interpolator.ContentWidth = Desc.AuxiliaryWidth;
        Interpolator.ContentHeight = Desc.AuxiliaryHeight;
        Interpolator.DeltaTime = args.DeltaTimeInSeconds;
        Interpolator.JitterOffsetX = args.JitterOffset.X;
        Interpolator.JitterOffsetY = args.JitterOffset.Y;
        Interpolator.MotionVectorScaleX = Desc.AuxiliaryWidth;
        Interpolator.MotionVectorScaleY = Desc.AuxiliaryHeight;
        Interpolator.WorldToViewMatrix = Unsafe.BitCast<Matrix4x4, SimdFloat4x4>(args.WorldToView);
        Interpolator.ViewToClipMatrix = Unsafe.BitCast<Matrix4x4, SimdFloat4x4>(args.ViewToClip);
        Interpolator.NearPlane = nearPlane;
        Interpolator.FarPlane = Desc.Flags.HasFlag(UpscalingInputFlags.DepthInfinite) ? float.PositiveInfinity : farPlane;
        Interpolator.FieldOfView = float.RadiansToDegrees(verticalFieldOfViewInRadians);
        Interpolator.AspectRatio = aspectRatio;
        Interpolator.IsUITextureComposited = Desc.HudlessFormat is not null;
        Interpolator.ShouldResetHistory = args.Reset;
        Interpolator.IsDepthReversed = Desc.Flags.HasFlag(UpscalingInputFlags.DepthReversed);
        Interpolator.Encode(mtlCommandBuffer);

        MetalFXVendor.EncodeBarrier(mtlCommandBuffer);
    }

    protected override void Destroy()
    {
        Interpolator.Dispose();
    }
}
