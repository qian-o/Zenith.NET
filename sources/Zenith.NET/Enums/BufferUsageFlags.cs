namespace Zenith.NET;

[Flags]
public enum BufferUsageFlags
{
    None = 0,

    VertexBuffer = 1 << 0,

    IndexBuffer = 1 << 1,

    IndirectBuffer = 1 << 2,

    ConstantBuffer = 1 << 3,

    AccelerationStructure = 1 << 4,

    ShaderReadOnly = 1 << 5,

    ShaderReadWrite = 1 << 6,

    Dynamic = 1 << 7
}
