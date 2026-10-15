using System.Numerics;

namespace Zenith.NET.Extensions.DLSS;

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
