namespace Zenith.NET.Vulkan;

internal readonly record struct VKDescriptorCounts(uint UniformBufferCount,
                                                   uint StorageBufferCount,
                                                   uint SampledImageCount,
                                                   uint StorageImageCount,
                                                   uint SamplerCount,
                                                   uint AccelerationStructureCount);
