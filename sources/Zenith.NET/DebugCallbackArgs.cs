namespace Zenith.NET;

public class DebugCallbackArgs(MessageCategory category, MessageSeverity severity, string message) : EventArgs
{
    public MessageCategory Category { get; } = category;

    public MessageSeverity Severity { get; } = severity;

    public string Message { get; } = message;
}