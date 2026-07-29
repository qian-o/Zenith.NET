namespace Zenith.NET;

public class ValidationMessageEventArgs(MessageSeverity severity, string message) : EventArgs
{
    public MessageSeverity Severity { get; } = severity;

    public string Message { get; } = message;

    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
}
