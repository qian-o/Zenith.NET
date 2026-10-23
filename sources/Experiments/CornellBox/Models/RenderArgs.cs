using System.Numerics;
using CornellBox.Handlers;
using Zenith.NET;

namespace CornellBox.Models;

internal readonly ref struct RenderArgs
{
    public SceneResources Scene { get; init; }

    public CameraHandler Camera { get; init; }

    public PathTracingOutput Input { get; init; }

    public Texture Color { get; init; }

    public ReadOnlySpan<Texture> Generated { get; init; }

    public Matrix4x4 View { get; init; }

    public Matrix4x4 Projection { get; init; }

    public Matrix4x4 PreviousView { get; init; }

    public Matrix4x4 PreviousProjection { get; init; }

    public Vector2 JitterOffset { get; init; }

    public uint FrameIndex { get; init; }

    public bool Reset => FrameIndex is 0;
}
