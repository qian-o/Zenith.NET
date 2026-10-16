```csharp
using System.Numerics;

namespace Zenith.NET.Extensions.DLSS;

public class DLSSCapabilities
{
    public bool SuperResolutionSupported { get; }

    public bool RayReconstructionSupported { get; }

    public bool FrameGenerationSupported { get; }
}

[Flags]
public enum DLSSSuperResolutionFlags
{
    None = 0,

    AutoExposure = 1 << 0,

    DepthReversed = 1 << 1
}

[Flags]
public enum DLSSRayReconstructionFlags
{
    None = 0,

    DepthReversed = 1 << 0
}

public struct DLSSSuperResolutionDesc
{
    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public DLSSMode Mode;

    public DLSSSuperResolutionFlags Flags;
}

public struct DLSSSuperResolutionArgs
{
    public DLSSBinding Color;

    public DLSSBinding Depth;

    public DLSSBinding MotionVectors;

    public DLSSBinding? Exposure;

    public DLSSBinding Output;

    public Vector2 JitterOffset;

    public Vector2 MotionVectorScale;

    public float PreExposure;

    public bool Reset;
}

public struct DLSSRayReconstructionDesc
{
    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public DLSSMode Mode;

    public DLSSRayReconstructionFlags Flags;
}

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

public struct DLSSFrameGenerationDesc
{
    public PixelFormat Format;

    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;
}

public struct DLSSFrameGenerationArgs
{
    public DLSSBinding Color;

    public DLSSBinding Depth;

    public DLSSBinding MotionVectors;

    public DLSSBinding Output;

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
```
