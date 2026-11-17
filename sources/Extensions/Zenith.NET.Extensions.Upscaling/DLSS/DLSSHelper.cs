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

            return Ngx.NVSDK_NGX_SUCCEED(Ngx.Parameter.GetI(parameters, pointer, &value)) && value is not 0;
        }
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
}
