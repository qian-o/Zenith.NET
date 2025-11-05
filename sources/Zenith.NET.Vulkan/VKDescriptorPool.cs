using System.Runtime.CompilerServices;
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
        uint sizeCount = Context.Capabilities.RayTracingSupported ? 8u : 7u;

        using ZenithMarshal.Scope scope = new();

        DescriptorPoolSize* sizes = (DescriptorPoolSize*)ZenithMarshal.Allocate<DescriptorPoolSize>(scope, sizeCount);

        sizes[0] = new()
        {
            Type = DescriptorType.UniformBuffer,
            DescriptorCount = DescriptorCount
        };

        sizes[1] = new()
        {
            Type = DescriptorType.UniformBufferDynamic,
            DescriptorCount = DescriptorCount
        };

        sizes[2] = new()
        {
            Type = DescriptorType.StorageBuffer,
            DescriptorCount = DescriptorCount
        };

        sizes[3] = new()
        {
            Type = DescriptorType.StorageBufferDynamic,
            DescriptorCount = DescriptorCount
        };

        sizes[4] = new()
        {
            Type = DescriptorType.SampledImage,
            DescriptorCount = DescriptorCount
        };

        sizes[5] = new()
        {
            Type = DescriptorType.StorageImage,
            DescriptorCount = DescriptorCount
        };

        sizes[6] = new()
        {
            Type = DescriptorType.Sampler,
            DescriptorCount = DescriptorCount
        };

        if (Context.Capabilities.RayTracingSupported)
        {
            sizes[7] = new()
            {
                Type = DescriptorType.AccelerationStructureKhr,
                DescriptorCount = DescriptorCount
            };
        }

        DescriptorPoolCreateInfo createInfo = new()
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            Flags = DescriptorPoolCreateFlags.FreeDescriptorSetBit,
            MaxSets = MaxSets,
            PoolSizeCount = sizeCount,
            PPoolSizes = sizes
        };

        context.Vk.CreateDescriptorPool(context.Device, &createInfo, null, (DescriptorPool*)Unsafe.AsPointer(ref Pool)).Success();
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public bool CanAlloc(VKDescriptorCounts counts)
    {
        if (remainingSets < 1 ||
            uniformBufferCount < counts.UniformBufferCount ||
            storageBufferCount < counts.StorageBufferCount ||
            sampledImageCount < counts.SampledImageCount ||
            storageImageCount < counts.StorageImageCount ||
            samplerCount < counts.SamplerCount ||
            accelerationStructureCount < counts.AccelerationStructureCount)
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
