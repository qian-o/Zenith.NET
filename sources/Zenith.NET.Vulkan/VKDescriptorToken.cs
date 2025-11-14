using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal readonly record struct VKDescriptorToken(VKDescriptorPool DescriptorPool, DescriptorSet DescriptorSet);
