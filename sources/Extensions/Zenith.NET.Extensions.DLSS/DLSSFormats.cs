using NGX.NET;

namespace Zenith.NET.Extensions.DLSS;

internal static class DLSSFormats
{
    public static uint DirectX12(PixelFormat pixelFormat)
    {
        return pixelFormat switch
        {
            PixelFormat.R8UNorm => 61,
            PixelFormat.R8SNorm => 63,
            PixelFormat.R8UInt => 62,
            PixelFormat.R8SInt => 64,

            PixelFormat.R16UNorm => 56,
            PixelFormat.R16SNorm => 58,
            PixelFormat.R16UInt => 57,
            PixelFormat.R16SInt => 59,
            PixelFormat.R16Float => 54,

            PixelFormat.R32UInt => 42,
            PixelFormat.R32SInt => 43,
            PixelFormat.R32Float => 41,

            PixelFormat.R8G8UNorm => 49,
            PixelFormat.R8G8SNorm => 51,
            PixelFormat.R8G8UInt => 50,
            PixelFormat.R8G8SInt => 52,

            PixelFormat.R16G16UNorm => 35,
            PixelFormat.R16G16SNorm => 37,
            PixelFormat.R16G16UInt => 36,
            PixelFormat.R16G16SInt => 38,
            PixelFormat.R16G16Float => 34,

            PixelFormat.R32G32UInt => 17,
            PixelFormat.R32G32SInt => 18,
            PixelFormat.R32G32Float => 16,

            PixelFormat.R32G32B32UInt => 7,
            PixelFormat.R32G32B32SInt => 8,
            PixelFormat.R32G32B32Float => 6,

            PixelFormat.R8G8B8A8UNorm => 28,
            PixelFormat.R8G8B8A8SNorm => 31,
            PixelFormat.R8G8B8A8UInt => 30,
            PixelFormat.R8G8B8A8SInt => 32,
            PixelFormat.R8G8B8A8SRgb => 29,

            PixelFormat.R16G16B16A16UNorm => 11,
            PixelFormat.R16G16B16A16SNorm => 13,
            PixelFormat.R16G16B16A16UInt => 12,
            PixelFormat.R16G16B16A16SInt => 14,
            PixelFormat.R16G16B16A16Float => 10,

            PixelFormat.R32G32B32A32UInt => 3,
            PixelFormat.R32G32B32A32SInt => 4,
            PixelFormat.R32G32B32A32Float => 2,

            PixelFormat.B8G8R8A8UNorm => 87,
            PixelFormat.B8G8R8A8SRgb => 91,

            PixelFormat.D16UNorm => 55,
            PixelFormat.D24UNormS8UInt => 45,
            PixelFormat.D32Float => 40,
            PixelFormat.D32FloatS8UInt => 20,

            _ => default
        };
    }

