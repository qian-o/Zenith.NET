using System.Numerics;

namespace Zenith.NET.Extensions.DLSS;

public struct DLSSRayReconstructionArgs
{
    public DLSSBinding Input;

    public DLSSBinding DiffuseAlbedo;

    public DLSSBinding SpecularAlbedo;

    public DLSSBinding Normals;

    public DLSSBinding Roughness;

    public DLSSBinding Depth;

    public DLSSBinding MotionVectors;

    public DLSSBinding SpecularMotionVectors;

    public DLSSBinding SpecularHitDistance;

    public DLSSBinding TransparencyLayer;

    public DLSSBinding TransparencyLayerOpacity;

    public DLSSBinding ColorBeforeTransparency;

    public DLSSBinding SubsurfaceScatteringGuide;

    public DLSSBinding DepthOfFieldGuide;

    public DLSSBinding Exposure;

    public DLSSBinding ReactiveMask;

    public DLSSBinding Output;

    public float JitterOffsetX;

    public float JitterOffsetY;

    public float MotionVectorScaleX;

    public float MotionVectorScaleY;

    public Matrix4x4 WorldToView;

    public Matrix4x4 ViewToClip;

    public float PreExposure;

    public float ExposureScale;

    public bool Reset;
}
