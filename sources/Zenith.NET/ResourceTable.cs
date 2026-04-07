namespace Zenith.NET;

public abstract class ResourceTable(GraphicsContext context, ResourceTableDesc desc) : GraphicsResource(context)
{
    private ResourceTableDesc desc = desc;

    public ref readonly ResourceTableDesc Desc => ref desc;

    public void Write(uint slot, params IBindableResource[] resources)
    {
        if (slot >= desc.Slots.Length || resources.Length is 0 || resources.Length > desc.Slots[slot].Count)
        {
            return;
        }

        foreach (IBindableResource resource in resources)
        {
            if (resource?.IsDisposed is not false)
            {
                return;
            }
        }

        switch (desc.Slots[slot].Type)
        {
            case ResourceType.ConstantBuffer:
            case ResourceType.StructuredBuffer:
            case ResourceType.StructuredBufferReadWrite:
                foreach (IBindableResource resource in resources)
                {
                    if (resource is not Buffer and not BufferView)
                    {
                        return;
                    }
                }
                break;

            case ResourceType.Texture:
            case ResourceType.TextureReadWrite:
                foreach (IBindableResource resource in resources)
                {
                    if (resource is not Texture and not TextureView)
                    {
                        return;
                    }
                }
                break;

            case ResourceType.Sampler:
                foreach (IBindableResource resource in resources)
                {
                    if (resource is not Sampler)
                    {
                        return;
                    }
                }
                break;

            case ResourceType.AccelerationStructure:
                foreach (IBindableResource resource in resources)
                {
                    if (resource is not TopLevelAccelerationStructure)
                    {
                        return;
                    }
                }
                break;
        }

        SetImpl(slot, resources);
    }

    internal void Preprocess(CommandBuffer commandBuffer)
    {
        PreprocessImpl(commandBuffer);
    }

    protected abstract void SetImpl(uint slot, IBindableResource[] resources);

    protected abstract void PreprocessImpl(CommandBuffer commandBuffer);
}
