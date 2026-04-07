using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLResourceTable : ResourceTable
{
    private readonly Binding?[] bufferBindings;
    private readonly Binding?[] textureBindings;
    private readonly Binding?[] samplerBindings;

    public MTL4ArgumentTable ArgumentTable;

    public MTLResourceTable(MTLGraphicsContext context, ResourceTableDesc desc) : base(context, desc)
    {
        bufferBindings = new Binding?[desc.Slots.Where(static item => item.Type is ResourceType.ConstantBuffer or ResourceType.StructuredBuffer or ResourceType.StructuredBufferReadWrite or ResourceType.AccelerationStructure).Sum(static item => item.Count)];
        textureBindings = new Binding?[desc.Slots.Where(static item => item.Type is ResourceType.Texture or ResourceType.TextureReadWrite).Sum(static item => item.Count)];
        samplerBindings = new Binding?[desc.Slots.Where(static item => item.Type is ResourceType.Sampler).Sum(static item => item.Count)];

        MTL4ArgumentTableDescriptor descriptor = new()
        {
            MaxBufferBindCount = (uint)bufferBindings.Length,
            MaxTextureBindCount = (uint)textureBindings.Length,
            MaxSamplerStateBindCount = (uint)samplerBindings.Length
        };

        ArgumentTable = context.Device.MakeArgumentTable(descriptor, out NSError error);
        error.Success();
    }

    public void Bind(MTL4ArgumentTable argumentTable)
    {
        foreach (Binding? binding in bufferBindings)
        {
            binding?.Buffer(argumentTable);
        }

        foreach (Binding? binding in textureBindings)
        {
            binding?.Texture(argumentTable);
        }

        foreach (Binding? binding in samplerBindings)
        {
            binding?.Sampler(argumentTable);
        }
    }

    protected override void SetImpl(uint slot, IBindableResource[] resources)
    {
        ResourceSlot resourceSlot = Desc.Slots[slot];

        uint index = 0;
        switch (resourceSlot.Type)
        {
            case ResourceType.ConstantBuffer:
            case ResourceType.StructuredBuffer:
            case ResourceType.StructuredBufferReadWrite:
            case ResourceType.AccelerationStructure:
                index = (uint)Desc.Slots.Take((int)slot).Where(static item => item.Type is ResourceType.ConstantBuffer or ResourceType.StructuredBuffer or ResourceType.StructuredBufferReadWrite or ResourceType.AccelerationStructure).Sum(static item => item.Count);
                break;

            case ResourceType.Texture:
            case ResourceType.TextureReadWrite:
                index = (uint)Desc.Slots.Take((int)slot).Where(static item => item.Type is ResourceType.Texture or ResourceType.TextureReadWrite).Sum(static item => item.Count);
                break;

            case ResourceType.Sampler:
                index = (uint)Desc.Slots.Take((int)slot).Where(static item => item.Type is ResourceType.Sampler).Sum(static item => item.Count);
                break;
        }

        switch (resourceSlot.Type)
        {
            case ResourceType.ConstantBuffer:
            case ResourceType.StructuredBuffer:
            case ResourceType.StructuredBufferReadWrite:
            case ResourceType.AccelerationStructure:
                foreach (IBindableResource resource in resources)
                {
                    if (resource is Buffer buffer)
                    {
                        bufferBindings[index] = new(buffer.Metal().GpuAddress, default, index);
                    }
                    else if (resource is BufferView bufferView)
                    {
                        bufferBindings[index] = new(bufferView.Metal().GpuAddress, default, index);
                    }
                    else if (resource is TopLevelAccelerationStructure topLevelAccelerationStructure)
                    {
                        bufferBindings[index] = new(default, topLevelAccelerationStructure.Metal().AccelerationStructure.GpuResourceID, index);
                    }

                    index++;
                }
                break;

            case ResourceType.Texture:
            case ResourceType.TextureReadWrite:
                foreach (IBindableResource resource in resources)
                {
                    if (resource is Texture texture)
                    {
                        textureBindings[index] = new(default, texture.Metal().Texture.GpuResourceID, index);
                    }
                    else if (resource is TextureView textureView)
                    {
                        textureBindings[index] = new(default, textureView.Metal().Texture.GpuResourceID, index);
                    }

                    index++;
                }
                break;

            case ResourceType.Sampler:
                foreach (IBindableResource resource in resources)
                {
                    if (resource is Sampler sampler)
                    {
                        samplerBindings[index] = new(default, sampler.Metal().SamplerState.GpuResourceID, index);
                    }

                    index++;
                }
                break;
        }

        Bind(ArgumentTable);
    }

    protected override void PreprocessImpl(CommandBuffer commandBuffer)
    {
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        ArgumentTable.Dispose();
    }

    private readonly struct Binding(nuint gpuAddress, MTLResourceID resourceID, uint index)
    {
        public void Buffer(MTL4ArgumentTable argumentTable)
        {
            if (gpuAddress != default)
            {
                argumentTable.SetAddress(gpuAddress, index);
            }
            else
            {
                argumentTable.SetResource(resourceID, index);
            }
        }

        public void Texture(MTL4ArgumentTable argumentTable)
        {
            argumentTable.SetTexture(resourceID, index);
        }

        public void Sampler(MTL4ArgumentTable argumentTable)
        {
            argumentTable.SetSamplerState(resourceID, index);
        }
    }
}
