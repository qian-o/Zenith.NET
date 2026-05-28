namespace Zenith.NET;

[Flags]
public enum ResourceAccess
{
    None = 0,

    Vertex = 1 << 0,

    Index = 1 << 1,

    Constant = 1 << 2,

    Indirect = 1 << 3,

    ShaderRead = 1 << 4,

    ShaderWrite = 1 << 5,

    ColorAttachmentRead = 1 << 6,

    ColorAttachmentWrite = 1 << 7,

    DepthStencilAttachmentRead = 1 << 8,

    DepthStencilAttachmentWrite = 1 << 9,

    CopyRead = 1 << 10,

    CopyWrite = 1 << 11,

    ResolveRead = 1 << 12,

    ResolveWrite = 1 << 13,

    AccelerationStructureRead = 1 << 14,

    AccelerationStructureWrite = 1 << 15,

    Present = 1 << 16
}
