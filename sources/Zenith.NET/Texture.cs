namespace Zenith.NET;

public abstract class Texture(GraphicsContext context, TextureDesc desc) : GraphicsResource(context)
{
    private TextureDesc desc = desc;

    public ref readonly TextureDesc Desc => ref desc;

    public ResourceHandle SampledHandle { get; }

    public ResourceHandle StorageHandle { get; }

    public void Upload(TextureSubresource subresource, Offset3D offset, Extent3D extent, TextureData data)
    {
        CommandBuffer commandBuffer = Context.CopyQueue.AcquireCommandBuffer();

        commandBuffer.Upload(this, subresource, offset, extent, data);
        commandBuffer.Submit().Wait();
    }

    public void Download(TextureSubresource subresource, Offset3D offset, Extent3D extent, TextureData data)
    {
        CommandBuffer commandBuffer = Context.CopyQueue.AcquireCommandBuffer();

        commandBuffer.Download(this, subresource, offset, extent, data);
        commandBuffer.Submit().Wait();
    }
}
