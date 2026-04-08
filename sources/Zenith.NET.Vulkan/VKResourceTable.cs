using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKResourceTable : ResourceTable
{
    private readonly ZenithMarshal.Scope scope = new();

    public WriteDescriptorSet* Sets;

    public VKResourceTable(VKGraphicsContext context, ResourceTableDesc desc) : base(context, desc)
    {
        WriteDescriptorSet[] sets = new WriteDescriptorSet[desc.Slots.Length];

        for (int i = 0; i < sets.Length; i++)
        {
            ResourceSlot resourceSlot = desc.Slots[i];

            sets[i] = new()
            {
                SType = StructureType.WriteDescriptorSet,
                DstBinding = (uint)i,
                DescriptorCount = resourceSlot.Count,
                DescriptorType = VKFormats.Vulkan(resourceSlot.Type)
            };

            switch (resourceSlot.Type)
            {
                case ResourceType.ConstantBuffer:
                case ResourceType.StructuredBuffer:
                case ResourceType.StructuredBufferReadWrite:
                    sets[i].PBufferInfo = (DescriptorBufferInfo*)ZenithMarshal.Allocate<DescriptorBufferInfo>(scope, resourceSlot.Count);
                    break;

                case ResourceType.Texture:
                case ResourceType.TextureReadWrite:
                case ResourceType.Sampler:
                    sets[i].PImageInfo = (DescriptorImageInfo*)ZenithMarshal.Allocate<DescriptorImageInfo>(scope, resourceSlot.Count);
                    break;

                case ResourceType.AccelerationStructure:
                    sets[i].PNext = (WriteDescriptorSetAccelerationStructureKHR*)ZenithMarshal.AllocateAndFill(scope,
                    [
                        new WriteDescriptorSetAccelerationStructureKHR()
                        {
                            SType = StructureType.WriteDescriptorSetAccelerationStructureKhr,
                            AccelerationStructureCount = resourceSlot.Count,
                            PAccelerationStructures = (AccelerationStructureKHR*)ZenithMarshal.Allocate<AccelerationStructureKHR>(scope, resourceSlot.Count)
                        }
                    ]);
                    break;
            }
        }

        Sets = (WriteDescriptorSet*)ZenithMarshal.AllocateAndFill(scope, sets);

        SrvTextureViews = new VKTextureView?[desc.Slots.Sum(static item => item.Count)];
        UavTextureViews = new VKTextureView?[desc.Slots.Sum(static item => item.Count)];
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public VKTextureView?[] SrvTextureViews { get; }

    public VKTextureView?[] UavTextureViews { get; }

    protected override void SetImpl(uint slot, IBindableResource[] resources)
    {
        ResourceSlot resourceSlot = Desc.Slots[slot];

        ref WriteDescriptorSet descriptorSet = ref Sets[slot];

        switch (resourceSlot.Type)
        {
            case ResourceType.ConstantBuffer:
            case ResourceType.StructuredBuffer:
            case ResourceType.StructuredBufferReadWrite:
                for (int i = 0; i < resources.Length; i++)
                {
                    IBindableResource resource = resources[i];

                    if (resource is Buffer buffer)
                    {
                        descriptorSet.PBufferInfo[i] = buffer.Vulkan().View.BufferInfo;
                    }
                    else if (resource is BufferView bufferView)
                    {
                        descriptorSet.PBufferInfo[i] = bufferView.Vulkan().BufferInfo;
                    }
                }
                break;

            case ResourceType.Texture:
                {
                    uint index = (uint)Desc.Slots.Take((int)slot).Sum(static item => item.Count);

                    for (int i = 0; i < resources.Length; i++)
                    {
                        IBindableResource resource = resources[i];

                        if (resource is Texture texture)
                        {
                            descriptorSet.PImageInfo[i] = texture.Vulkan().View.SrvImageInfo;

                            SrvTextureViews[index + i] = texture.Vulkan().View;
                        }
                        else if (resource is TextureView textureView)
                        {
                            descriptorSet.PImageInfo[i] = textureView.Vulkan().SrvImageInfo;

                            SrvTextureViews[index + i] = textureView.Vulkan();
                        }
                    }
                }
                break;

            case ResourceType.TextureReadWrite:
                {
                    uint index = (uint)Desc.Slots.Take((int)slot).Sum(static item => item.Count);

                    for (int i = 0; i < resources.Length; i++)
                    {
                        IBindableResource resource = resources[i];

                        if (resource is Texture texture)
                        {
                            descriptorSet.PImageInfo[i] = texture.Vulkan().View.UavImageInfo;

                            UavTextureViews[index + i] = texture.Vulkan().View;
                        }
                        else if (resource is TextureView textureView)
                        {
                            descriptorSet.PImageInfo[i] = textureView.Vulkan().UavImageInfo;

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
                        descriptorSet.PImageInfo[i] = new() { Sampler = sampler.Vulkan().Sampler };
                    }
                }
                break;

            case ResourceType.AccelerationStructure:
                for (int i = 0; i < resources.Length; i++)
                {
                    if (resources[i] is TopLevelAccelerationStructure topLevelAccelerationStructure)
                    {
                        ((WriteDescriptorSetAccelerationStructureKHR*)descriptorSet.PNext)->PAccelerationStructures[i] = topLevelAccelerationStructure.Vulkan().AccelerationStructure;
                    }
                }
                break;
        }
    }

    protected override void PreprocessImpl(CommandBuffer commandBuffer)
    {
        VKCommandBuffer vkCommandBuffer = commandBuffer.Vulkan();

        foreach (VKTextureView? textureView in SrvTextureViews)
        {
            textureView?.TransitionLayout(vkCommandBuffer, ImageLayout.ShaderReadOnlyOptimal);
        }

        foreach (VKTextureView? textureView in UavTextureViews)
        {
            textureView?.TransitionLayout(vkCommandBuffer, ImageLayout.General);
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