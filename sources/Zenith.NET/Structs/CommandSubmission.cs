namespace Zenith.NET;

public readonly struct CommandSubmission(CommandQueue queue, ulong value)
{
    public readonly CommandQueue Queue = queue;

    public readonly ulong Value = value;

    public void Wait()
    {
        Queue.Wait(Value);
    }
}
