using System.Numerics;

namespace Zenith.NET.Extensions.Upscaling;

public struct SuperResolutionArgs
{
    public Texture Color;

    public Texture Depth;

    public Texture MotionVectors;

    public Texture? ReactiveMask;

    public Texture? Exposure;

    public Vector2 JitterOffset;

    public Matrix4x4 Projection;

    public float DeltaTimeInSeconds;

    public float? PreExposure;

    public bool Reset;
}
