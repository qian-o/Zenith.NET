using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKDescriptorRegion(uint baseIndex, ulong stride)
{
    private readonly Lock @lock = new();
    private readonly Stack<uint> recycled = [];

    private uint head;

    public VKDescriptorToken Allocate(nint pointer, out HostAddressRangeEXT target)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (!recycled.TryPop(out uint index))
        {
            index = head++;
        }

        index += baseIndex;

        target = new((void*)(pointer + (nint)(stride * index)), (nuint)stride);

        return new(this, index);
    }

    public void Free(VKDescriptorToken token)
    {
        using Lock.Scope _ = @lock.EnterScope();

        recycled.Push(token.Index - baseIndex);
    }
}
