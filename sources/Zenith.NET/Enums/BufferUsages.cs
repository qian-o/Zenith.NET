namespace Zenith.NET;

[Flags]
public enum BufferUsages
{
    None = 0,

    Vertex = 1 << 0,

    Index = 1 << 1,

    Indirect = 1 << 2,

    Constant = 1 << 3,

    StorageReadOnly = 1 << 4,

    StorageReadWrite = 1 << 5,

    TransferSrc = 1 << 6,

    TransferDst = 1 << 7
}
