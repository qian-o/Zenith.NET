namespace Zenith.NET;

[Flags]
public enum BarrierStages
{
    None = 0,

    VertexShading = 1 << 0,

    MeshShading = 1 << 1,

    FragmentShading = 1 << 2,

    ComputeShading = 1 << 3,

    ColorAttachment = 1 << 4,

    DepthStencilAttachment = 1 << 5,

    Copy = 1 << 6,

    Resolve = 1 << 7,

    All = 1 << 8
}
