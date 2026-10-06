namespace Zenith.NET;

public abstract class SwapChain(GraphicsContext context, SwapChainDesc desc) : GraphicsResource(context)
{
    private SwapChainDesc desc = desc;

    public ref readonly SwapChainDesc Desc => ref desc;

    public CommandQueue Queue => desc.IsIndependentPresentEnabled ? Context.PresentQueue : Context.GraphicsQueue;

    public abstract Texture Drawable { get; }

    public void Present()
    {
        PresentImpl();

        Queue.Timeline.Signal().Wait();
    }

    public void Resize(uint width, uint height)
    {
        desc.Surface.Width = width;
        desc.Surface.Height = height;

        ResizeImpl();

        SetResourceName(Name);
    }

    public void Refresh(Surface surface)
    {
        desc.Surface = surface;

        RefreshImpl();

        SetResourceName(Name);
    }

    protected abstract void PresentImpl();

    protected abstract void ResizeImpl();

    protected abstract void RefreshImpl();
}
