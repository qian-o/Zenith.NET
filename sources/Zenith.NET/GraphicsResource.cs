namespace Zenith.NET;

/// <summary>
/// Provides a base class for graphics resources that are associated with a graphics context and support naming.
/// </summary>
public abstract class GraphicsResource(GraphicsContext context) : DisposableObject
{
    /// <summary>
    /// Gets or sets the name of the resource.
    /// </summary>
    public string Name
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;

                NameChanged(value);
            }
        }
    } = string.Empty;

    /// <summary>
    /// The graphics context associated with this resource.
    /// </summary>
    protected GraphicsContext Context => context;

    /// <summary>
    /// Called when the resource name changes.
    /// </summary>
    /// <param name="name">The new name of the resource.</param>
    protected abstract void NameChanged(string name);
}
