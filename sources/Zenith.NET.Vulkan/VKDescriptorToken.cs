using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal record struct VKDescriptorToken
{
    public VKDescriptorPool DescriptorPool;

    public DescriptorSet DescriptorSet;
}
