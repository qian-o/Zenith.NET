namespace Zenith.NET;

public struct BufferDesc
{
    public uint SizeInBytes;

    public uint StrideInBytes;

    public BufferAccess Access;

    public BufferUsages Usages;

    public static BufferDesc Vertex(uint sizeInBytes)
    {
        return new()
        {
            SizeInBytes = sizeInBytes,
            StrideInBytes = 0,
            Access = BufferAccess.GpuOnly,
            Usages = BufferUsages.CopyDst | BufferUsages.Vertex
        };
    }

    public static BufferDesc Index(uint sizeInBytes)
    {
        return new()
        {
            SizeInBytes = sizeInBytes,
            StrideInBytes = 0,
            Access = BufferAccess.GpuOnly,
            Usages = BufferUsages.CopyDst | BufferUsages.Index
        };
    }

    public static BufferDesc Constant(uint sizeInBytes)
    {
        return new()
        {
            SizeInBytes = sizeInBytes,
            StrideInBytes = 0,
            Access = BufferAccess.GpuOnly,
            Usages = BufferUsages.CopyDst | BufferUsages.Constant
        };
    }

    public static BufferDesc StorageReadOnly(uint sizeInBytes, uint strideInBytes)
    {
        return new()
        {
            SizeInBytes = sizeInBytes,
            StrideInBytes = strideInBytes,
            Access = BufferAccess.GpuOnly,
            Usages = BufferUsages.CopyDst | BufferUsages.StorageReadOnly
        };
    }

    public static BufferDesc StorageReadWrite(uint sizeInBytes, uint strideInBytes)
    {
        return new()
        {
            SizeInBytes = sizeInBytes,
            StrideInBytes = strideInBytes,
            Access = BufferAccess.GpuOnly,
            Usages = BufferUsages.CopyDst | BufferUsages.StorageReadWrite
        };
    }

    public static BufferDesc Indirect(uint sizeInBytes)
    {
        return new()
        {
            SizeInBytes = sizeInBytes,
            StrideInBytes = 0,
            Access = BufferAccess.GpuOnly,
            Usages = BufferUsages.CopyDst | BufferUsages.Indirect
        };
    }

    public static BufferDesc Staging(uint sizeInBytes)
    {
        return new()
        {
            SizeInBytes = sizeInBytes,
            StrideInBytes = 0,
            Access = BufferAccess.CpuWriteOnly,
            Usages = BufferUsages.CopySrc
        };
    }
}
