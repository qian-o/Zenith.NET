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
            Usages = BufferUsages.Vertex | BufferUsages.CopyDst,
            Residency = MemoryResidency.GpuOnly
        };
    }

    public static BufferDesc Index(uint sizeInBytes)
    {
        return new()
        {
            SizeInBytes = sizeInBytes,
            StrideInBytes = 0,
            Usages = BufferUsages.Index | BufferUsages.CopyDst,
            Residency = MemoryResidency.GpuOnly
        };
    }

    public static BufferDesc Indirect(uint sizeInBytes)
    {
        return new()
        {
            SizeInBytes = sizeInBytes,
            StrideInBytes = 0,
            Usages = BufferUsages.Indirect | BufferUsages.CopyDst,
            Residency = MemoryResidency.GpuOnly
        };
    }

    public static BufferDesc Constant(uint sizeInBytes)
    {
        return new()
        {
            SizeInBytes = sizeInBytes,
            StrideInBytes = 0,
            Usages = BufferUsages.Constant | BufferUsages.CopyDst,
            Residency = MemoryResidency.GpuOnly
        };
    }

    public static BufferDesc StorageReadOnly(uint sizeInBytes, uint strideInBytes)
    {
        return new()
        {
            SizeInBytes = sizeInBytes,
            StrideInBytes = strideInBytes,
            Usages = BufferUsages.StorageReadOnly | BufferUsages.CopyDst,
            Residency = MemoryResidency.GpuOnly
        };
    }

    public static BufferDesc StorageReadWrite(uint sizeInBytes, uint strideInBytes)
    {
        return new()
        {
            SizeInBytes = sizeInBytes,
            StrideInBytes = strideInBytes,
            Usages = BufferUsages.StorageReadWrite | BufferUsages.CopyDst,
            Residency = MemoryResidency.GpuOnly
        };
    }

    public static BufferDesc AccelerationStructure(uint sizeInBytes)
    {
        return new()
        {
            SizeInBytes = sizeInBytes,
            StrideInBytes = 0,
            Usages = BufferUsages.AccelerationStructure | BufferUsages.CopyDst,
            Residency = MemoryResidency.GpuOnly
        };
    }

    public static BufferDesc Staging(uint sizeInBytes)
    {
        return new()
        {
            SizeInBytes = sizeInBytes,
            StrideInBytes = 0,
            Usages = BufferUsages.CopySrc,
            Residency = MemoryResidency.CpuWriteOnly
        };
    }
}
