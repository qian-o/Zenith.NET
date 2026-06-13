using Metal.NET;

namespace Zenith.NET.Metal;

internal static class MTLFormats
{
    public static MTLResourceOptions Metal(MemoryResidency memoryResidency)
    {
        return memoryResidency switch
        {
            MemoryResidency.GpuOnly => MTLResourceOptions.CPUCacheModeDefaultCache | MTLResourceOptions.StorageModePrivate | MTLResourceOptions.HazardTrackingModeUntracked,
            MemoryResidency.CpuReadOnly => MTLResourceOptions.CPUCacheModeDefaultCache | MTLResourceOptions.StorageModeShared | MTLResourceOptions.HazardTrackingModeUntracked,
            MemoryResidency.CpuWriteOnly => MTLResourceOptions.CPUCacheModeWriteCombined | MTLResourceOptions.StorageModeShared | MTLResourceOptions.HazardTrackingModeUntracked,
            _ => default
        };
    }

    internal static MTLTextureType Metal(TextureType type)
    {
        throw new NotImplementedException();
    }

    internal static MTLPixelFormat Metal(PixelFormat format)
    {
        throw new NotImplementedException();
    }

    internal static nuint Metal(SampleCount sampleCount)
    {
        throw new NotImplementedException();
    }

    internal static MTLTextureUsage Metal(TextureUsages usages)
    {
        throw new NotImplementedException();
    }
}
