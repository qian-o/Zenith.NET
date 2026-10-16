using System.Numerics;

namespace Zenith.NET.Extensions.DLSS;

public struct DLSSRayReconstructionArgs
{
    public DLSSBinding Color;

    public DLSSBinding Depth;

    public DLSSBinding MotionVectors;

    public DLSSBinding DiffuseAlbedo;

    public DLSSBinding SpecularAlbedo;

    public DLSSBinding Normals;

    public DLSSBinding Roughness;

    public DLSSBinding SpecularHitDistance;

    public DLSSBinding? TransparencyLayer;

    public DLSSBinding Output;

    public Vector2 JitterOffset;

    public Vector2 MotionVectorScale;

    public Matrix4x4 WorldToView;

    public Matrix4x4 ViewToClip;

    public float PreExposure;

    public bool Reset;
}
