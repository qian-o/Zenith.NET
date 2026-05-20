namespace Zenith.NET;

public readonly struct CommandSubmission
{
    public CommandQueue? Queue { get; init; }

    public ulong Value { get; init; }

    public void Wait()
    {
        Queue?.Wait(Value);
    }
}
