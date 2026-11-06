using System.Numerics;
using Zenith.NET;

namespace CornellBox.Models;

internal readonly struct RenderedFrame
{
    public Texture Color { get; init; }

    public Texture Depth { get; init; }

    public Texture MotionVectors { get; init; }

    public Vector2 JitterOffset { get; init; }

    public Matrix4x4 WorldToView { get; init; }

    public Matrix4x4 ViewToClip { get; init; }

    public float DeltaTimeInSeconds { get; init; }

    public bool Reset { get; init; }
}
