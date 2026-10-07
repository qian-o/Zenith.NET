using System.Numerics;

namespace Zenith.NET.Extensions.DLSS;

public struct DLSSFrameGenerationArgs
{
    public DLSSBinding Color;

    public DLSSBinding HudlessColor;

    public DLSSBinding UI;

    public DLSSBinding UIAlpha;

    public DLSSBinding DistortionField;

    public DLSSBinding Depth;

    public DLSSBinding MotionVectors;

    public DLSSBinding Output;

    public uint InputContentWidth;

    public uint InputContentHeight;

    public float JitterOffsetX;

    public float JitterOffsetY;

    public float MotionVectorScaleX;

    public float MotionVectorScaleY;

    public Matrix4x4 ViewToClip;

    public Matrix4x4 ClipToPrevClip;

    public Vector3 CameraPosition;

    public Vector3 CameraUp;

    public Vector3 CameraRight;

    public Vector3 CameraForward;

    public float CameraPinholeOffsetX;

    public float CameraPinholeOffsetY;

    public float CameraNear;

    public float CameraFar;

    public float CameraFovAngleVer;

    public float CameraAspectRatio;

    public bool Orthographic;

    public bool Reset;
}
