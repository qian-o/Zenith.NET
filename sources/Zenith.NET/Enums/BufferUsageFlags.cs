namespace Zenith.NET;

[Flags]
public enum BufferUsageFlags
{
    None = 0,

    VertexBuffer = 1 << 0,

    IndexBuffer = 1 << 1,

    ConstantBuffer = 1 << 2,

    ShaderResource = 1 << 3,

    UnorderedAccess = 1 << 4,

    AccelerationStructure = 1 << 5,

    IndirectBuffer = 1 << 6,

    Dynamic = 1 << 7
}
