namespace Zenith.NET;

[Flags]
public enum BufferUsages
{
    None = 0,

    CopySrc = 1 << 0,

    CopyDst = 1 << 1,

    Vertex = 1 << 2,

    Index = 1 << 3,

    Uniform = 1 << 4,

    StorageReadOnly = 1 << 5,

    StorageReadWrite = 1 << 6,

    Indirect = 1 << 7,

    AccelerationStructure = 1 << 8
}
