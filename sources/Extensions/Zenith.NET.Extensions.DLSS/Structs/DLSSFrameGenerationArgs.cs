using System.Numerics;

namespace Zenith.NET.Extensions.DLSS;

public struct DLSSFrameGenerationArgs
{
    public DLSSBinding Color;

    public DLSSBinding Depth;

    public DLSSBinding MotionVectors;

    public DLSSBinding? HudlessColor;

    public DLSSBinding? UI;

    public DLSSBinding? UIAlpha;

    public DLSSBinding? DistortionField;

    public DLSSBinding Output;

    public Buffer? OutputDisableInterpolation;

    public uint MultiFrameCount;

    public uint MultiFrameIndex;

    public ulong? FrameId;

    public Vector2 JitterOffset;

    public Vector2 MotionVectorScale;

    public float MotionVectorInvalidValue;

    public float DistortionFieldBias;

    public float DistortionFieldScale;

    public Matrix4x4 ViewToClip;

    public Matrix4x4 ClipToView;

    public Matrix4x4 ClipToLensClip;

    public Matrix4x4 ClipToPrevClip;

    public Matrix4x4 PrevClipToClip;

    public Vector3 CameraPosition;

    public Vector3 CameraUp;

    public Vector3 CameraRight;

    public Vector3 CameraForward;

    public Vector2 CameraPinholeOffset;

    public float CameraNear;

    public float CameraFar;

    public float CameraFovAngleVer;

    public float CameraAspectRatio;

    public bool IsHdrDisplay;

    public bool IsDepthReversed;

    public bool IsCameraMotionIncluded;

    public bool IsMotionVectorJittered;

    public bool IsMotionVectorDilated;

    public bool IsDistortionFieldLowPrecision;

    public bool IsOrthographic;

    public bool IsMenuDetectionEnabled;

    public bool IsNotRenderingGameFrames;

    public bool IsAutomodeOverrideReset;

    public bool Reset;
}
