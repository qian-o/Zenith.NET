namespace Zenith.NET;

public abstract class SwapChain(GraphicsContext context, SwapChainDesc desc) : GraphicsResource(context)
{
    private SwapChainDesc desc = desc;

    public ref readonly SwapChainDesc Desc => ref desc;

    public abstract FrameBuffer FrameBuffer { get; }

    public abstract void Present();

    public abstract void Resize(uint width, uint height);

    public void Refresh(Surface surface)
    {
        desc = (desc with
        {
            Surface = surface
        });

        RefreshImpl();
    }

    protected abstract void RefreshImpl();
}
