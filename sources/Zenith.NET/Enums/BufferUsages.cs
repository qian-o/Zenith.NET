namespace Zenith.NET;

[Flags]
public enum BufferUsages
{
    None = 0,

    CopySrc = 1 << 0,

    CopyDst = 1 << 1,

    Uniform = 1 << 2,

    Vertex = 1 << 3,

    Index = 1 << 4,

    StorageReadOnly = 1 << 5,

    StorageReadWrite = 1 << 6,

    Indirect = 1 << 7,

    AccelerationStructure = 1 << 8
}
