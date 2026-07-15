namespace Zenith.NET;

public abstract class Texture(GraphicsContext context, TextureDesc desc) : GraphicsResource(context)
{
    private TextureDesc desc = desc;

    public ref readonly TextureDesc Desc => ref desc;

    public abstract ResourceHandle SampledHandle { get; }

    public abstract ResourceHandle StorageHandle { get; }

    public void Upload(TextureSubresource subresource, TextureLayout currentLayout, TextureLayout finalLayout, Offset3D offset, Extent3D extent, TextureData data)
    {
        CommandBuffer commandBuffer = Context.GraphicsQueue.CommandBuffer();

        commandBuffer.Transition(this, subresource, currentLayout, TextureLayout.CopyDst);
        commandBuffer.Upload(this, subresource, offset, extent, data);
        commandBuffer.Transition(this, subresource, TextureLayout.CopyDst, finalLayout);
        commandBuffer.Submit().Wait();
    }

    public void Download(TextureSubresource subresource, TextureLayout currentLayout, TextureLayout finalLayout, Offset3D offset, Extent3D extent, TextureData data)
    {
        CommandBuffer commandBuffer = Context.GraphicsQueue.CommandBuffer();

        commandBuffer.Transition(this, subresource, currentLayout, TextureLayout.CopySrc);
        commandBuffer.Download(this, subresource, offset, extent, data);
        commandBuffer.Transition(this, subresource, TextureLayout.CopySrc, finalLayout);
        commandBuffer.Submit().Wait();
    }
}
