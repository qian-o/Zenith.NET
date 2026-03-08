namespace Zenith.NET.Metal;

internal class MTLResourceLayout : ResourceLayout
{
    public MTLResourceLayout(MTLGraphicsContext context, ResourceLayoutDesc desc) : base(context, desc)
    {
        for (int i = 0; i < desc.Bindings.Length; i++)
        {
            ResourceBinding binding = desc.Bindings[i];

            switch (binding.Type)
            {
                case ResourceType.ConstantBuffer:
                case ResourceType.StructuredBuffer:
                case ResourceType.StructuredBufferReadWrite:
                case ResourceType.AccelerationStructure:
                    BufferCount += binding.Count;
                    break;

                case ResourceType.Texture:
                case ResourceType.TextureReadWrite:
                    TextureCount += binding.Count;
                    break;

                case ResourceType.Sampler:
                    SamplerCount += binding.Count;
                    break;
            }
        }
    }

    public uint BufferCount { get; }

    public uint TextureCount { get; }

    public uint SamplerCount { get; }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
    }
}
