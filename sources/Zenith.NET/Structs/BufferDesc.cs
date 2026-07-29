namespace Zenith.NET;

public struct BufferDesc
{
    public uint SizeInBytes;

    public uint StrideInBytes;

    public BufferUsages Usages;

    public MemoryResidency Residency;

    public static BufferDesc Vertex(uint sizeInBytes)
    {
        return new()
        {
            SizeInBytes = sizeInBytes,
            StrideInBytes = 0,
            Usages = BufferUsages.Vertex | BufferUsages.TransferDst,
            Residency = MemoryResidency.GpuOnly
        };
    }

    public static BufferDesc Index(uint sizeInBytes)
    {
        return new()
        {
            SizeInBytes = sizeInBytes,
            StrideInBytes = 0,
            Usages = BufferUsages.Index | BufferUsages.TransferDst,
            Residency = MemoryResidency.GpuOnly
        };
    }

    public static BufferDesc Indirect(uint sizeInBytes)
    {
        return new()
        {
            SizeInBytes = sizeInBytes,
            StrideInBytes = 0,
            Usages = BufferUsages.Indirect | BufferUsages.TransferDst,
            Residency = MemoryResidency.GpuOnly
        };
    }

    public static BufferDesc Constant(uint sizeInBytes)
    {
        return new()
        {
            SizeInBytes = sizeInBytes,
            StrideInBytes = 0,
            Usages = BufferUsages.Constant | BufferUsages.TransferDst,
            Residency = MemoryResidency.GpuOnly
        };
    }

    public static BufferDesc StorageReadOnly(uint sizeInBytes, uint strideInBytes)
    {
        return new()
        {
            SizeInBytes = sizeInBytes,
            StrideInBytes = strideInBytes,
            Usages = BufferUsages.StorageReadOnly | BufferUsages.TransferDst,
            Residency = MemoryResidency.GpuOnly
        };
    }

    public static BufferDesc StorageReadWrite(uint sizeInBytes, uint strideInBytes)
    {
        return new()
        {
            SizeInBytes = sizeInBytes,
            StrideInBytes = strideInBytes,
            Usages = BufferUsages.StorageReadWrite | BufferUsages.TransferDst,
            Residency = MemoryResidency.GpuOnly
        };
    }

    public static BufferDesc Staging(uint sizeInBytes)
    {
        return new()
        {
            SizeInBytes = sizeInBytes,
            StrideInBytes = 0,
            Usages = BufferUsages.TransferSrc,
            Residency = MemoryResidency.CpuWriteOnly
        };
    }
}
