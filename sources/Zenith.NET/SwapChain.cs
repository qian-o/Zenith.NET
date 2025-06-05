namespace Zenith.NET;

/// <summary>
/// Represents a swap chain, which manages the presentation of rendered images to a display surface.
/// </summary>
public abstract class SwapChain(GraphicsContext context, SwapChainDesc desc) : GraphicsResource(context)
{
    /// <summary>
    /// Gets the swap chain description.
    /// </summary>
    public SwapChainDesc Desc { get; private set; } = desc;

    /// <summary>
    /// Gets the current frame buffer used for rendering graphical content.
    /// </summary>
    public abstract FrameBuffer FrameBuffer { get; }

    /// <summary>
    /// Presents the contents of the swap chain to the display surface.
    /// </summary>
    public abstract void Present();

    /// <summary>
    /// Resizes the swap chain to the specified width and height.
    /// </summary>
    /// <param name="width">The new width of the swap chain, in pixels.</param>
    /// <param name="height">The new height of the swap chain, in pixels.</param>
    public abstract void Resize(uint width, uint height);

    /// <summary>
    /// Refreshes the swap chain with a new presentation surface.
    /// </summary>
    /// <param name="surface">The new surface description.</param>
    /// <exception cref="ArgumentException">Thrown if the surface description is invalid.</exception>
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

    /// <summary>
    /// When overridden in a derived class, performs the platform-specific refresh logic.
    /// </summary>
    protected abstract void RefreshImpl();
}
