using Silk.NET.Vulkan;

namespace Zenith.NET;

internal readonly record struct VKDescriptorToken(VKDescriptorPool DescriptorPool, DescriptorSet DescriptorSet);
