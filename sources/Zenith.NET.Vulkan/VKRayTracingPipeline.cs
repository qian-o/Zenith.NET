using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKRayTracingPipeline : RayTracingPipeline
{
    public PipelineLayout PipelineLayout;

    public VkPipeline Pipeline;

    public VKRayTracingPipeline(VKGraphicsContext context, RayTracingPipelineDesc desc) : base(context, desc)
    {
        throw new NotImplementedException();
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    protected override void SetResourceName(string name)
    {
        using ZenithMarshal.Scope scope = new();

        DebugUtilsObjectNameInfoEXT nameInfo = new()
        {
            SType = StructureType.DebugUtilsObjectNameInfoExt,
            ObjectType = ObjectType.Pipeline,
            ObjectHandle = Pipeline.Handle,
            PObjectName = (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8)
        };

        Context.DebugUtils?.SetDebugUtilsObjectName(Context.Device, &nameInfo).Success();
    }

    protected override void Destroy()
    {
        Context.Vk.DestroyPipeline(Context.Device, Pipeline, null);
        Context.Vk.DestroyPipelineLayout(Context.Device, PipelineLayout, null);
    }
}
