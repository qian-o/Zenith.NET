using System.Numerics;

namespace Zenith.NET;

public record struct ColorAttachment
{
    public Texture Texture;

    public TextureSubresource Subresource;

    public Texture? ResolveTexture;

    public TextureSubresource ResolveSubresource;

    public LoadOp LoadOp;

    public StoreOp StoreOp;

    public Vector4 ClearColor;
}
