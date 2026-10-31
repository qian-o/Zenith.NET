using System.Numerics;
using NGX.NET;

namespace Zenith.NET.Extensions.Reconstruction.DLSS;

internal unsafe class DLSSFrameGenerator : FrameGenerator
{
    private readonly DLSSContext context;
    private readonly DLSSFeature feature;

    private Matrix4x4 previousWorldToView;
    private Matrix4x4 previousViewToClip;

    public DLSSFrameGenerator(DLSSContext context, FrameGeneratorDesc desc) : base(desc)
    {
        this.context = context;

        feature = new(context);
        feature.SetUI(Ngx.DLSSGParameterMvecJittered, 0);

        NGXDLSSGCreateParams createParams = new()
        {
            Width = desc.OutputWidth,
            Height = desc.OutputHeight,
            NativeBackbufferFormat = context.Context.GraphicsApi is GraphicsApi.DirectX12 ? DLSSFormats.DirectX12(desc.ColorFormat) : DLSSFormats.Vulkan(desc.ColorFormat).Format,
            RenderWidth = desc.InputWidth,
            RenderHeight = desc.InputHeight
        };

        context.Create(feature, createParams);
    }

    protected override void DispatchImpl(CommandBuffer commandBuffer, FrameGeneratorArgs args)
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

        (float nearPlane, float farPlane, float verticalFieldOfViewInRadians, float aspectRatio) = args.ViewToClip.DecomposePerspective();

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
            MvecScale = args.MotionVectorScale,
            CameraPos = viewToWorld.Translation,
            CameraUp = new(viewToWorld.M21, viewToWorld.M22, viewToWorld.M23),
            CameraRight = new(viewToWorld.M11, viewToWorld.M12, viewToWorld.M13),
            CameraFwd = args.ViewToClip.M34 < 0.0f ? -forward : forward,
            CameraNear = nearPlane,
            CameraFar = farPlane,
            CameraFOV = verticalFieldOfViewInRadians,
            CameraAspectRatio = aspectRatio,
            DepthInverted = Desc.Flags.HasFlag(ReconstructionFlags.DepthReversed),
            CameraMotionIncluded = true,
            Reset = args.Reset,
            MvecsSubrectSize = new()
            {
                Width = Desc.InputWidth,
                Height = Desc.InputHeight
            },
            DepthSubrectSize = new()
            {
                Width = Desc.InputWidth,
                Height = Desc.InputHeight
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

        context.Evaluate(commandBuffer, feature, args, &optEvalParams);

        previousWorldToView = args.WorldToView;
        previousViewToClip = args.ViewToClip;
    }

    protected override void Destroy()
    {
        feature.Dispose();
    }
}
