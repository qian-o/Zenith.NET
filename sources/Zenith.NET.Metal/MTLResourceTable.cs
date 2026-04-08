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
        bufferBindings = new Binding?[desc.Bindings.Where(static item => item.Type is ResourceType.ConstantBuffer or ResourceType.StructuredBuffer or ResourceType.StructuredBufferReadWrite or ResourceType.AccelerationStructure).Sum(static item => item.Count)];
        textureBindings = new Binding?[desc.Bindings.Where(static item => item.Type is ResourceType.Texture or ResourceType.TextureReadWrite).Sum(static item => item.Count)];
        samplerBindings = new Binding?[desc.Bindings.Where(static item => item.Type is ResourceType.Sampler).Sum(static item => item.Count)];

        MTL4ArgumentTableDescriptor descriptor = new()
        {
            MaxBufferBindCount = (uint)bufferBindings.Length,
            MaxTextureBindCount = (uint)textureBindings.Length,
            MaxSamplerStateBindCount = (uint)samplerBindings.Length
        };

        ArgumentTable = context.Device.MakeArgumentTable(descriptor, out NSError error);
        error.Success();
    }

    public void Bind(MTL4ArgumentTable argumentTable, uint argumentCount)
    {
        foreach (Binding? binding in bufferBindings)
        {
            binding?.Buffer(argumentTable, argumentCount);
        }

        foreach (Binding? binding in textureBindings)
        {
            binding?.Texture(argumentTable, argumentCount);
        }

        foreach (Binding? binding in samplerBindings)
        {
            binding?.Sampler(argumentTable, argumentCount);
        }
    }

    protected override void SetImpl(uint binding, IBindableResource[] resources)
    {
        switch (Desc.Bindings[binding].Type)
        {
            case ResourceType.ConstantBuffer:
            case ResourceType.StructuredBuffer:
            case ResourceType.StructuredBufferReadWrite:
            case ResourceType.AccelerationStructure:
                {
                    uint index = (uint)Desc.Bindings.Take((int)binding).Where(static item => item.Type is ResourceType.ConstantBuffer or ResourceType.StructuredBuffer or ResourceType.StructuredBufferReadWrite or ResourceType.AccelerationStructure).Sum(static item => item.Count);

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
                }
                break;

            case ResourceType.Texture:
            case ResourceType.TextureReadWrite:
                {
                    uint index = (uint)Desc.Bindings.Take((int)binding).Where(static item => item.Type is ResourceType.Texture or ResourceType.TextureReadWrite).Sum(static item => item.Count);

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
                }
                break;

            case ResourceType.Sampler:
                {
                    uint index = (uint)Desc.Bindings.Take((int)binding).Where(static item => item.Type is ResourceType.Sampler).Sum(static item => item.Count);

                    foreach (IBindableResource resource in resources)
                    {
                        if (resource is Sampler sampler)
                        {
                            samplerBindings[index] = new(default, sampler.Metal().SamplerState.GpuResourceID, index);
                        }

                        index++;
                    }
                }
                break;
        }

        Bind(ArgumentTable, uint.MaxValue);
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
        public void Buffer(MTL4ArgumentTable argumentTable, uint argumentCount)
        {
            if (index >= argumentCount)
            {
                return;
            }

            if (gpuAddress != default)
            {
                argumentTable.SetAddress(gpuAddress, index);
            }
            else
            {
                argumentTable.SetResource(resourceID, index);
            }
        }

        public void Texture(MTL4ArgumentTable argumentTable, uint argumentCount)
        {
            if (index >= argumentCount)
            {
                return;
            }

            argumentTable.SetTexture(resourceID, index);
        }

        public void Sampler(MTL4ArgumentTable argumentTable, uint argumentCount)
        {
            if (index >= argumentCount)
            {
                return;
            }

            argumentTable.SetSamplerState(resourceID, index);
        }
    }
}
