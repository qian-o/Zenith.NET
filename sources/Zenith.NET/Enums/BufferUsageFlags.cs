namespace Zenith.NET;

[Flags]
public enum BufferUsageFlags
{
    None = 0,

    VertexBuffer = 1 << 0,

    IndexBuffer = 1 << 1,

    IndirectBuffer = 1 << 2,

    ConstantBuffer = 1 << 3,

    StructuredBuffer = 1 << 4,

    RWStructuredBuffer = 1 << 5,

    AccelerationStructure = 1 << 6,

    Dynamic = 1 << 7
}
