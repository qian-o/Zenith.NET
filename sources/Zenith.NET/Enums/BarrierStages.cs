namespace Zenith.NET;

[Flags]
public enum BarrierStages
{
    None = 0,

    VertexShading = 1 << 0,

    FragmentShading = 1 << 1,

    ComputeShading = 1 << 2,

    Copy = 1 << 3,

    Resolve = 1 << 4,

    All = 1 << 5
}
