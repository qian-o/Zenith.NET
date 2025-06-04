namespace Zenith.NET;

public abstract class SwapChain(GraphicsContext context, SwapChainDesc desc) : GraphicsResource(context)
{
    public SwapChainDesc Desc { get; private set; } = desc;

    /// <summary>
    /// Gets the current frame buffer used for rendering graphical content.
    /// </summary>
    public abstract FrameBuffer FrameBuffer { get; }

    /// <summary>
    /// Present the swap chain.
    /// </summary>
    public abstract void Present();

    /// <summary>
    /// Resize the swap chain.
    /// </summary>
    public abstract void Resize(uint width, uint height);

    /// <summary>
    /// Refresh the swap chain surface.
    /// </summary>
    /// <param name="surface">New surface.</param>
    public void Refresh(SurfaceDesc surface)
    {
        if (!surface.Validate())
        {
            throw new ArgumentException("Invalid surface description.", nameof(surface));
        }

        Desc = (Desc with
        {
            Surface = surface
        });

        RefreshImpl();
    }

    protected abstract void RefreshImpl();
}
