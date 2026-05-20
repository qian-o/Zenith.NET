using System.Numerics;

namespace Zenith.NET;

public struct ColorAttachment
{
    public Texture Texture;

    public TextureSubresource Subresource;

    public Texture? ResolveTexture;

    public TextureSubresource ResolveSubresource;

    public LoadOp LoadOp;

    public StoreOp StoreOp;

    public Vector4 ClearColor;

    public static ColorAttachment Clear(Texture texture, Vector4 clearColor)
    {
        return new()
        {
            Texture = texture,
            Subresource = new(),
            ResolveTexture = null,
            ResolveSubresource = new(),
            LoadOp = LoadOp.Clear,
            StoreOp = StoreOp.Store,
            ClearColor = clearColor
        };
    }

    public static ColorAttachment Load(Texture texture)
    {
        return new()
        {
            Texture = texture,
            Subresource = new(),
            ResolveTexture = null,
            ResolveSubresource = new(),
            LoadOp = LoadOp.Load,
            StoreOp = StoreOp.Store,
            ClearColor = Vector4.Zero
        };
    }

    public static ColorAttachment DontCare(Texture texture)
    {
        return new()
        {
            Texture = texture,
            Subresource = new(),
            ResolveTexture = null,
            ResolveSubresource = new(),
            LoadOp = LoadOp.DontCare,
            StoreOp = StoreOp.Store,
            ClearColor = Vector4.Zero
        };
    }
}
