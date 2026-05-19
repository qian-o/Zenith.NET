namespace Zenith.NET;

public record struct DepthStencilAttachment
{
    public Texture Texture;

    public TextureSubresource Subresource;

    public LoadOp DepthLoadOp;

    public StoreOp DepthStoreOp;

    public float ClearDepth;

    public LoadOp StencilLoadOp;

    public StoreOp StencilStoreOp;

    public byte ClearStencil;
}
