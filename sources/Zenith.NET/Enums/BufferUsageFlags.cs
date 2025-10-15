namespace Zenith.NET;

[Flags]
public enum BufferUsageFlags
{
    None = 0,

    Vertex = 1 << 0,

    Index = 1 << 1,

    Indirect = 1 << 2,

    AccelerationStructure = 1 << 3,

    Constant = 1 << 4,

    ShaderResource = 1 << 5,

    UnorderedAccess = 1 << 6,

    Dynamic = 1 << 7
}
