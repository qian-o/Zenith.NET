namespace SponzaScene;

internal static class Dispatcher
{
    private static readonly Lock @lock = new();
    private static readonly Queue<Action> actions = new();

    public static void Invoke(Action action)
    {
        using Lock.Scope _ = @lock.EnterScope();

        actions.Enqueue(action);
    }

    public static void Process()
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (actions.Count > 0)
        {
            actions.Dequeue()();
        }
    }
}
