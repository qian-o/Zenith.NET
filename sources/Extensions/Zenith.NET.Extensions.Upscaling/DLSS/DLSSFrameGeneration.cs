using System.Numerics;
using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal unsafe class DLSSFrameGeneration : FrameGeneration
{
    public DLSSFeature Feature;

    private Matrix4x4 previousWorldToView;
    private Matrix4x4 previousViewToClip;

    public DLSSFrameGeneration(DLSSVendor vendor, FrameGenerationDesc desc) : base(desc)
    {
        Feature = new(vendor);
        Feature.SetUI(Ngx.DLSSGParameterMvecJittered, 0);

        NGXDLSSGCreateParams createParams = new()
        {
            Width = desc.Width,
            Height = desc.Height,
            NativeBackbufferFormat = vendor.Context.GraphicsApi is GraphicsApi.DirectX12 ? DLSSFormats.DirectX12(desc.ColorFormat) : DLSSFormats.Vulkan(desc.ColorFormat).Format,
            RenderWidth = desc.AuxiliaryWidth,
            RenderHeight = desc.AuxiliaryHeight
        };

        vendor.Create(Feature, createParams);
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, FrameGenerationArgs args)
    {
        Matrix4x4.Invert(args.WorldToView, out Matrix4x4 viewToWorld);
        Matrix4x4.Invert(args.ViewToClip, out Matrix4x4 clipToView);

        Matrix4x4 clipToPreviousClip = Matrix4x4.Identity;
        Matrix4x4 previousClipToClip = Matrix4x4.Identity;

        if (!args.Reset)
        {
            Matrix4x4.Invert(previousWorldToView, out Matrix4x4 previousViewToWorld);
            Matrix4x4.Invert(previousViewToClip, out Matrix4x4 previousClipToView);

            // Relative translations keep the view-to-view transform precise far from the world origin.
            Matrix4x4 relativeViewToWorld = viewToWorld;
            relativeViewToWorld.Translation -= previousViewToWorld.Translation;

            Matrix4x4 relativeWorldToPreviousView = previousWorldToView;
            relativeWorldToPreviousView.Translation = Vector3.Zero;

            Matrix4x4 viewToPreviousView = relativeViewToWorld * relativeWorldToPreviousView;
            Matrix4x4.Invert(viewToPreviousView, out Matrix4x4 previousViewToView);

            clipToPreviousClip = clipToView * viewToPreviousView * previousViewToClip;
            previousClipToClip = previousClipToView * previousViewToView * args.ViewToClip;
        }

        (float nearPlane, float farPlane, float verticalFieldOfViewInRadians, float aspectRatio) = UpscalingHelper.DecomposePerspective(args.ViewToClip);

        Vector3 forward = new(viewToWorld.M31, viewToWorld.M32, viewToWorld.M33);

        NGXDLSSGOptEvalParams optEvalParams = new()
        {
            MultiFrameCount = 1,
            MultiFrameIndex = 1,
            CameraViewToClip = args.ViewToClip,
            ClipToCameraView = clipToView,
            ClipToLensClip = Matrix4x4.Identity,
            ClipToPrevClip = clipToPreviousClip,
            PrevClipToClip = previousClipToClip,
            JitterOffset = args.JitterOffset,
            MvecScale = new(Desc.AuxiliaryWidth, Desc.AuxiliaryHeight),
            CameraPos = viewToWorld.Translation,
            CameraUp = new(viewToWorld.M21, viewToWorld.M22, viewToWorld.M23),
            CameraRight = new(viewToWorld.M11, viewToWorld.M12, viewToWorld.M13),
            CameraFwd = args.ViewToClip.M34 < 0.0f ? -forward : forward,
            CameraNear = nearPlane,
            CameraFar = Desc.Flags.HasFlag(UpscalingInputFlags.DepthInfinite) ? float.PositiveInfinity : farPlane,
            CameraFOV = verticalFieldOfViewInRadians,
            CameraAspectRatio = aspectRatio,
            ColorBuffersHDR = Desc.IsHdr,
            DepthInverted = Desc.Flags.HasFlag(UpscalingInputFlags.DepthReversed),
            CameraMotionIncluded = true,
            Reset = args.Reset,
            MvecsSubrectSize = new()
            {
                Width = Desc.AuxiliaryWidth,
                Height = Desc.AuxiliaryHeight
            },
            DepthSubrectSize = new()
            {
                Width = Desc.AuxiliaryWidth,
                Height = Desc.AuxiliaryHeight
            },
            HudLessSubrectSize = new()
            {
                Width = Desc.Width,
                Height = Desc.Height
            },
            MinRelativeLinearDepthObjectSeparation = 40.0f,
            BackbufferSubrectSize = new()
            {
                Width = Desc.Width,
                Height = Desc.Height
            },
            OutputInterpSubrectSize = new()
            {
                Width = Desc.Width,
                Height = Desc.Height
            }
        };

        Feature.Vendor.Evaluate(commandBuffer, Feature, args, &optEvalParams);

        previousWorldToView = args.WorldToView;
        previousViewToClip = args.ViewToClip;
    }

    protected override void Destroy()
    {
        Feature.Dispose();
    }
}
