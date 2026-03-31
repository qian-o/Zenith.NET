using Metal.NET;

namespace Zenith.NET.Metal;

internal unsafe class MTLQueryHeap : QueryHeap
{
    public MTL4CounterHeap CounterHeap;

    public MTLQueryHeap(MTLGraphicsContext context, QueryHeapDesc desc) : base(context, desc)
    {
        MTL4CounterHeapDescriptor descriptor = new()
        {
            Type = MTL4CounterHeapType.Timestamp,
            Count = desc.Count
        };

        CounterHeap = context.Device.MakeCounterHeap(descriptor, out NSError error);
        error.Success();

        Buffer = new(context, new()
        {
            SizeInBytes = sizeof(ulong) * desc.Count,
            StrideInBytes = sizeof(ulong),
            Flags = BufferUsageFlags.MapRead
        });
    }

    public MTLBuffer Buffer { get; }

    protected override void GetResultsImpl(Span<ulong> results, uint startIndex)
    {
        MappedMemory mappedMemory = Buffer.Map();

        new Span<ulong>((void*)(mappedMemory.Pointer + (sizeof(ulong) * startIndex)), results.Length).CopyTo(results);

        Buffer.Unmap();
    }

    protected override void SetResourceName(string name)
    {
        CounterHeap.Label = name;
    }

    protected override void Destroy()
    {
        Buffer.Dispose();

        CounterHeap.Dispose();
    }
}
