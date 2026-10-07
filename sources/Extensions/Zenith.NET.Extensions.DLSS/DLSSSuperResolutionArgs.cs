namespace Zenith.NET.Extensions.DLSS;

public struct DLSSSuperResolutionArgs
{
    public DLSSBinding Input;

    public DLSSBinding Depth;

    public DLSSBinding MotionVectors;

    public DLSSBinding Exposure;

    public DLSSBinding ReactiveMask;

    public DLSSBinding Output;

    public uint InputContentWidth;

    public uint InputContentHeight;

    public float JitterOffsetX;

    public float JitterOffsetY;

    public float MotionVectorScaleX;

    public float MotionVectorScaleY;

    public float PreExposure;

    public float ExposureScale;

    public bool Reset;
}
