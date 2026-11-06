using System.Numerics;

namespace Zenith.NET.Extensions.Upscaling;

public struct FrameGenerationArgs
{
    public Texture Color;

    public Texture Depth;

    public Texture MotionVectors;

    public Texture? Hudless;

    public Texture Output;

    public Vector2 JitterOffset;

    public Matrix4x4 WorldToView;

    public Matrix4x4 ViewToClip;

    public float DeltaTimeInSeconds;

    public float MinLuminanceInNits;

    public float MaxLuminanceInNits;

    public bool Reset;
}
