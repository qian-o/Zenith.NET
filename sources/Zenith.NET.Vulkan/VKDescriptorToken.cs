namespace Zenith.NET.Vulkan;

internal readonly struct VKDescriptorToken(VKDescriptorRegion region, uint index) : IDisposable
{
    public readonly uint Index = index;

    public readonly ResourceHandle ResourceHandle = new(index, 0);

    public void Dispose()
    {
        region.Free(this);
    }
}
