using System.Numerics;
using Buffer = Zenith.NET.Buffer;

namespace FluidTank.Models;

internal struct ParticleData
{
    public Buffer Particles;

    public Buffer PreviousPositions;

    public uint Count;

    public float Radius;

    public float Spacing;

    public Vector3 Minimum;

    public Vector3 Maximum;

    public ulong Version;
}