    public static (uint Format, uint AspectFlags) Vulkan(PixelFormat pixelFormat)
    {
        uint format = pixelFormat switch
        {
            PixelFormat.R8UNorm => 9,
            PixelFormat.R8SNorm => 10,
            PixelFormat.R8UInt => 13,
            PixelFormat.R8SInt => 14,

            PixelFormat.R16UNorm => 70,
            PixelFormat.R16SNorm => 71,
            PixelFormat.R16UInt => 74,
            PixelFormat.R16SInt => 75,
            PixelFormat.R16Float => 76,

            PixelFormat.R32UInt => 98,
            PixelFormat.R32SInt => 99,
            PixelFormat.R32Float => 100,

            PixelFormat.R8G8UNorm => 16,
            PixelFormat.R8G8SNorm => 17,
            PixelFormat.R8G8UInt => 20,
            PixelFormat.R8G8SInt => 21,

            PixelFormat.R16G16UNorm => 77,
            PixelFormat.R16G16SNorm => 78,
            PixelFormat.R16G16UInt => 81,
            PixelFormat.R16G16SInt => 82,
            PixelFormat.R16G16Float => 83,

            PixelFormat.R32G32UInt => 101,
            PixelFormat.R32G32SInt => 102,
            PixelFormat.R32G32Float => 103,

            PixelFormat.R32G32B32UInt => 104,
            PixelFormat.R32G32B32SInt => 105,
            PixelFormat.R32G32B32Float => 106,

            PixelFormat.R8G8B8A8UNorm => 37,
            PixelFormat.R8G8B8A8SNorm => 38,
            PixelFormat.R8G8B8A8UInt => 41,
            PixelFormat.R8G8B8A8SInt => 42,
            PixelFormat.R8G8B8A8SRgb => 43,

            PixelFormat.R16G16B16A16UNorm => 91,
            PixelFormat.R16G16B16A16SNorm => 92,
            PixelFormat.R16G16B16A16UInt => 95,
            PixelFormat.R16G16B16A16SInt => 96,
            PixelFormat.R16G16B16A16Float => 97,

            PixelFormat.R32G32B32A32UInt => 107,
            PixelFormat.R32G32B32A32SInt => 108,
            PixelFormat.R32G32B32A32Float => 109,

            PixelFormat.B8G8R8A8UNorm => 44,
            PixelFormat.B8G8R8A8SRgb => 50,

            PixelFormat.D16UNorm => 124,
            PixelFormat.D24UNormS8UInt => 129,
            PixelFormat.D32Float => 126,
            PixelFormat.D32FloatS8UInt => 130,

            _ => default
        };

        return (format, ZenithHelper.HasDepth(pixelFormat) ? 2u : 1u);
    }

    public static NGXPerfQualityValue PerfQualityValue(DLSSMode mode)
    {
        return mode switch
        {
            DLSSMode.DLAA => NGXPerfQualityValue.DLAA,
            DLSSMode.UltraQuality => NGXPerfQualityValue.UltraQuality,
            DLSSMode.Quality => NGXPerfQualityValue.MaxQuality,
            DLSSMode.Balanced => NGXPerfQualityValue.Balanced,
            DLSSMode.Performance => NGXPerfQualityValue.MaxPerf,
            DLSSMode.UltraPerformance => NGXPerfQualityValue.UltraPerformance,
            _ => default
        };
    }

    public static NGXDLSSFeatureFlags FeatureFlags(DLSSSuperResolutionDesc desc)
    {
        NGXDLSSFeatureFlags result = NGXDLSSFeatureFlags.MVLowRes;

        if (desc.IsHdr)
        {
            result |= NGXDLSSFeatureFlags.IsHDR;
        }

        if (desc.IsAutoExposureEnabled)
        {
            result |= NGXDLSSFeatureFlags.AutoExposure;
        }

        if (desc.IsDepthReversed)
        {
            result |= NGXDLSSFeatureFlags.DepthInverted;
        }

        if (desc.IsMotionVectorJittered)
        {
            result |= NGXDLSSFeatureFlags.MVJittered;
        }

        if (desc.IsAlphaUpscalingEnabled)
        {
            result |= NGXDLSSFeatureFlags.AlphaUpscaling;
        }

        return result;
    }

    public static NGXDLSSFeatureFlags FeatureFlags(DLSSRayReconstructionDesc desc)
    {
        NGXDLSSFeatureFlags result = NGXDLSSFeatureFlags.IsHDR | NGXDLSSFeatureFlags.MVLowRes;

        if (desc.IsDepthReversed)
        {
            result |= NGXDLSSFeatureFlags.DepthInverted;
        }

        if (desc.IsMotionVectorJittered)
        {
            result |= NGXDLSSFeatureFlags.MVJittered;
        }

        if (desc.IsAlphaUpscalingEnabled)
        {
            result |= NGXDLSSFeatureFlags.AlphaUpscaling;
        }

        return result;
    }

    public static NGXDLSSDepthType DepthType(DLSSRayReconstructionDesc desc)
    {
        return desc.IsDepthLinear ? NGXDLSSDepthType.Linear : NGXDLSSDepthType.HW;
    }

    public static NGXDLSSRoughnessMode RoughnessMode(DLSSRayReconstructionDesc desc)
    {
        return desc.IsRoughnessPacked ? NGXDLSSRoughnessMode.Packed : NGXDLSSRoughnessMode.Unpacked;
    }
}
