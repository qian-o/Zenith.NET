using System.Numerics;

namespace Zenith.NET.Extensions.DLSS;

public struct DLSSFrameGenerationArgs
{
    public Texture Color;

    public Texture Depth;

    public Texture MotionVectors;

    public Texture Output;

    public Buffer? OutputDisableInterpolation;

    public Vector2 JitterOffset;

    public Vector2 MotionVectorScale;

    public Matrix4x4 ViewToClip;

    public Matrix4x4 ClipToView;

    public Matrix4x4 ClipToPrevClip;

    public Matrix4x4 PrevClipToClip;

    public Vector3 CameraPosition;

    public Vector3 CameraUp;

    public Vector3 CameraRight;

    public Vector3 CameraForward;

    public float CameraNear;

    public float CameraFar;

    public float CameraFovAngleVer;

    public float CameraAspectRatio;

    public bool IsHdrDisplay;

    public bool IsDepthReversed;

    public bool IsOrthographic;

    public bool Reset;
}
