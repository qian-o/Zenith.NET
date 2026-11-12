using System.Numerics;

namespace Zenith.NET.Extensions.Upscaling;

public struct FrameGenerationSwapChainArgs
{
    public Texture Depth;

    public Texture MotionVectors;

    public Texture? Hudless;

    public Vector2 JitterOffset;

    public Matrix4x4 View;

    public Matrix4x4 Projection;

    public float DeltaTimeInSeconds;

    public float? MinLuminanceInNits;

    public float? MaxLuminanceInNits;

    public bool Reset;
}
