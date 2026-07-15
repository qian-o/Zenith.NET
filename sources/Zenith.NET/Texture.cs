namespace Zenith.NET;

public abstract class Texture(GraphicsContext context, TextureDesc desc) : GraphicsResource(context)
{
    private TextureDesc desc = desc;

    public ref readonly TextureDesc Desc => ref desc;

    public abstract ResourceHandle SampledHandle { get; }

    public abstract ResourceHandle StorageHandle { get; }

    public void Upload(TextureSubresource subresource, TextureLayout before, TextureLayout after, Offset3D offset, Extent3D extent, TextureData data)
    {
        CommandBuffer commandBuffer = Context.TransferQueue.CommandBuffer();

        commandBuffer.Upload(this, subresource, before, after, offset, extent, data);
        commandBuffer.Submit().Wait();
    }

    public void Download(TextureSubresource subresource, TextureLayout before, TextureLayout after, Offset3D offset, Extent3D extent, TextureData data)
    {
        CommandBuffer commandBuffer = Context.TransferQueue.CommandBuffer();

        commandBuffer.Download(this, subresource, before, after, offset, extent, data);
        commandBuffer.Submit().Wait();
    }
}
