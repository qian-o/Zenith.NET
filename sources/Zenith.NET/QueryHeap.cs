namespace Zenith.NET;

public abstract class QueryHeap(GraphicsContext context, QueryHeapDesc desc) : GraphicsResource(context)
{
    private QueryHeapDesc desc = desc;

    public ref readonly QueryHeapDesc Desc => ref desc;

    public void GetResults(Span<ulong> results, uint startIndex)
    {
        GetResultsImpl(results, startIndex);
    }

    protected abstract void GetResultsImpl(Span<ulong> results, uint startIndex);
}
