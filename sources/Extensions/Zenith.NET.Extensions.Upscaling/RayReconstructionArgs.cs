using System.Numerics;

namespace Zenith.NET.Extensions.Upscaling;

public struct RayReconstructionArgs
{
    public Texture Color;

    public Texture Depth;

    public Texture MotionVectors;

    public Texture DiffuseAlbedo;

    public Texture SpecularAlbedo;

    public Texture Normals;

    public Texture Roughness;

    public Texture SpecularHitDistance;

    public Texture? LinearDepthDelta;

    public Texture? DirectDiffuseRadiance;

    public Texture? DirectSpecularRadiance;

    public Texture? IndirectDiffuseRadiance;

    public Texture? IndirectSpecularRadiance;

    public Texture? ReactiveMask;

    public Texture? TransparencyOverlay;

    public Texture? Exposure;

    public Texture Output;

    public Vector2 JitterOffset;

    public Matrix4x4 WorldToView;

    public Matrix4x4 ViewToClip;

    public float DeltaTimeInSeconds;

    public float PreExposure;

    public bool Reset;
}
