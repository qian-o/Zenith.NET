namespace Zenith.NET;

[Flags]
public enum BarrierStages
{
    None = 0,

    Vertex = 1 << 0,

    Fragment = 1 << 1,

    Compute = 1 << 2,

    Copy = 1 << 3,

    All = 1 << 4
}
