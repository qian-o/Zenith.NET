namespace Zenith.NET;

public abstract class SwapChain(GraphicsContext context, SwapChainDesc desc) : GraphicsResource(context)
{
    public SwapChainDesc Desc { get; private set; } = desc;

    public abstract FrameBuffer FrameBuffer { get; }

    public abstract void Present();

    public abstract void Resize(uint width, uint height);

    public void Refresh(Surface surface)
    {
        Desc = (Desc with
        {
            Surface = surface
        });

        RefreshImpl();
    }

    protected abstract void RefreshImpl();
}
