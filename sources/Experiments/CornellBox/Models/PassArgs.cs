using System.Numerics;
using Zenith.NET;
using Zenith.NET.Extensions.DLSS;

namespace CornellBox.Models;

internal readonly struct PassArgs
{
    public Scene Scene { get; init; }

    public Texture Color { get; init; }

    public Texture Normal { get; init; }

    public Texture Depth { get; init; }

    public Texture MotionVectors { get; init; }

    public Texture DiffuseAlbedo { get; init; }

    public Texture SpecularAlbedo { get; init; }

    public Texture SpecularHitDistance { get; init; }

    public Texture UI { get; init; }

    public Texture BackBuffer { get; init; }

    public Texture GeneratedFrame { get; init; }

    public Matrix4x4 View { get; init; }

    public Matrix4x4 Projection { get; init; }

    public Matrix4x4 InverseView { get; init; }

    public Matrix4x4 InverseProjection { get; init; }

    public Matrix4x4 ViewProjection { get; init; }

    public Matrix4x4 PreviousViewProjection { get; init; }

    public Matrix4x4 ClipToPrevClip { get; init; }

    public Vector3 CameraPosition { get; init; }

    public Vector3 CameraUp { get; init; }

    public Vector3 CameraRight { get; init; }

    public Vector3 CameraForward { get; init; }

    public float CameraNear { get; init; }

    public float CameraFar { get; init; }

    public float CameraFovAngleVer { get; init; }

    public float CameraAspectRatio { get; init; }

    public Vector2 Jitter { get; init; }

    public uint FrameIndex { get; init; }

    public int Slot { get; init; }

    public DLSSMode? RayReconstruction { get; init; }

    public bool FrameGeneration { get; init; }
}
