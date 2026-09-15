using System.Numerics;
using Buffer = Zenith.NET.Buffer;

namespace FluidTank.Models;

internal readonly struct ParticleData
{
    public Buffer Particles { get; init; }

    public Buffer PreviousPositions { get; init; }

    public uint Count { get; init; }

    public float Radius { get; init; }

    public float Spacing { get; init; }

    public Vector3 Minimum { get; init; }

    public Vector3 Maximum { get; init; }

    public ulong Version { get; init; }
}
