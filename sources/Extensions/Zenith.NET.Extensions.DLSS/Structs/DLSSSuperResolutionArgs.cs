using System.Numerics;

namespace Zenith.NET.Extensions.DLSS;

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
