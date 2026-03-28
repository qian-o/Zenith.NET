using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLResourceTable : ResourceTable
{
    private readonly Binding[] bufferBindings;
    private readonly Binding[] textureBindings;
    private readonly Binding[] samplerBindings;
    private readonly Binding[] accelerationStructureBindings;

    public MTL4ArgumentTable ArgumentTable;

    public MTLResourceTable(MTLGraphicsContext context, ResourceTableDesc desc) : base(context, desc)
    {
        MTLResourceLayout layout = desc.Layout.Metal();

        MTL4ArgumentTableDescriptor descriptor = new()
        {
            MaxBufferBindCount = layout.BufferCount,
            MaxTextureBindCount = layout.TextureCount,
            MaxSamplerStateBindCount = layout.SamplerCount
        };

        ArgumentTable = context.Device.MakeArgumentTable(descriptor, out NSError error);
        error.Success();

        List<Binding> bufferBindingList = [];
        List<Binding> textureBindingList = [];
        List<Binding> samplerBindingList = [];
        List<Binding> accelerationStructureBindingList = [];

        uint resourceStartIndex = 0;

        for (int i = 0; i < layout.Desc.Bindings.Length; i++)
        {
            ResourceBinding binding = layout.Desc.Bindings[i];

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
                            bufferBindingList.Add(new(buffer.Metal().GpuAddress, default, binding.Index + j));
                        }
                        else if (resource is BufferView bufferView)
                        {
                            bufferBindingList.Add(new(bufferView.Metal().GpuAddress, default, binding.Index + j));
                        }
                        break;

                    case ResourceType.Texture:
                    case ResourceType.TextureReadWrite:
                        if (resource is Texture texture)
                        {
                            textureBindingList.Add(new(default, texture.Metal().Texture.GpuResourceID, binding.Index + j));
                        }
                        else if (resource is TextureView textureView)
                        {
                            textureBindingList.Add(new(default, textureView.Metal().Texture.GpuResourceID, binding.Index + j));
                        }
                        break;

                    case ResourceType.Sampler:
                        if (resource is Sampler sampler)
                        {
                            samplerBindingList.Add(new(default, sampler.Metal().SamplerState.GpuResourceID, binding.Index + j));
                        }
                        break;

                    case ResourceType.AccelerationStructure:
                        if (resource is TopLevelAccelerationStructure topLevelAccelerationStructure)
                        {
                            accelerationStructureBindingList.Add(new(default, topLevelAccelerationStructure.Metal().AccelerationStructure.GpuResourceID, binding.Index + j));
                        }
                        break;
                }
            }

            resourceStartIndex += binding.Count;
        }

        bufferBindings = [.. bufferBindingList];
        textureBindings = [.. textureBindingList];
        samplerBindings = [.. samplerBindingList];
        accelerationStructureBindings = [.. accelerationStructureBindingList];

        Bind(ArgumentTable);
    }

    public void Bind(MTL4ArgumentTable argumentTable)
    {
        foreach (Binding binding in bufferBindings)
        {
            binding.Buffer(argumentTable);
        }

        foreach (Binding binding in textureBindings)
        {
            binding.Texture(argumentTable);
        }

        foreach (Binding binding in samplerBindings)
        {
            binding.Sampler(argumentTable);
        }

        foreach (Binding binding in accelerationStructureBindings)
        {
            binding.AccelerationStructure(argumentTable);
        }
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
            argumentTable.SetAddress(gpuAddress, index);
        }

        public void Texture(MTL4ArgumentTable argumentTable)
        {
            argumentTable.SetTexture(resourceID, index);
        }

        public void Sampler(MTL4ArgumentTable argumentTable)
        {
            argumentTable.SetSamplerState(resourceID, index);
        }

        public void AccelerationStructure(MTL4ArgumentTable argumentTable)
        {
            argumentTable.SetResource(resourceID, index);
        }
    }
}
