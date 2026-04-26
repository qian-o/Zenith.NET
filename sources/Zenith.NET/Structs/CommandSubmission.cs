namespace Zenith.NET;

public readonly record struct CommandSubmission(CommandQueue? Queue, ulong Value)
{
    public void Wait()
    {
        Queue?.Wait(Value);
    }
}
