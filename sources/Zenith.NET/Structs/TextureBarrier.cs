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
}
