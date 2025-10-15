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

                if (!string.IsNullOrWhiteSpace(value))
                {
                    SetResourceName(value);
                }
            }
        }
    } = string.Empty;

    protected GraphicsContext Context => context;

    protected abstract void SetResourceName(string name);
}
