namespace Zenith.NET;

public record struct DepthStencilAttachment
{
    public Texture Texture;

    public TextureSubresource Subresource;

    public LoadOperation DepthLoad;

    public StoreOperation DepthStore;

    public LoadOperation StencilLoad;

    public StoreOperation StencilStore;

    public float ClearDepth;

    public byte ClearStencil;
}
