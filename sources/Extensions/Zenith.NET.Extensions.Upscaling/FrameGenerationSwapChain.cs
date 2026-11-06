namespace Zenith.NET.Extensions.Upscaling;

public class FrameGenerationSwapChain : DisposableObject
{
    private readonly FrameGenerationSwapChainDesc desc;

    public FrameGenerationSwapChain(GraphicsContext context, FrameGenerationSwapChainDesc desc)
    {
        Context = context;
        this.desc = desc;

        throw new NotImplementedException("The frame generation swap chain is not implemented.");
    }

    public GraphicsContext Context { get; }

    public ref readonly FrameGenerationSwapChainDesc Desc => ref desc;

    public Texture Color => throw new NotImplementedException();

    public Texture Depth => throw new NotImplementedException();

    public Texture MotionVectors => throw new NotImplementedException();

    public Texture? Hudless => throw new NotImplementedException();

    public void Present(FrameGenerationSwapChainArgs args)
    {
        throw new NotImplementedException();
    }

    public void Resize(uint width, uint height)
    {
        throw new NotImplementedException();
    }

    public void Refresh(Surface surface)
    {
        throw new NotImplementedException();
    }

    protected override void Destroy()
    {
    }
}
