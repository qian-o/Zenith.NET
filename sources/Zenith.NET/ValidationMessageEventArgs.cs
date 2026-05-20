namespace Zenith.NET;

public class ValidationMessageEventArgs(MessageSource source, MessageSeverity severity, string message) : EventArgs
{
    public MessageSource Source { get; } = source;

    public MessageSeverity Severity { get; } = severity;

    public string Message { get; } = message;

    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
}