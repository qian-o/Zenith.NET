using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKComputePipeline : ComputePipeline
{
    public VkPipeline Pipeline;

    public VKComputePipeline(VKGraphicsContext context, ComputePipelineDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        ComputePipelineCreateInfo createInfo = new()
        {
            SType = StructureType.ComputePipelineCreateInfo,
            Stage = desc.ComputeShader.Vulkan().GetPipelineShaderStageCreateInfo(scope, ShaderStageFlags.ComputeBit)
        };

        context.Vk.CreateComputePipelines(context.Device, default, 1, &createInfo, default, out Pipeline).Success();
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

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
        Context.Vk.DestroyPipeline(Context.Device, Pipeline, default);
    }
}
