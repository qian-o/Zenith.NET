using System.Numerics;

namespace SponzaScene.Models;

internal struct CSMData
{
    public Matrix4x4 View;

    public Matrix4x4 Projection;

    public float NearPlane;

    public float FarPlane;
}
