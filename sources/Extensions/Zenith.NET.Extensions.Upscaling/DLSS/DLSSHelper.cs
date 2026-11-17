using System.Numerics;
using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal static unsafe class DLSSHelper
{
    public static int FeatureFlags(PixelFormat colorFormat, Texture? exposure, Matrix4x4 projection)
    {
        NGXDLSSFeatureFlags flags = NGXDLSSFeatureFlags.MvLowRes;

        if (UpscalingHelper.IsFloat(colorFormat))
        {
            flags |= NGXDLSSFeatureFlags.IsHdr;
        }

        if (exposure is null)
        {
            flags |= NGXDLSSFeatureFlags.AutoExposure;
        }

        if (UpscalingHelper.IsDepthReversed(projection))
        {
            flags |= NGXDLSSFeatureFlags.DepthInverted;
        }

        return (int)flags;
    }

    public static bool GetBool(NGXParameter parameters, ReadOnlySpan<byte> name)
    {
        fixed (byte* pointer = name)
        {
            int value = 0;
            Ngx.Parameter.GetI(parameters, pointer, &value).Success();

            return value is not 0;
        }
    }

    public static NGXDLSSGOptEvalParams OptEvalParams(Matrix4x4 previousView, Matrix4x4 previousProjection, FrameGenerationArgs args, Texture output)
    {
        const float FallbackInfiniteFarScale = 100000.0f;

        UpscalingHelper.DecomposePerspective(args.Projection, out float nearPlane, out float farPlane, out float verticalFieldOfViewInRadians, out float aspectRatio, out bool depthReversed, out bool depthInfinite);

        Matrix4x4.Invert(args.View, out Matrix4x4 viewToWorld);
        Matrix4x4.Invert(args.Projection, out Matrix4x4 clipToCameraView);

        Matrix4x4 clipToPrevClip = Matrix4x4.Identity;
        Matrix4x4 prevClipToClip = Matrix4x4.Identity;
        if (!args.Reset)
        {
            Matrix4x4.Invert(previousView, out Matrix4x4 previousViewToWorld);
            Matrix4x4.Invert(previousProjection, out Matrix4x4 previousClipToView);

            Matrix4x4 relativeViewToWorld = viewToWorld;
            relativeViewToWorld.Translation -= previousViewToWorld.Translation;

            Matrix4x4 relativeWorldToPreviousView = previousView;
            relativeWorldToPreviousView.Translation = Vector3.Zero;

            Matrix4x4 viewToPreviousView = relativeViewToWorld * relativeWorldToPreviousView;
            Matrix4x4.Invert(viewToPreviousView, out Matrix4x4 previousViewToView);

            clipToPrevClip = clipToCameraView * viewToPreviousView * previousProjection;
            prevClipToClip = previousClipToView * previousViewToView * args.Projection;
        }

        Vector3 forward = new(viewToWorld.M31, viewToWorld.M32, viewToWorld.M33);

        return new()
        {
            CameraViewToClip = args.Projection,
            ClipToCameraView = clipToCameraView,
            ClipToLensClip = Matrix4x4.Identity,
            ClipToPrevClip = clipToPrevClip,
            PrevClipToClip = prevClipToClip,
            JitterOffset = -args.JitterOffset,
            MvecScale = new(args.MotionVectors.Desc.Width, args.MotionVectors.Desc.Height),
            CameraPos = viewToWorld.Translation,
            CameraUp = Vector3.Normalize(new(viewToWorld.M21, viewToWorld.M22, viewToWorld.M23)),
            CameraRight = Vector3.Normalize(new(viewToWorld.M11, viewToWorld.M12, viewToWorld.M13)),
            CameraFwd = Vector3.Normalize(args.Projection.M34 < 0.0f ? -forward : forward),
            CameraNear = nearPlane,
            CameraFar = depthInfinite ? MathF.Max(nearPlane * FallbackInfiniteFarScale, FallbackInfiniteFarScale) : farPlane,
            CameraFOV = verticalFieldOfViewInRadians,
            CameraAspectRatio = aspectRatio,
            ColorBuffersHDR = args.Color.Desc.Format is PixelFormat.R10G10B10A2UNorm || UpscalingHelper.IsFloat(args.Color.Desc.Format),
            DepthInverted = depthReversed,
            CameraMotionIncluded = true,
            Reset = args.Reset,
            MvecsSubrectSize = Dimensions(args.MotionVectors),
            DepthSubrectSize = Dimensions(args.Depth),
            HudLessSubrectSize = args.Hudless is not null ? Dimensions(args.Hudless) : default,
            BackbufferSubrectSize = Dimensions(args.Color),
            OutputInterpSubrectSize = Dimensions(output)
        };
    }

    public static NGXPerfQualityValue PerfQuality(uint renderWidth, uint renderHeight, uint outputWidth, uint outputHeight)
    {
        float scale = MathF.Sqrt((float)outputWidth * outputHeight / Math.Max(renderWidth * renderHeight, 1));

        if (scale <= 1.25f)
        {
            return NGXPerfQualityValue.Dlaa;
        }

        if (scale <= 1.6f)
        {
            return NGXPerfQualityValue.MaxQuality;
        }

        if (scale <= 1.85f)
        {
            return NGXPerfQualityValue.Balanced;
        }

        if (scale <= 2.5f)
        {
            return NGXPerfQualityValue.MaxPerf;
        }

        return NGXPerfQualityValue.UltraPerformance;
    }

    public static void ResetParameters(NGXParameter parameters)
    {
        Ngx.Parameter.Reset(parameters);
        SetUI(parameters, Ngx.ParameterFreeMemOnReleaseFeature, 1);
    }

    public static void SetUI(NGXParameter parameters, ReadOnlySpan<byte> name, uint value)
    {
        fixed (byte* pointer = name)
        {
            Ngx.Parameter.SetUI(parameters, pointer, value);
        }
    }

    private static NGXDimensions Dimensions(Texture texture)
    {
        return new()
        {
            Width = texture.Desc.Width,
            Height = texture.Desc.Height
        };
    }
}
