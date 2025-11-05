namespace Zenith.NET;

internal readonly record struct VKResourceCounts(uint UniformBufferCount, uint StorageBufferCount, uint SampledImageCount, uint StorageImageCount, uint SamplerCount, uint AccelerationStructureCount);
