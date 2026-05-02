namespace Zenith.NET;

public record struct DepthStencilAttachment
{
    public Texture Texture;

    public TextureSubresource Subresource;

    public LoadOperation DepthLoadOperation;

    public StoreOperation DepthStoreOperation;

    public LoadOperation StencilLoadOperation;

    public StoreOperation StencilStoreOperation;

    public float ClearDepth;

    public byte ClearStencil;
}
