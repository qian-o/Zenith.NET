using Metal.NET;

namespace Zenith.NET.Metal;

internal static class MTLFormats
{
    public static MTLResourceOptions Metal(MemoryResidency memoryResidency)
    {
        return memoryResidency switch
        {
            MemoryResidency.GpuOnly => MTLResourceOptions.StorageModePrivate,
            MemoryResidency.CpuReadOnly => MTLResourceOptions.StorageModeShared,
            MemoryResidency.CpuWriteOnly => MTLResourceOptions.StorageModeShared | MTLResourceOptions.CPUCacheModeWriteCombined,
            _ => default
        };
    }
}
