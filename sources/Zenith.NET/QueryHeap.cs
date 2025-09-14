namespace Zenith.NET;

public abstract class QueryHeap(GraphicsContext context, QueryHeapDesc desc) : GraphicsResource(context)
{
    private QueryHeapDesc desc = desc;

    public ref readonly QueryHeapDesc Desc => ref desc;

    public abstract void GetData(Span<ulong> data, uint startIndex);
}
