using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLResourceTable : ResourceTable
{
    private readonly BufferBinding[] bufferBindings;
    private readonly TextureBinding[] textureBindings;
    private readonly SamplerBinding[] samplerBindings;

    public MTL4ArgumentTable ArgumentTable;

    public MTLResourceTable(MTLGraphicsContext context, ResourceTableDesc desc) : base(context, desc)
    {
        uint resourceStartIndex = 0;
        List<BufferBinding> buffers = [];
        List<TextureBinding> textures = [];
        List<SamplerBinding> samplers = [];

        for (int i = 0; i < desc.Layout.Desc.Bindings.Length; i++)
        {
            ResourceBinding binding = desc.Layout.Desc.Bindings[i];

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
                            buffers.Add(new(buffer.Metal().GpuAddress, binding.Index + j));
                        }
                        else if (resource is BufferView bufferView)
                        {
                            buffers.Add(new(bufferView.Metal().GpuAddress, binding.Index + j));
                        }
                        break;

                    case ResourceType.Texture:
                    case ResourceType.TextureReadWrite:
                        if (resource is Texture texture)
                        {
                            textures.Add(new(texture.Metal().Texture.GpuResourceID, binding.Index + j));
                        }
                        else if (resource is TextureView textureView)
                        {
                            textures.Add(new(textureView.Metal().Texture.GpuResourceID, binding.Index + j));
                        }
                        break;

                    case ResourceType.Sampler:
                        if (resource is Sampler sampler)
                        {
                            samplers.Add(new(sampler.Metal().SamplerState.GpuResourceID, binding.Index + j));
                        }
                        break;

                    case ResourceType.AccelerationStructure:
                        // TODO: Acceleration structures
                        break;
                }
            }

            resourceStartIndex += binding.Count;
        }

        bufferBindings = [.. buffers];
        textureBindings = [.. textures];
        samplerBindings = [.. samplers];

        MTL4ArgumentTableDescriptor descriptor = new()
        {
            MaxBufferBindCount = (uint)bufferBindings.Length,
            MaxTextureBindCount = (uint)textureBindings.Length,
            MaxSamplerStateBindCount = (uint)samplerBindings.Length
        };

        ArgumentTable = context.Device.NewArgumentTable(descriptor, out NSError error);
        error.Success();

        Bind(ArgumentTable);
    }

    public void Bind(MTL4ArgumentTable argumentTable)
    {
        foreach (BufferBinding binding in bufferBindings)
        {
            argumentTable.SetAddress(binding.GpuAddress, binding.Index);
        }

        foreach (TextureBinding binding in textureBindings)
        {
            argumentTable.SetTexture(binding.ResourceID, binding.Index);
        }

        foreach (SamplerBinding binding in samplerBindings)
        {
            argumentTable.SetSamplerState(binding.ResourceID, binding.Index);
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

    private readonly record struct BufferBinding(nuint GpuAddress, uint Index);

    private readonly record struct TextureBinding(MTLResourceID ResourceID, uint Index);

    private readonly record struct SamplerBinding(MTLResourceID ResourceID, uint Index);
}
