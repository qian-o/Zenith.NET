using System.Numerics;

namespace Sponza.Models;

internal struct FrameData
{
    public Matrix4x4 Projection;

    public Matrix4x4 UnjitteredProjection;

    public Matrix4x4 ViewProjection;

    public Matrix4x4 UnjitteredViewProjection;

    public Matrix4x4 InverseProjection;

    public Matrix4x4 InverseViewProjection;

    public Matrix4x4 PreviousViewProjection;

    public Matrix4x4 ClipToPrevClip;

    public Vector3 CameraPositionWorld;

    public Vector3 CameraForwardWorld;

    public Vector2 JitterInPixels;

    public uint RenderWidth;

    public uint RenderHeight;

    public uint DisplayWidth;

    public uint DisplayHeight;

    public uint FrameIndex;

    public float HorizontalHalfFovTan;

    public bool Reset;

    public bool SameCamera;
}
