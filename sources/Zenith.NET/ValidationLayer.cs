namespace Zenith.NET;

public abstract class ValidationLayer(GraphicsContext context) : GraphicsResource(context)
{
    public event EventHandler<ValidationMessageArgs>? ValidationMessage;

    protected void OnValidationMessage(ValidationMessageArgs args)
    {
        ValidationMessage?.Invoke(this, args);
    }
}
