namespace Zenith.NET;

public abstract class GraphicsResource(GraphicsContext context) : DisposableObject
{
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

    protected GraphicsContext Context => context;

    protected abstract void NameChanged(string name);
}
