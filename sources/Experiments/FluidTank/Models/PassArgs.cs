using System.Numerics;
using Zenith.NET;

namespace FluidTank.Models;

internal readonly struct PassArgs
{
    public Matrix4x4 View { get; init; }

    public Matrix4x4 Projection { get; init; }

    public Matrix4x4 InverseView { get; init; }

    public Matrix4x4 InverseProjection { get; init; }

    public Vector3 CameraPosition { get; init; }

    public Vector3 CameraRight { get; init; }

    public Vector3 CameraUp { get; init; }

    public Vector3 SunDirection { get; init; }

    public float LightIntensity { get; init; }

    public float InterpolationAlpha { get; init; }

    public ParticleData Particles { get; init; }

    public SceneResources Scene { get; init; }

    public Texture Color { get; init; }

    public Texture SceneDepth { get; init; }

    public Texture DepthStencil { get; init; }

    public Texture FluidDepth { get; init; }

    public Texture Thickness { get; init; }

    public Texture Normal { get; init; }

    public FluidViewMode ViewMode { get; init; }

    public bool FrontFaces { get; init; }
}
