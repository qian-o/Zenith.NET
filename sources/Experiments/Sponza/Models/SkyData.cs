using System.Numerics;

namespace Sponza.Models;

internal struct SkyData
{
    public Vector3 SunDirectionWorld;

    public Vector3 SunRadiance;

    public Vector3 ZenithColor;

    public Vector3 HorizonColor;

    public Vector3 GroundColor;

    public float SkyIntensity;
}
