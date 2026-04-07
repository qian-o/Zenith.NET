using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKResourceTable(VKGraphicsContext context, ResourceTableDesc desc) : ResourceTable(context, desc)
{
    private readonly VKTextureView?[] srvTextureViews = new VKTextureView?[desc.Slots.Sum(static item => item.Count)];
    private readonly VKTextureView?[] uavTextureViews = new VKTextureView?[desc.Slots.Sum(static item => item.Count)];

    public VKDescriptorToken DescriptorToken = context.DescriptorAllocator.Allocate(desc.Slots);

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    protected override void SetImpl(uint slot, IBindableResource[] resources)
    {
        ResourceSlot resourceSlot = Desc.Slots[slot];

        DescriptorBufferInfo[] bufferInfos = new DescriptorBufferInfo[resources.Length];
        DescriptorImageInfo[] imageInfos = new DescriptorImageInfo[resources.Length];
        AccelerationStructureKHR[] accelerationStructures = new AccelerationStructureKHR[resources.Length];

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
                        bufferInfos[i] = buffer.Vulkan().View.BufferInfo;
                    }
                    else if (resource is BufferView bufferView)
                    {
                        bufferInfos[i] = bufferView.Vulkan().BufferInfo;
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
                            imageInfos[i] = texture.Vulkan().View.SrvImageInfo;

                            srvTextureViews[index + i] = texture.Vulkan().View;
                        }
                        else if (resource is TextureView textureView)
                        {
                            imageInfos[i] = textureView.Vulkan().SrvImageInfo;

                            srvTextureViews[index + i] = textureView.Vulkan();
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
                            imageInfos[i] = texture.Vulkan().View.UavImageInfo;

                            uavTextureViews[index + i] = texture.Vulkan().View;
                        }
                        else if (resource is TextureView textureView)
                        {
                            imageInfos[i] = textureView.Vulkan().UavImageInfo;

                            uavTextureViews[index + i] = textureView.Vulkan();
                        }
                    }
                }
                break;

            case ResourceType.Sampler:
                for (int i = 0; i < resources.Length; i++)
                {
                    if (resources[i] is Sampler sampler)
                    {
                        imageInfos[i] = new() { Sampler = sampler.Vulkan().Sampler };
                    }
                }
                break;

            case ResourceType.AccelerationStructure:
                for (int i = 0; i < resources.Length; i++)
                {
                    if (resources[i] is TopLevelAccelerationStructure topLevelAccelerationStructure)
                    {
                        accelerationStructures[i] = topLevelAccelerationStructure.Vulkan().AccelerationStructure;
                    }
                }
                break;
        }

        fixed (DescriptorBufferInfo* pBufferInfo = bufferInfos)
        {
            fixed (DescriptorImageInfo* pImageInfo = imageInfos)
            {
                fixed (AccelerationStructureKHR* pAccelerationStructures = accelerationStructures)
                {
                    WriteDescriptorSet descriptorWrite = new()
                    {
                        SType = StructureType.WriteDescriptorSet,
                        DstSet = DescriptorToken.Set,
                        DstBinding = slot,
                        DstArrayElement = 0,
                        DescriptorCount = (uint)resources.Length,
                        DescriptorType = VKFormats.Vulkan(resourceSlot.Type),
                        PBufferInfo = pBufferInfo,
                        PImageInfo = pImageInfo
                    };

                    if (resourceSlot.Type is ResourceType.AccelerationStructure)
                    {
                        WriteDescriptorSetAccelerationStructureKHR accelerationStructureWrite = new()
                        {
                            SType = StructureType.WriteDescriptorSetAccelerationStructureKhr,
                            AccelerationStructureCount = (uint)resources.Length,
                            PAccelerationStructures = pAccelerationStructures
                        };
                    }

                    Context.Vk.UpdateDescriptorSets(Context.Device, 1, &descriptorWrite, 0, (CopyDescriptorSet*)null);
                }
            }
        }
    }

    protected override void PreprocessImpl(CommandBuffer commandBuffer)
    {
        VKCommandBuffer vkCommandBuffer = commandBuffer.Vulkan();

        foreach (VKTextureView? textureView in srvTextureViews)
        {
            textureView?.TransitionLayout(vkCommandBuffer, ImageLayout.ShaderReadOnlyOptimal);
        }

        foreach (VKTextureView? textureView in uavTextureViews)
        {
            textureView?.TransitionLayout(vkCommandBuffer, ImageLayout.General);
        }
    }

    protected override void SetResourceName(string name)
    {
        using ZenithMarshal.Scope scope = new();

        DebugUtilsObjectNameInfoEXT nameInfo = new()
        {
            SType = StructureType.DebugUtilsObjectNameInfoExt,
            ObjectType = ObjectType.DescriptorSet,
            ObjectHandle = DescriptorToken.Set.Handle,
            PObjectName = (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8)
        };

        Context.DebugUtils?.SetDebugUtilsObjectName(Context.Device, &nameInfo).Success();
    }

    protected override void Destroy()
    {
        Context.DescriptorAllocator.Free(DescriptorToken);
    }
}