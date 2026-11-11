using System.Numerics;
using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal unsafe class DLSSFrameGeneration(DLSSVendor vendor, FrameGenerationDesc desc) : FrameGeneration(desc)
{
    private DLSSFeature? feature;
    private PixelFormat colorFormat;
    private bool hasHistory;
    private Matrix4x4 previousView;
    private Matrix4x4 previousProjection;

    protected override void DispatchImpl(CommandBuffer commandBuffer, FrameGenerationArgs args)
    {
        EnsureFeature(args.Color.Desc.Format);

        Matrix4x4.Invert(args.View, out Matrix4x4 viewToWorld);
        Matrix4x4.Invert(args.Projection, out Matrix4x4 clipToView);

        Matrix4x4 clipToPreviousClip = Matrix4x4.Identity;
        Matrix4x4 previousClipToClip = Matrix4x4.Identity;

        if (hasHistory && !args.Reset)
        {
            Matrix4x4.Invert(previousView, out Matrix4x4 previousViewToWorld);
            Matrix4x4.Invert(previousProjection, out Matrix4x4 previousClipToView);

            // Relative translations keep the view-to-view transform precise far from the world origin.
            Matrix4x4 relativeViewToWorld = viewToWorld;
            relativeViewToWorld.Translation -= previousViewToWorld.Translation;

            Matrix4x4 relativeWorldToPreviousView = previousView;
            relativeWorldToPreviousView.Translation = Vector3.Zero;

            Matrix4x4 viewToPreviousView = relativeViewToWorld * relativeWorldToPreviousView;
            Matrix4x4.Invert(viewToPreviousView, out Matrix4x4 previousViewToView);

            clipToPreviousClip = clipToView * viewToPreviousView * previousClipToView;
            previousClipToClip = previousClipToView * previousViewToView * args.Projection;
        }

        (float nearPlane, float farPlane, float verticalFieldOfViewInRadians, float aspectRatio, bool depthReversed, bool depthInfinite) = Vendor.DecomposePerspective(args.Projection);

        Vector3 forward = new(viewToWorld.M31, viewToWorld.M32, viewToWorld.M33);

        NGXDLSSGOptEvalParams optEvalParams = new()
        {
            MultiFrameCount = 1,
            MultiFrameIndex = 1,
            CameraViewToClip = args.Projection,
            ClipToCameraView = clipToView,
            ClipToLensClip = Matrix4x4.Identity,
            ClipToPrevClip = clipToPreviousClip,
            PrevClipToClip = previousClipToClip,
            JitterOffset = -args.JitterOffset,
            MvecScale = new(Desc.RenderWidth, Desc.RenderHeight),
            CameraPos = viewToWorld.Translation,
            CameraUp = new(viewToWorld.M21, viewToWorld.M22, viewToWorld.M23),
            CameraRight = new(viewToWorld.M11, viewToWorld.M12, viewToWorld.M13),
            CameraFwd = args.Projection.M34 < 0.0f ? -forward : forward,
            CameraNear = nearPlane,
            CameraFar = depthInfinite ? float.PositiveInfinity : farPlane,
            CameraFOV = verticalFieldOfViewInRadians,
            CameraAspectRatio = aspectRatio,
            ColorBuffersHDR = args.Color.Desc.Format.IsHdr(),
            DepthInverted = depthReversed,
            CameraMotionIncluded = true,
            Reset = !hasHistory || args.Reset,
            MvecsSubrectSize = new()
            {
                Width = Desc.RenderWidth,
                Height = Desc.RenderHeight
            },
            DepthSubrectSize = new()
            {
                Width = Desc.RenderWidth,
                Height = Desc.RenderHeight
            },
            HudLessSubrectSize = new()
            {
                Width = Desc.OutputWidth,
                Height = Desc.OutputHeight
            },
            MinRelativeLinearDepthObjectSeparation = 40.0f,
            BackbufferSubrectSize = new()
            {
                Width = Desc.OutputWidth,
                Height = Desc.OutputHeight
            },
            OutputInterpSubrectSize = new()
            {
                Width = Desc.OutputWidth,
                Height = Desc.OutputHeight
            }
        };

        vendor.Evaluate(commandBuffer, feature!, args, &optEvalParams);

        hasHistory = true;
        previousView = args.View;
        previousProjection = args.Projection;
    }

    protected override void Destroy()
    {
        feature?.Dispose();
    }

    private void EnsureFeature(PixelFormat nextColorFormat)
    {
        if (feature is not null && colorFormat == nextColorFormat)
        {
            return;
        }

        if (feature is not null)
        {
            vendor.Context.GraphicsQueue.Timeline.Signal().Wait();
            vendor.Context.PresentQueue.Timeline.Signal().Wait();
            feature.Dispose();
            hasHistory = false;
        }

        colorFormat = nextColorFormat;
        feature = new(vendor);
        feature.SetUI(Ngx.DLSSGParameterMvecJittered, 0);

        NGXDLSSGCreateParams createParams = new()
        {
            Width = Desc.OutputWidth,
            Height = Desc.OutputHeight,
            NativeBackbufferFormat = vendor.Context.GraphicsApi is GraphicsApi.DirectX12 ? DLSSFormats.DirectX12(nextColorFormat) : DLSSFormats.Vulkan(nextColorFormat).Format,
            RenderWidth = Desc.RenderWidth,
            RenderHeight = Desc.RenderHeight
        };

        vendor.Create(feature, createParams);
    }
}
