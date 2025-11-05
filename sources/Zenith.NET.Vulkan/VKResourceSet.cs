using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKResourceSet : ResourceSet
{
    public VKDescriptorToken DescriptorToken;

    public VKResourceSet(VKGraphicsContext context, ResourceSetDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        DescriptorToken = context.DescriptorAllocator.Allocate(desc.Layout.Vulkan());

        WriteDescriptorSet* descriptorWrites = (WriteDescriptorSet*)ZenithMarshal.Allocate<WriteDescriptorSet>(scope, (uint)desc.Resources.Length);

        uint offset = 0;

        for (int i = 0; i < desc.Layout.Desc.Bindings.Length; i++)
        {
            ResourceBinding binding = desc.Layout.Desc.Bindings[i];

            IBindableResource[] resources = desc.Resources[(int)offset..(int)(offset + binding.Count)];

            descriptorWrites[i] = new()
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = DescriptorToken.DescriptorSet,
                DstBinding = binding.Index,
                DescriptorCount = binding.Count,
                DescriptorType = VKFormats.Vulkan(binding.Type)
            };

            switch (binding.Type)
            {
                case ResourceType.ConstantBuffer:
                case ResourceType.StructuredBuffer:
                case ResourceType.StructuredBufferReadWrite:
                    {
                        DescriptorBufferInfo* infos = (DescriptorBufferInfo*)ZenithMarshal.Allocate<DescriptorBufferInfo>(scope, (uint)resources.Length);

                        for (int j = 0; j < resources.Length; j++)
                        {
                            if (resources[j] is Buffer buffer)
                            {
                                infos[j] = buffer.Vulkan().View.BufferInfo;
                            }
                            else if (resources[j] is BufferView bufferView)
                            {
                                infos[j] = bufferView.Vulkan().BufferInfo;
                            }
                        }
                    }
                    break;

                case ResourceType.Texture:
                case ResourceType.TextureReadWrite:
                    {
                        DescriptorImageInfo* infos = (DescriptorImageInfo*)ZenithMarshal.Allocate<DescriptorImageInfo>(scope, (uint)resources.Length);

                        for (int j = 0; j < resources.Length; j++)
                        {
                            if (binding.Type is ResourceType.Texture)
                            {
                                if (resources[j] is Texture texture)
                                {
                                    infos[j] = texture.Vulkan().View.SrvImageInfo;
                                }
                                else if (resources[j] is TextureView textureView)
                                {
                                    infos[j] = textureView.Vulkan().SrvImageInfo;
                                }
                            }
                            else
                            {
                                if (resources[j] is Texture texture)
                                {
                                    infos[j] = texture.Vulkan().View.UavImageInfo;
                                }
                                else if (resources[j] is TextureView textureView)
                                {
                                    infos[j] = textureView.Vulkan().UavImageInfo;
                                }
                            }
                        }
                    }
                    break;

                case ResourceType.Sampler:
                    {
                        DescriptorImageInfo* infos = (DescriptorImageInfo*)ZenithMarshal.Allocate<DescriptorImageInfo>(scope, (uint)resources.Length);

                        for (int j = 0; j < resources.Length; j++)
                        {
                            if (resources[j] is Sampler sampler)
                            {
                                infos[j] = new() { Sampler = sampler.Vulkan().Sampler };
                            }
                        }
                    }
                    break;

                case ResourceType.AccelerationStructure:
                    {
                        // TODO: Implement
                    }
                    break;
            }

            offset += binding.Count;
        }

        context.Vk.UpdateDescriptorSets(context.Device,
                                        (uint)desc.Resources.Length,
                                        descriptorWrites,
                                        0,
                                        (CopyDescriptorSet*)null);
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

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
