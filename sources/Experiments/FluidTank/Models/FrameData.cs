using System.Numerics;

namespace FluidTank.Models;

internal struct FrameData
{
    public Matrix4x4 View;

    public Matrix4x4 Projection;

    public Matrix4x4 InvView;

    public Matrix4x4 InvProjection;

    public Vector3 Position;

    public Vector3 Right;

    public Vector3 Up;

    public Vector3 SunDirection;

    public float LightIntensity;

    public float Time;

    public float InterpolationAlpha;
}
