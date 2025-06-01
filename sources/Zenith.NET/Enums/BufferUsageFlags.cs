namespace Zenith.NET;

[Flags]
public enum BufferUsageFlags
{
    None = 0,

    /// <summary>
    /// Binds a buffer as a vertex buffer to the input-assembler stage.
    /// </summary>
    VertexBuffer = 1 << 0,

    /// <summary>
    /// Binds a buffer as an index buffer to the input-assembler stage.
    /// </summary>
    IndexBuffer = 1 << 1,

    /// <summary>
    /// Binds a buffer as a constant buffer to a shader stage.
    /// </summary>
    ConstantBuffer = 1 << 2,

    /// <summary>
    /// Binds a buffer as a shader resource to a shader stage.
    /// </summary>
    ShaderResource = 1 << 3,

    /// <summary>
    /// Binds a buffer as an unordered-access resource to a shader stage.
    /// </summary>
    UnorderedAccess = 1 << 4,

    /// <summary>
    /// Binds a buffer as an acceleration structure to a shader stage.
    /// </summary>
    AccelerationStructure = 1 << 5,

    /// <summary>
    /// Indirect buffer.
    /// </summary>
    IndirectBuffer = 1 << 6,

    /// <summary>
    /// Creates a buffer that can be updated by the CPU.
    /// </summary>
    Dynamic = 1 << 7
}
