using System.Numerics;
using CornellBox.Handlers;

namespace CornellBox.Models;

internal readonly struct RenderArgs
{
    public CameraHandler Camera { get; init; }

    public Matrix4x4 View { get; init; }

    public Matrix4x4 Projection { get; init; }

    public Matrix4x4 ViewProjection { get; init; }

    public Matrix4x4 PreviousViewProjection { get; init; }

    public Vector2 JitterOffset { get; init; }

    public uint FrameIndex { get; init; }

    public bool Reset { get; init; }
}
