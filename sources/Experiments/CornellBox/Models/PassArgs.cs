using System.Numerics;
using Zenith.NET;

namespace CornellBox.Models;

internal readonly struct PassArgs
{
    public Texture Color { get; init; }

    public Texture ResolvedColor { get; init; }

    public Texture Normal { get; init; }

    public Texture Depth { get; init; }

    public Texture MotionVectors { get; init; }

    public Matrix4x4 InverseView { get; init; }

    public Matrix4x4 InverseProjection { get; init; }

    public Matrix4x4 ViewProjection { get; init; }

    public Matrix4x4 PreviousViewProjection { get; init; }

    public Matrix4x4 ClipToPrevClip { get; init; }

    public Vector3 CameraPosition { get; init; }

    public float CameraFovAngleHor { get; init; }

    public Vector2 Jitter { get; init; }

    public Vector2 PreviousJitter { get; init; }

    public uint FrameIndex { get; init; }

    public bool SameCamera { get; init; }

    public UpscaleMode UpscaleMode { get; init; }
}
