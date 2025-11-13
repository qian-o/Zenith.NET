using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKDescriptorPool : GraphicsResource
{
    private const uint MaxSets = 100;
    private const uint DescriptorCount = 1000;

    public DescriptorPool Pool;

    private uint remainingSets = MaxSets;
    private uint uniformBufferCount = DescriptorCount;
    private uint storageBufferCount = DescriptorCount;
    private uint sampledImageCount = DescriptorCount;
    private uint storageImageCount = DescriptorCount;
    private uint samplerCount = DescriptorCount;
    private uint accelerationStructureCount = DescriptorCount;

    public VKDescriptorPool(VKGraphicsContext context) : base(context)
    {
        DescriptorPoolSize[] poolSizes = new DescriptorPoolSize[context.Capabilities.RayTracingSupported ? 8 : 7];

        poolSizes[0] = new()
        {
            Type = DescriptorType.UniformBuffer,
            DescriptorCount = DescriptorCount
        };

        poolSizes[1] = new()
        {
            Type = DescriptorType.UniformBufferDynamic,
            DescriptorCount = DescriptorCount
        };

        poolSizes[2] = new()
        {
            Type = DescriptorType.StorageBuffer,
            DescriptorCount = DescriptorCount
        };

        poolSizes[3] = new()
        {
            Type = DescriptorType.StorageBufferDynamic,
            DescriptorCount = DescriptorCount
        };

        poolSizes[4] = new()
        {
            Type = DescriptorType.SampledImage,
            DescriptorCount = DescriptorCount
        };

        poolSizes[5] = new()
        {
            Type = DescriptorType.StorageImage,
            DescriptorCount = DescriptorCount
        };

        poolSizes[6] = new()
        {
            Type = DescriptorType.Sampler,
            DescriptorCount = DescriptorCount
        };

        if (context.Capabilities.RayTracingSupported)
        {
            poolSizes[7] = new()
            {
                Type = DescriptorType.AccelerationStructureKhr,
                DescriptorCount = DescriptorCount
            };
        }

        fixed (DescriptorPoolSize* pPoolSizes = poolSizes)
        {
            DescriptorPoolCreateInfo createInfo = new()
            {
                SType = StructureType.DescriptorPoolCreateInfo,
                Flags = DescriptorPoolCreateFlags.FreeDescriptorSetBit,
                MaxSets = MaxSets,
                PoolSizeCount = (uint)poolSizes.Length,
                PPoolSizes = pPoolSizes
            };

            context.Vk.CreateDescriptorPool(context.Device, &createInfo, null, out Pool).Success();
        }
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public bool CanAllocate(VKDescriptorCounts counts)
    {
        if (remainingSets < 1
            || uniformBufferCount < counts.UniformBufferCount
            || storageBufferCount < counts.StorageBufferCount
            || sampledImageCount < counts.SampledImageCount
            || storageImageCount < counts.StorageImageCount
            || samplerCount < counts.SamplerCount
            || accelerationStructureCount < counts.AccelerationStructureCount)
        {
            return false;
        }

        remainingSets--;
        uniformBufferCount -= counts.UniformBufferCount;
        storageBufferCount -= counts.StorageBufferCount;
        sampledImageCount -= counts.SampledImageCount;
        storageImageCount -= counts.StorageImageCount;
        samplerCount -= counts.SamplerCount;
        accelerationStructureCount -= counts.AccelerationStructureCount;

        return true;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        Context.Vk.DestroyDescriptorPool(Context.Device, Pool, null);
    }
}
