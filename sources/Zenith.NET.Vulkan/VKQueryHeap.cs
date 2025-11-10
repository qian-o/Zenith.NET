using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKQueryHeap : QueryHeap
{
    public QueryPool QueryPool;

    public VKQueryHeap(VKGraphicsContext context, QueryHeapDesc desc) : base(context, desc)
    {
        QueryPoolCreateInfo createInfo = new()
        {
            SType = StructureType.QueryPoolCreateInfo,
            QueryType = VKFormats.Vulkan(desc.Type),
            QueryCount = desc.Count
        };

        context.Vk.CreateQueryPool(context.Device, &createInfo, null, (QueryPool*)Unsafe.AsPointer(ref QueryPool)).Success();

        context.Vk.ResetQueryPool(context.Device, QueryPool, 0, desc.Count);
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public override void GetResults(Span<ulong> results, uint startIndex)
    {
        Context.Vk.GetQueryPoolResults(Context.Device,
                                       QueryPool,
                                       startIndex,
                                       (uint)results.Length,
                                       (uint)(results.Length * 64),
                                       Unsafe.AsPointer(ref results[0]),
                                       64,
                                       QueryResultFlags.Result64Bit).Success();
    }

    protected override void SetResourceName(string name)
    {
        using ZenithMarshal.Scope scope = new();

        DebugUtilsObjectNameInfoEXT nameInfo = new()
        {
            SType = StructureType.DebugUtilsObjectNameInfoExt,
            ObjectType = ObjectType.QueryPool,
            ObjectHandle = QueryPool.Handle,
            PObjectName = (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8)
        };

        Context.DebugUtils?.SetDebugUtilsObjectName(Context.Device, &nameInfo).Success();
    }

    protected override void Destroy()
    {
        Context.Vk.DestroyQueryPool(Context.Device, QueryPool, null);
    }
}
