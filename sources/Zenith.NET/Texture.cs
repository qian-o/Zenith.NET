namespace Zenith.NET;

public abstract class Texture(GraphicsContext context, TextureDesc desc) : GraphicsResource(context)
{
    private readonly TextureLayout[] layouts = new TextureLayout[desc.MipLevels * desc.ArrayLayers];

    private TextureDesc desc = desc;

    public ref readonly TextureDesc Desc => ref desc;

    public abstract ResourceHandle SampledHandle { get; }

    public abstract ResourceHandle StorageHandle { get; }

    internal TextureLayout this[TextureSubresource subresource]
    {
        get => layouts[subresource.MipLevel + (subresource.ArrayLayer * Desc.MipLevels)];
        set => layouts[subresource.MipLevel + (subresource.ArrayLayer * Desc.MipLevels)] = value;
    }

    public void Upload(TextureSubresource subresource, Offset3D offset, Extent3D extent, TextureData data)
    {
        CommandBuffer commandBuffer = Context.TransferQueue.CommandBuffer();

        commandBuffer.Upload(this, subresource, offset, extent, data);
        commandBuffer.Submit().Wait();
    }

    public void Download(TextureSubresource subresource, Offset3D offset, Extent3D extent, TextureData data)
    {
        CommandBuffer commandBuffer = Context.TransferQueue.CommandBuffer();

        commandBuffer.Download(this, subresource, offset, extent, data);
        commandBuffer.Submit().Wait();
    }

    public void OverrideLayout(TextureSubresource subresource, TextureLayout layout)
    {
        this[subresource] = layout;
    }
}
