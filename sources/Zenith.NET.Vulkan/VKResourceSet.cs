using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKResourceSet : ResourceSet
{
    public VKDescriptorToken DescriptorToken;

    public VKResourceSet(VKGraphicsContext context, ResourceSetDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        DescriptorToken = context.DescriptorAllocator.Allocate(desc.Layout.Vulkan());

        WriteDescriptorSet* descriptorWrites = (WriteDescriptorSet*)ZenithMarshal.Allocate<WriteDescriptorSet>(scope, (uint)desc.Layout.Desc.Bindings.Length);

        uint resourceStartIndex = 0;
        List<VKTextureView> srvTextureViews = [];
        List<VKTextureView> uavTextureViews = [];

        for (int i = 0; i < desc.Layout.Desc.Bindings.Length; i++)
        {
            ResourceBinding binding = desc.Layout.Desc.Bindings[i];

            DescriptorImageInfo* imageInfos = (DescriptorImageInfo*)ZenithMarshal.Allocate<DescriptorImageInfo>(scope, binding.Count);
            DescriptorBufferInfo* bufferInfos = (DescriptorBufferInfo*)ZenithMarshal.Allocate<DescriptorBufferInfo>(scope, binding.Count);
            AccelerationStructureKHR* accelerationStructures = (AccelerationStructureKHR*)ZenithMarshal.Allocate<AccelerationStructureKHR>(scope, binding.Count);

            descriptorWrites[i] = new()
            {
                SType = StructureType.WriteDescriptorSet,
                PNext = null,
                DstSet = DescriptorToken.DescriptorSet,
                DstBinding = binding.Index,
                DstArrayElement = 0,
                DescriptorCount = binding.Count,
                DescriptorType = VKFormats.Vulkan(binding.Type),
                PImageInfo = imageInfos,
                PBufferInfo = bufferInfos
            };

            descriptorWrites[i].AddNext(out WriteDescriptorSetAccelerationStructureKHR accelerationStructureDescriptorWrite);
            accelerationStructureDescriptorWrite.AccelerationStructureCount = binding.Count;
            accelerationStructureDescriptorWrite.PAccelerationStructures = accelerationStructures;

            for (uint j = 0; j < binding.Count; j++)
            {
                IBindableResource resource = desc.Resources[(int)(resourceStartIndex + j)];

                switch (binding.Type)
                {
                    case ResourceType.ConstantBuffer:
                    case ResourceType.StructuredBuffer:
                    case ResourceType.StructuredBufferReadWrite:
                        if (resource is Buffer buffer)
                        {
                            bufferInfos[j] = buffer.Vulkan().View.BufferInfo;
                        }
                        else if (resource is BufferView bufferView)
                        {
                            bufferInfos[j] = bufferView.Vulkan().BufferInfo;
                        }
                        break;

                    case ResourceType.Texture:
                    case ResourceType.TextureReadWrite:
                        if (binding.Type is ResourceType.Texture)
                        {
                            if (resource is Texture texture)
                            {
                                imageInfos[j] = texture.Vulkan().View.SrvImageInfo;

                                srvTextureViews.Add(texture.Vulkan().View);
                            }
                            else if (resource is TextureView textureView)
                            {
                                imageInfos[j] = textureView.Vulkan().SrvImageInfo;

                                srvTextureViews.Add(textureView.Vulkan());
                            }
                        }
                        else if (resource is Texture texture)
                        {
                            imageInfos[j] = texture.Vulkan().View.UavImageInfo;

                            uavTextureViews.Add(texture.Vulkan().View);
                        }
                        else if (resource is TextureView textureView)
                        {
                            imageInfos[j] = textureView.Vulkan().UavImageInfo;

                            uavTextureViews.Add(textureView.Vulkan());
                        }
                        break;

                    case ResourceType.Sampler:
                        if (resource is Sampler sampler)
                        {
                            imageInfos[j] = new() { Sampler = sampler.Vulkan().Sampler };
                        }
                        break;

                    case ResourceType.AccelerationStructure:
                        if (resource is TopLevelAccelerationStructure topLevelAccelerationStructure)
                        {
                            accelerationStructures[j] = topLevelAccelerationStructure.Vulkan().AccelerationStructure;
                        }
                        break;
                }
            }

            resourceStartIndex += binding.Count;
        }

        context.Vk.UpdateDescriptorSets(context.Device, (uint)desc.Layout.Desc.Bindings.Length, descriptorWrites, 0, (CopyDescriptorSet*)null);

        SrvTextureViews = [.. srvTextureViews];
        UavTextureViews = [.. uavTextureViews];
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public VKTextureView[] SrvTextureViews { get; }

    public VKTextureView[] UavTextureViews { get; }

    public void TransitionLayout(VKCommandBuffer commandBuffer)
    {
        foreach (VKTextureView textureView in SrvTextureViews)
        {
            textureView.TransitionLayout(commandBuffer, ImageLayout.ShaderReadOnlyOptimal);
        }

        foreach (VKTextureView textureView in UavTextureViews)
        {
            textureView.TransitionLayout(commandBuffer, ImageLayout.General);
        }
    }

    protected override void SetResourceName(string name)
    {
        using ZenithMarshal.Scope scope = new();

        DebugUtilsObjectNameInfoEXT nameInfo = new()
        {
            SType = StructureType.DebugUtilsObjectNameInfoExt,
            ObjectType = ObjectType.DescriptorSet,
            ObjectHandle = DescriptorToken.DescriptorSet.Handle,
            PObjectName = (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8)
        };

        Context.DebugUtils?.SetDebugUtilsObjectName(Context.Device, &nameInfo).Success();
    }

    protected override void Destroy()
    {
        Context.DescriptorAllocator.Free(DescriptorToken);
    }
}