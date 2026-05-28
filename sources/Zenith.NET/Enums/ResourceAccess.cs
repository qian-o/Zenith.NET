namespace Zenith.NET;

[Flags]
public enum ResourceAccess
{
    None = 0,

    Vertex = 1 << 0,

    Index = 1 << 1,

    Indirect = 1 << 2,

    Constant = 1 << 3,

    ShaderRead = 1 << 4,

    ShaderWrite = 1 << 5,

    AccelerationStructureRead = 1 << 6,

    AccelerationStructureWrite = 1 << 7,

    ColorAttachmentRead = 1 << 8,

    ColorAttachmentWrite = 1 << 9,

    DepthStencilAttachmentRead = 1 << 10,

    DepthStencilAttachmentWrite = 1 << 11,

    CopyRead = 1 << 12,

    CopyWrite = 1 << 13,

    ResolveRead = 1 << 14,

    ResolveWrite = 1 << 15,

    Present = 1 << 16
}
