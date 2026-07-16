namespace Zenith.NET;

public readonly struct TimelineValue(Timeline timeline, ulong value)
{
    public readonly Timeline Timeline = timeline;

    public readonly ulong Value = value;

    public bool IsCompleted => Timeline.IsCompleted(Value);

    public void Wait()
    {
        Timeline.Wait(Value);
        Timeline.Queue.Recycle();
    }
}
