namespace Zenith.NET;

public abstract class GraphicsResource(GraphicsContext context) : DisposableObject, INativeObject
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

    public abstract nint GetNativeObject(NativeObjectType type);

    protected abstract void SetResourceName(string name);
}
