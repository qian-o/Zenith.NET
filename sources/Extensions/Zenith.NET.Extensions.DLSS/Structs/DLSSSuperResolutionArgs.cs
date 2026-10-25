using System.Numerics;

namespace Zenith.NET.Extensions.DLSS;

public struct DLSSSuperResolutionArgs
{
    public Texture Color;

    public Texture Depth;

    public Texture MotionVectors;

    public Texture? Exposure;

    public Texture Output;

    public Vector2 JitterOffset;

    public Vector2 MotionVectorScale;

    public float PreExposure;

    public bool Reset;
}
