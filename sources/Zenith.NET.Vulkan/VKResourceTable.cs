using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKResourceTable : ResourceTable
{
    private readonly ZenithMarshal.Scope scope = new();

    public WriteDescriptorSet* Sets;

    public VKResourceTable(VKGraphicsContext context, ResourceTableDesc desc) : base(context, desc)
    {
        WriteDescriptorSet[] sets = new WriteDescriptorSet[desc.Bindings.Length];

        for (int i = 0; i < sets.Length; i++)
        {
            ResourceBinding binding = desc.Bindings[i];

            sets[i] = new()
            {
                SType = StructureType.WriteDescriptorSet,
                DstBinding = (uint)i,
                DescriptorCount = binding.Count,
                DescriptorType = VKFormats.Vulkan(binding.Type)
            };

            switch (binding.Type)
            {
                case ResourceType.ConstantBuffer:
                case ResourceType.StructuredBuffer:
                case ResourceType.StructuredBufferReadWrite:
                    sets[i].PBufferInfo = (DescriptorBufferInfo*)ZenithMarshal.Allocate<DescriptorBufferInfo>(scope, binding.Count);
                    break;

                case ResourceType.Texture:
                case ResourceType.TextureReadWrite:
                case ResourceType.Sampler:
                    sets[i].PImageInfo = (DescriptorImageInfo*)ZenithMarshal.Allocate<DescriptorImageInfo>(scope, binding.Count);
                    break;

                case ResourceType.AccelerationStructure:
                    sets[i].PNext = (WriteDescriptorSetAccelerationStructureKHR*)ZenithMarshal.AllocateAndFill(scope,
                    [
                        new WriteDescriptorSetAccelerationStructureKHR()
                        {
                            SType = StructureType.WriteDescriptorSetAccelerationStructureKhr,
                            AccelerationStructureCount = binding.Count,
                            PAccelerationStructures = (AccelerationStructureKHR*)ZenithMarshal.Allocate<AccelerationStructureKHR>(scope, binding.Count)
                        }
                    ]);
                    break;
            }
        }

        Sets = (WriteDescriptorSet*)ZenithMarshal.AllocateAndFill(scope, sets);

        SrvTextureViews = new VKTextureView?[desc.Bindings.Sum(static item => item.Count)];
        UavTextureViews = new VKTextureView?[desc.Bindings.Sum(static item => item.Count)];
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public VKTextureView?[] SrvTextureViews { get; }

    public VKTextureView?[] UavTextureViews { get; }

    protected override void WriteImpl(uint binding, IBindableResource[] resources)
    {
        ref WriteDescriptorSet set = ref Sets[binding];

        switch (Desc.Bindings[binding].Type)
        {
            case ResourceType.ConstantBuffer:
            case ResourceType.StructuredBuffer:
            case ResourceType.StructuredBufferReadWrite:
                for (int i = 0; i < resources.Length; i++)
                {
                    IBindableResource resource = resources[i];

                    if (resource is Buffer buffer)
                    {
                        set.PBufferInfo[i] = buffer.Vulkan().View.BufferInfo;
                    }
                    else if (resource is BufferView bufferView)
                    {
                        set.PBufferInfo[i] = bufferView.Vulkan().BufferInfo;
                    }
                }
                break;

            case ResourceType.Texture:
                {
                    uint index = (uint)Desc.Bindings.Take((int)binding).Sum(static item => item.Count);

                    for (int i = 0; i < resources.Length; i++)
                    {
                        IBindableResource resource = resources[i];

                        if (resource is Texture texture)
                        {
                            set.PImageInfo[i] = texture.Vulkan().View.SrvImageInfo;

                            SrvTextureViews[index + i] = texture.Vulkan().View;
                        }
                        else if (resource is TextureView textureView)
                        {
                            set.PImageInfo[i] = textureView.Vulkan().SrvImageInfo;

                            SrvTextureViews[index + i] = textureView.Vulkan();
                        }
                    }
                }
                break;

            case ResourceType.TextureReadWrite:
                {
                    uint index = (uint)Desc.Bindings.Take((int)binding).Sum(static item => item.Count);

                    for (int i = 0; i < resources.Length; i++)
                    {
                        IBindableResource resource = resources[i];

                        if (resource is Texture texture)
                        {
                            set.PImageInfo[i] = texture.Vulkan().View.UavImageInfo;

                            UavTextureViews[index + i] = texture.Vulkan().View;
                        }
                        else if (resource is TextureView textureView)
                        {
                            set.PImageInfo[i] = textureView.Vulkan().UavImageInfo;

                            UavTextureViews[index + i] = textureView.Vulkan();
                        }
                    }
                }
                break;

            case ResourceType.Sampler:
                for (int i = 0; i < resources.Length; i++)
                {
                    if (resources[i] is Sampler sampler)
                    {
                        set.PImageInfo[i] = new() { Sampler = sampler.Vulkan().Sampler };
                    }
                }
                break;

            case ResourceType.AccelerationStructure:
                for (int i = 0; i < resources.Length; i++)
                {
                    if (resources[i] is TopLevelAccelerationStructure topLevelAccelerationStructure)
                    {
                        ((WriteDescriptorSetAccelerationStructureKHR*)set.PNext)->PAccelerationStructures[i] = topLevelAccelerationStructure.Vulkan().AccelerationStructure;
                    }
                }
                break;
        }
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        scope.Dispose();
    }
}