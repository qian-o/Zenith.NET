namespace Zenith.NET;

public readonly record struct Submission(CommandQueue? Queue, ulong Value)
{
    public void Wait()
    {
        Queue?.Wait(Value);
    }
}
