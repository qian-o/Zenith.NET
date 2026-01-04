using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal record struct VKDescriptorToken
{
    public VKDescriptorPool Pool;

    public DescriptorSet Set;
}
