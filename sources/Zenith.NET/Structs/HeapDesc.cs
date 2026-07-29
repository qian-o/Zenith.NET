namespace Zenith.NET;

public struct HeapDesc
{
    public ulong SizeInBytes;

    public MemoryResidency Residency;

    public static HeapDesc GpuOnly(ulong sizeInBytes)
    {
        return new()
        {
            SizeInBytes = sizeInBytes,
            Residency = MemoryResidency.GpuOnly
        };
    }

    public static HeapDesc CpuReadOnly(ulong sizeInBytes)
    {
        return new()
        {
            SizeInBytes = sizeInBytes,
            Residency = MemoryResidency.CpuReadOnly
        };
    }

    public static HeapDesc CpuWriteOnly(ulong sizeInBytes)
    {
        return new()
        {
            SizeInBytes = sizeInBytes,
            Residency = MemoryResidency.CpuWriteOnly
        };
    }
}
