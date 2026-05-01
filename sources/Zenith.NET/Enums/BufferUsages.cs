namespace Zenith.NET;

[Flags]
public enum BufferUsages
{
    None = 0,

    Vertex = 1 << 0,

    Index = 1 << 1,

    Uniform = 1 << 2,

    StorageReadOnly = 1 << 3,

    StorageReadWrite = 1 << 4,

    Indirect = 1 << 5,

    AccelerationStructure = 1 << 6
}
