using System.Numerics;
using System.Runtime.CompilerServices;
using Metal.NET;

namespace Zenith.NET.Extensions.Upscaling.MetalFX;

internal class MetalFXFrameGenerator : FrameGenerator
{
    private readonly MTL4FXFrameInterpolator interpolator;

    public MetalFXFrameGenerator(MetalFXBackend backend, FrameGeneratorDesc desc) : base(desc)
    {
        MTLFXFrameInterpolatorDescriptor descriptor = new()
        {
            ColorTextureFormat = MetalFXFormats.Metal(desc.ColorFormat),
            OutputTextureFormat = MetalFXFormats.Metal(desc.ColorFormat),
            DepthTextureFormat = MetalFXFormats.Metal(desc.DepthFormat),
            MotionTextureFormat = MetalFXFormats.Metal(desc.MotionVectorsFormat),
            UiTextureFormat = MetalFXFormats.Metal(desc.UIFormat),
            InputWidth = desc.InputWidth,
            InputHeight = desc.InputHeight,
            OutputWidth = desc.OutputWidth,
            OutputHeight = desc.OutputHeight,
            RequiresPrevColorTexture = false
        };

        interpolator = descriptor.MakeFrameInterpolator(backend.Device, backend.Compiler);

        if (interpolator.IsNull)
        {
            throw new InvalidOperationException("MetalFX frame generator creation failed.");
        }
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, FrameGeneratorArgs args)
    {
        using NSAutoreleasePool _ = new();

        MTL4CommandBuffer mtlCommandBuffer = commandBuffer.Metal();

        (float nearPlane, float farPlane, float verticalFieldOfViewInRadians, float aspectRatio) = UpscalingHelper.DecomposePerspective(args.ViewToClip);

        interpolator.ColorTexture = args.Color.Metal();
        interpolator.DepthTexture = args.Depth.Metal();
        interpolator.MotionTexture = args.MotionVectors.Metal();
        interpolator.UiTexture = args.UI?.Metal() ?? MTLTexture.Null;
        interpolator.OutputTexture = args.Output.Metal();
        interpolator.ContentWidth = Desc.InputWidth;
        interpolator.ContentHeight = Desc.InputHeight;
        interpolator.DeltaTime = args.DeltaTimeInSeconds;
        interpolator.JitterOffsetX = args.JitterOffset.X;
        interpolator.JitterOffsetY = args.JitterOffset.Y;
        interpolator.MotionVectorScaleX = args.MotionVectorScale.X;
        interpolator.MotionVectorScaleY = args.MotionVectorScale.Y;
        interpolator.WorldToViewMatrix = Unsafe.BitCast<Matrix4x4, SimdFloat4x4>(args.WorldToView);
        interpolator.ViewToClipMatrix = Unsafe.BitCast<Matrix4x4, SimdFloat4x4>(args.ViewToClip);
        interpolator.NearPlane = nearPlane;
        interpolator.FarPlane = farPlane;
        interpolator.FieldOfView = float.RadiansToDegrees(verticalFieldOfViewInRadians);
        interpolator.AspectRatio = aspectRatio;
        interpolator.IsUITextureComposited = Desc.UIFormat is not null;
        interpolator.ShouldResetHistory = args.Reset;
        interpolator.IsDepthReversed = Desc.Flags.HasFlag(UpscalingInputFlags.DepthReversed);
        interpolator.Encode(mtlCommandBuffer);

        MetalFXBackend.EncodeBarrier(mtlCommandBuffer);
    }

    protected override void Destroy()
    {
        interpolator?.Dispose();
    }
}
