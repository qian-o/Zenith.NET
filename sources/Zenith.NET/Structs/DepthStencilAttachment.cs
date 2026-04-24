namespace Zenith.NET;

public record struct DepthStencilAttachment
{
    public Texture Texture;

    public TextureSubresource Subresource;

    public LoadOp DepthLoadOp;

    public StoreOp DepthStoreOp;

    public LoadOp StencilLoadOp;

    public StoreOp StencilStoreOp;

    public float ClearDepth;

    public byte ClearStencil;
}
