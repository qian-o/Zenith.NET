using Metal.NET;

namespace Zenith.NET.Metal;

internal unsafe class MTLQueryHeap : QueryHeap
{
    public MTL4CounterHeap CounterHeap;

    public MTLQueryHeap(MTLGraphicsContext context, QueryHeapDesc desc) : base(context, desc)
    {
        using MTL4CounterHeapDescriptor descriptor = new()
        {
            Type = MTL4CounterHeapType.Timestamp,
            Count = desc.Count
        };

        CounterHeap = context.Device.MakeCounterHeap(descriptor, out NSError error);
        error.Success();

        Buffer = new(context, new()
        {
            SizeInBytes = sizeof(ulong) * desc.Count,
            Residency = MemoryResidency.CpuReadOnly
        });
    }

    public MTLBuffer Buffer { get; }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void GetResultsImpl(Span<ulong> results, uint startIndex)
    {
        nint pointer = Buffer.Map();

        new Span<ulong>((void*)(pointer + (sizeof(ulong) * startIndex)), results.Length).CopyTo(results);

        Buffer.Unmap();
    }

    protected override void SetResourceName(string name)
    {
        using NSString @string = new(name);

        CounterHeap.Label = @string;
    }

    protected override void Destroy()
    {
        Buffer.Dispose();
        CounterHeap.Dispose();
    }
}
