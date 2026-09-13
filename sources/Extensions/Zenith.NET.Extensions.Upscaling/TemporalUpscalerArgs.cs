using System.Numerics;

namespace Zenith.NET.Extensions.Upscaling;

public struct TemporalUpscalerArgs
{
    public ResourceHandle Input;

    public ResourceHandle OpaqueInput;

    public ResourceHandle Depth;

    public ResourceHandle MotionVectors;

    public ResourceHandle Output;

    public float JitterOffsetX;

    public float JitterOffsetY;

    public Matrix4x4 ClipToPrevClip;

    public float PreExposure;

    public float CameraFovAngleHor;

    public float MinLerpContribution;

    public bool SameCamera;

    public bool Reset;
}
