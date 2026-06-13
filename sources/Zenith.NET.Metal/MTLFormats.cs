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

    public static MTLTextureType Metal(TextureType textureType)
    {
        throw new NotImplementedException();
    }

    public static MTLPixelFormat Metal(PixelFormat pixelFormat)
    {
        throw new NotImplementedException();
    }

    public static nuint Metal(SampleCount sampleCount)
    {
        throw new NotImplementedException();
    }

    public static MTLTextureUsage Metal(TextureUsages textureUsages)
    {
        throw new NotImplementedException();
    }
}
