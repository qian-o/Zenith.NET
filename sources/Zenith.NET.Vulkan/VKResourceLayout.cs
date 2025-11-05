using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKResourceLayout : ResourceLayout
{
    public DescriptorSetLayout DescriptorSetLayout;

    public VKResourceLayout(VKGraphicsContext context, ResourceLayoutDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        DescriptorSetLayoutBinding* bindings = (DescriptorSetLayoutBinding*)ZenithMarshal.Allocate<DescriptorSetLayoutBinding>(scope, (uint)desc.Bindings.Length);

        uint uniformBufferCount = 0;
        uint storageBufferCount = 0;
        uint sampledImageCount = 0;
        uint storageImageCount = 0;
        uint samplerCount = 0;
        uint accelerationStructureCount = 0;

        for (int i = 0; i < desc.Bindings.Length; i++)
        {
            ResourceBinding binding = desc.Bindings[i];

            bindings[i] = new()
            {
                Binding = binding.Index,
                DescriptorType = VKFormats.Vulkan(binding.Type),
                DescriptorCount = binding.Count,
                StageFlags = VKFormats.Vulkan(binding.StageFlags)
            };

            switch (binding.Type)
            {
                case ResourceType.ConstantBuffer:
                    uniformBufferCount += binding.Count;
                    break;

                case ResourceType.StructuredBuffer:
                case ResourceType.StructuredBufferReadWrite:
                    storageBufferCount += binding.Count;
                    break;

                case ResourceType.Texture:
                    sampledImageCount += binding.Count;
                    break;

                case ResourceType.TextureReadWrite:
                    storageImageCount += binding.Count;
                    break;

                case ResourceType.Sampler:
                    samplerCount += binding.Count;
                    break;

                case ResourceType.AccelerationStructure:
                    accelerationStructureCount += binding.Count;
                    break;
            }
        }

        DescriptorSetLayoutCreateInfo createInfo = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = (uint)desc.Bindings.Length,
            PBindings = bindings
        };

        context.Vk.CreateDescriptorSetLayout(context.Device, &createInfo, null, (DescriptorSetLayout*)Unsafe.AsPointer(ref DescriptorSetLayout)).Success();

        Counts = new(uniformBufferCount, storageBufferCount, sampledImageCount, storageImageCount, samplerCount, accelerationStructureCount);
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public VKResourceCounts Counts { get; }

    protected override void SetResourceName(string name)
    {
        using ZenithMarshal.Scope scope = new();

        DebugUtilsObjectNameInfoEXT nameInfo = new()
        {
            SType = StructureType.DebugUtilsObjectNameInfoExt,
            ObjectType = ObjectType.DescriptorSetLayout,
            ObjectHandle = DescriptorSetLayout.Handle,
            PObjectName = (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8)
        };

        Context.DebugUtils?.SetDebugUtilsObjectName(Context.Device, &nameInfo).Success();
    }

    protected override void Destroy()
    {
        Context.Vk.DestroyDescriptorSetLayout(Context.Device, DescriptorSetLayout, null);
    }
}
