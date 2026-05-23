namespace Zenith.NET;

public abstract class SwapChain(GraphicsContext context, SwapChainDesc desc) : GraphicsResource(context)
{
    private SwapChainDesc desc = desc;

    public ref readonly SwapChainDesc Desc => ref desc;

    public abstract Texture CurrentColorAttachment { get; }

    public abstract Texture? CurrentDepthStencilAttachment { get; }

    public abstract CommandSubmission Acquire();

    public abstract CommandSubmission Present(params ReadOnlySpan<CommandSubmission> waits);

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

    protected abstract void ResizeImpl();

    protected abstract void RefreshImpl();
}
