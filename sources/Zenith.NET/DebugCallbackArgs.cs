namespace Zenith.NET;

public class DebugCallbackArgs(MessageSeverity severity, string message) : EventArgs
{
    public MessageSeverity Severity { get; } = severity;

    public string Message { get; } = message;
}