```csharp
using System.Numerics;

namespace Zenith.NET.Extensions.DLSS;

public class DLSSCapabilities
{
    public bool SuperResolutionSupported { get; }

    public bool RayReconstructionSupported { get; }

    public bool FrameGenerationSupported { get; }

    public uint MaxMultiFrameCount { get; }
}

[Flags]
public enum DLSSFeatureFlags
{
    None = 0,

    Hdr = 1 << 0,

    AutoExposure = 1 << 1,

    DepthReversed = 1 << 2,

    MotionVectorJittered = 1 << 3,

    AlphaUpscaling = 1 << 4,

    MotionVectorLowResolution = 1 << 5
}

public struct DLSSSuperResolutionDesc
{
    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public DLSSMode Mode;

    public DLSSFeatureFlags Flags;
}

public struct DLSSSuperResolutionArgs
{
    public DLSSBinding Color;

    public DLSSBinding Depth;

    public DLSSBinding MotionVectors;

    public DLSSBinding? Exposure;

    public DLSSBinding? BiasCurrentColorMask;

    public DLSSBinding Output;

    public Vector2 JitterOffset;

    public Vector2 MotionVectorScale;

    public float PreExposure;

    public float ExposureScale;

    public bool Reset;
}

public struct DLSSRayReconstructionDesc
{
    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public DLSSMode Mode;

    public DLSSDepthType DepthType;

    public DLSSRoughnessMode RoughnessMode;

    public DLSSFeatureFlags Flags;
}

public struct DLSSRayReconstructionArgs
{
    public DLSSBinding Color;

    public DLSSBinding? Alpha;

    public DLSSBinding Depth;

    public DLSSBinding MotionVectors;

    public DLSSBinding DiffuseAlbedo;

    public DLSSBinding SpecularAlbedo;

    public DLSSBinding? ReflectedAlbedo;

    public DLSSBinding Normals;

    public DLSSBinding? Roughness;

    public DLSSBinding? SpecularMotionVectors;

    public DLSSBinding? DiffuseHitDistance;

    public DLSSBinding? SpecularHitDistance;

    public DLSSBinding? DiffuseRayDirection;

    public DLSSBinding? SpecularRayDirection;

    public DLSSBinding? DiffuseRayDirectionHitDistance;

    public DLSSBinding? SpecularRayDirectionHitDistance;

    public DLSSBinding? TransparencyLayer;

    public DLSSBinding? TransparencyLayerOpacity;

    public DLSSBinding? TransparencyLayerMotionVectors;

    public DLSSBinding? ColorBeforeParticles;

    public DLSSBinding? ColorAfterParticles;

    public DLSSBinding? ColorBeforeTransparency;

    public DLSSBinding? ColorAfterTransparency;

    public DLSSBinding? ColorBeforeFog;

    public DLSSBinding? ColorAfterFog;

    public DLSSBinding? ScreenSpaceSubsurfaceScatteringGuide;

    public DLSSBinding? ColorBeforeScreenSpaceSubsurfaceScattering;

    public DLSSBinding? ColorAfterScreenSpaceSubsurfaceScattering;

    public DLSSBinding? ScreenSpaceRefractionGuide;

    public DLSSBinding? ColorBeforeScreenSpaceRefraction;

    public DLSSBinding? ColorAfterScreenSpaceRefraction;

    public DLSSBinding? DepthOfFieldGuide;

    public DLSSBinding? ColorBeforeDepthOfField;

    public DLSSBinding? ColorAfterDepthOfField;

    public DLSSBinding? Exposure;

    public DLSSBinding? BiasCurrentColorMask;

    public DLSSBinding? DisocclusionMask;

    public DLSSBinding? ResponsivityMask;

    public DLSSBinding Output;

    public DLSSBinding? OutputAlpha;

    public Vector2 JitterOffset;

    public Vector2 MotionVectorScale;

    public Matrix4x4 WorldToView;

    public Matrix4x4 ViewToClip;

    public float PreExposure;

    public float ExposureScale;

    public bool Reset;
}

public struct DLSSFrameGenerationDesc
{
    public PixelFormat Format;

    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public bool IsUIRecompositionEnabled;
}

public struct DLSSFrameGenerationArgs
{
    public DLSSBinding Color;

    public DLSSBinding Depth;

    public DLSSBinding MotionVectors;

    public DLSSBinding? HudlessColor;

    public DLSSBinding? UI;

    public DLSSBinding? UIAlpha;

    public DLSSBinding? DistortionField;

    public DLSSBinding Output;

    public Buffer? OutputDisableInterpolation;

    public uint MultiFrameCount;

    public uint MultiFrameIndex;

    public ulong? FrameId;

    public Vector2 JitterOffset;

    public Vector2 MotionVectorScale;

    public float MotionVectorInvalidValue;

    public float DistortionFieldBias;

    public float DistortionFieldScale;

    public Matrix4x4 ViewToClip;

    public Matrix4x4 ClipToView;

    public Matrix4x4 ClipToLensClip;

    public Matrix4x4 ClipToPrevClip;

    public Matrix4x4 PrevClipToClip;

    public Vector3 CameraPosition;

    public Vector3 CameraUp;

    public Vector3 CameraRight;

    public Vector3 CameraForward;

    public Vector2 CameraPinholeOffset;

    public float CameraNear;

    public float CameraFar;

    public float CameraFovAngleVer;

    public float CameraAspectRatio;

    public bool IsHdrDisplay;

    public bool IsDepthReversed;

    public bool IsCameraMotionIncluded;

    public bool IsMotionVectorJittered;

    public bool IsMotionVectorDilated;

    public bool IsDistortionFieldLowPrecision;

    public bool IsOrthographic;

    public bool IsMenuDetectionEnabled;

    public bool IsNotRenderingGameFrames;

    public bool IsAutomodeOverrideReset;

    public bool Reset;
}
```
