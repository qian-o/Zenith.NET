namespace Zenith.NET;

public struct TextureBarrier
{
    public Texture Texture;

    public TextureSubresourceRange Range;

    public PipelineStages SrcStages;

    public PipelineStages DstStages;

    public ResourceAccess SrcAccess;

    public ResourceAccess DstAccess;

    public TextureLayout SrcLayout;

    public TextureLayout DstLayout;

    public static TextureBarrier ColorAttachment(Texture texture, TextureBarrier? previous)
    {
        return new()
        {
            Texture = texture,
            Range = TextureSubresourceRange.All(texture),
            SrcStages = previous?.DstStages ?? PipelineStages.None,
            DstStages = PipelineStages.ColorAttachmentOutput,
            SrcAccess = previous?.DstAccess ?? ResourceAccess.None,
            DstAccess = ResourceAccess.ColorAttachmentWrite,
            SrcLayout = previous?.DstLayout ?? TextureLayout.Undefined,
            DstLayout = TextureLayout.ColorAttachment
        };
    }

    public static TextureBarrier DepthStencilAttachment(Texture texture, TextureBarrier? previous)
    {
        return new()
        {
            Texture = texture,
            Range = TextureSubresourceRange.All(texture),
            SrcStages = previous?.DstStages ?? PipelineStages.None,
            DstStages = PipelineStages.EarlyFragmentTests | PipelineStages.LateFragmentTests,
            SrcAccess = previous?.DstAccess ?? ResourceAccess.None,
            DstAccess = ResourceAccess.DepthStencilAttachmentWrite,
            SrcLayout = previous?.DstLayout ?? TextureLayout.Undefined,
            DstLayout = TextureLayout.DepthStencilAttachment
        };
    }

    public static TextureBarrier DepthStencilRead(Texture texture, TextureBarrier? previous)
    {
        return new()
        {
            Texture = texture,
            Range = TextureSubresourceRange.All(texture),
            SrcStages = previous?.DstStages ?? PipelineStages.None,
            DstStages = PipelineStages.EarlyFragmentTests | PipelineStages.FragmentShader | PipelineStages.LateFragmentTests,
            SrcAccess = previous?.DstAccess ?? ResourceAccess.None,
            DstAccess = ResourceAccess.DepthStencilAttachmentRead,
            SrcLayout = previous?.DstLayout ?? TextureLayout.Undefined,
            DstLayout = TextureLayout.DepthStencilReadOnly
        };
    }

    public static TextureBarrier ShaderRead(Texture texture, TextureBarrier? previous)
    {
        return new()
        {
            Texture = texture,
            Range = TextureSubresourceRange.All(texture),
            SrcStages = previous?.DstStages ?? PipelineStages.None,
            DstStages = PipelineStages.VertexShader | PipelineStages.FragmentShader | PipelineStages.ComputeShader,
            SrcAccess = previous?.DstAccess ?? ResourceAccess.None,
            DstAccess = ResourceAccess.ShaderRead,
            SrcLayout = previous?.DstLayout ?? TextureLayout.Undefined,
            DstLayout = TextureLayout.Sampled
        };
    }

    public static TextureBarrier Storage(Texture texture, TextureBarrier? previous)
    {
        return new()
        {
            Texture = texture,
            Range = TextureSubresourceRange.All(texture),
            SrcStages = previous?.DstStages ?? PipelineStages.None,
            DstStages = PipelineStages.VertexShader | PipelineStages.FragmentShader | PipelineStages.ComputeShader,
            SrcAccess = previous?.DstAccess ?? ResourceAccess.None,
            DstAccess = ResourceAccess.ShaderRead | ResourceAccess.ShaderWrite,
            SrcLayout = previous?.DstLayout ?? TextureLayout.Undefined,
            DstLayout = TextureLayout.Storage
        };
    }

    public static TextureBarrier CopySrc(Texture texture, TextureBarrier? previous)
    {
        return new()
        {
            Texture = texture,
            Range = TextureSubresourceRange.All(texture),
            SrcStages = previous?.DstStages ?? PipelineStages.None,
            DstStages = PipelineStages.Copy,
            SrcAccess = previous?.DstAccess ?? ResourceAccess.None,
            DstAccess = ResourceAccess.CopyRead,
            SrcLayout = previous?.DstLayout ?? TextureLayout.Undefined,
            DstLayout = TextureLayout.CopySrc
        };
    }

    public static TextureBarrier CopyDst(Texture texture, TextureBarrier? previous)
    {
        return new()
        {
            Texture = texture,
            Range = TextureSubresourceRange.All(texture),
            SrcStages = previous?.DstStages ?? PipelineStages.None,
            DstStages = PipelineStages.Copy,
            SrcAccess = previous?.DstAccess ?? ResourceAccess.None,
            DstAccess = ResourceAccess.CopyWrite,
            SrcLayout = previous?.DstLayout ?? TextureLayout.Undefined,
            DstLayout = TextureLayout.CopyDst
        };
    }

    public static TextureBarrier ResolveSrc(Texture texture, TextureBarrier? previous)
    {
        return new()
        {
            Texture = texture,
            Range = TextureSubresourceRange.All(texture),
            SrcStages = previous?.DstStages ?? PipelineStages.None,
            DstStages = PipelineStages.Resolve,
            SrcAccess = previous?.DstAccess ?? ResourceAccess.None,
            DstAccess = ResourceAccess.ResolveRead,
            SrcLayout = previous?.DstLayout ?? TextureLayout.Undefined,
            DstLayout = TextureLayout.ResolveSrc
        };
    }

    public static TextureBarrier ResolveDst(Texture texture, TextureBarrier? previous)
    {
        return new()
        {
            Texture = texture,
            Range = TextureSubresourceRange.All(texture),
            SrcStages = previous?.DstStages ?? PipelineStages.None,
            DstStages = PipelineStages.Resolve,
            SrcAccess = previous?.DstAccess ?? ResourceAccess.None,
            DstAccess = ResourceAccess.ResolveWrite,
            SrcLayout = previous?.DstLayout ?? TextureLayout.Undefined,
            DstLayout = TextureLayout.ResolveDst
        };
    }

    public static TextureBarrier Present(Texture texture, TextureBarrier? previous)
    {
        return new()
        {
            Texture = texture,
            Range = TextureSubresourceRange.All(texture),
            SrcStages = previous?.DstStages ?? PipelineStages.None,
            DstStages = PipelineStages.None,
            SrcAccess = previous?.DstAccess ?? ResourceAccess.None,
            DstAccess = ResourceAccess.Present,
            SrcLayout = previous?.DstLayout ?? TextureLayout.Undefined,
            DstLayout = TextureLayout.Present
        };
    }
}
