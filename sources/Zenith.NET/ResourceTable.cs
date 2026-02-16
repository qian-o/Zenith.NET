namespace Zenith.NET;

public abstract class ResourceTable(GraphicsContext context, ResourceTableDesc desc) : GraphicsResource(context)
{
    private ResourceTableDesc desc = desc;

    public ref readonly ResourceTableDesc Desc => ref desc;

    internal void Preprocess(CommandBuffer commandBuffer)
    {
        PreprocessImpl(commandBuffer);
    }

    protected abstract void PreprocessImpl(CommandBuffer commandBuffer);
}
