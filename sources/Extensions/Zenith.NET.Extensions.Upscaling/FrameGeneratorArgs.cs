using System.Numerics;

namespace Zenith.NET.Extensions.Upscaling;

public struct FrameGeneratorArgs
{
    public Texture Color;

    public Texture Depth;

    public Texture MotionVectors;

    public Texture? UI;

    public Texture Output;

    public float DeltaTimeInSeconds;

    public Vector2 JitterOffset;

    public Vector2 MotionVectorScale;

    public Matrix4x4 WorldToView;

    public Matrix4x4 ViewToClip;

    public bool Reset;
}
