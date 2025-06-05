namespace Zenith.NET;

/// <summary>
/// Represents a graphics context, providing access to the device, backend, and feature capabilities.
/// </summary>
public abstract class GraphicsContext : DisposableObject
{
    /// <summary>
    /// Gets the name of the graphics device.
    /// </summary>
    public abstract string Device { get; }

    /// <summary>
    /// Gets the graphics backend in use.
    /// </summary>
    public abstract Backend Backend { get; }

    /// <summary>
    /// Gets the feature capabilities of the graphics device.
    /// </summary>
    public abstract Capabilities Capabilities { get; }

    /// <summary>
    /// Releases resources used by the graphics context.
    /// </summary>
    protected override void Destroy()
    {
    }
}
