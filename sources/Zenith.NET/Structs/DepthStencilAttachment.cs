namespace Zenith.NET;

public struct DepthStencilAttachment
{
    public Texture Texture;

    public TextureSubresource Subresource;

    public LoadOp DepthLoadOp;

    public StoreOp DepthStoreOp;

    public float ClearDepth;

    public LoadOp StencilLoadOp;

    public StoreOp StencilStoreOp;

    public byte ClearStencil;

    public static DepthStencilAttachment Clear(Texture texture, float clearDepth, byte clearStencil)
    {
        return new()
        {
            Texture = texture,
            Subresource = new(),
            DepthLoadOp = LoadOp.Clear,
            DepthStoreOp = StoreOp.Store,
            ClearDepth = clearDepth,
            StencilLoadOp = LoadOp.Clear,
            StencilStoreOp = StoreOp.Store,
            ClearStencil = clearStencil
        };
    }

    public static DepthStencilAttachment Load(Texture texture)
    {
        return new()
        {
            Texture = texture,
            Subresource = new(),
            DepthLoadOp = LoadOp.Load,
            DepthStoreOp = StoreOp.Store,
            ClearDepth = 1.0f,
            StencilLoadOp = LoadOp.Load,
            StencilStoreOp = StoreOp.Store,
            ClearStencil = 0
        };
    }

    public static DepthStencilAttachment DontCare(Texture texture)
    {
        return new()
        {
            Texture = texture,
            Subresource = new(),
            DepthLoadOp = LoadOp.DontCare,
            DepthStoreOp = StoreOp.Store,
            ClearDepth = 1.0f,
            StencilLoadOp = LoadOp.DontCare,
            StencilStoreOp = StoreOp.Store,
            ClearStencil = 0
        };
    }
}
