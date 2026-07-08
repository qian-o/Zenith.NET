namespace Zenith.NET;

public abstract class SwapChain(GraphicsContext context, SwapChainDesc desc) : GraphicsResource(context)
{
    private SwapChainDesc desc = desc;

    public ref readonly SwapChainDesc Desc => ref desc;

    public abstract Texture Drawable { get; }

    public abstract void Present();

    public void Resize(uint width, uint height)
    {
        Context.GraphicsQueue.Timeline.Signal().Wait();

        desc.Surface.Width = width;
        desc.Surface.Height = height;

        ResizeImpl();

        SetResourceName(Name);
    }

    public void Refresh(Surface surface)
    {
        Context.GraphicsQueue.Timeline.Signal().Wait();

        desc.Surface = surface;

        RefreshImpl();

        SetResourceName(Name);
    }

    protected abstract void ResizeImpl();

    protected abstract void RefreshImpl();

    protected override void Destroy()
    {
        Context.GraphicsQueue.Timeline.Signal().Wait();
    }
}
