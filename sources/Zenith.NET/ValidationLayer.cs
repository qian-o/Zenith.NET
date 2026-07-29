namespace Zenith.NET;

public abstract class ValidationLayer(GraphicsContext context) : GraphicsResource(context)
{
    protected void Report(MessageSeverity severity, string message)
    {
        Context.OnValidationMessage(new(severity, message));
    }
}
