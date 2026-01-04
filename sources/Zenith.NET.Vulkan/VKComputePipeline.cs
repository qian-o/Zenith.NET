using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKComputePipeline : ComputePipeline
{
    public PipelineLayout PipelineLayout;

    public VkPipeline Pipeline;

    public VKComputePipeline(VKGraphicsContext context, ComputePipelineDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        ComputePipelineCreateInfo createInfo = new()
        {
            SType = StructureType.ComputePipelineCreateInfo,
            Stage = desc.Compute.Vulkan().GetPipelineShaderStageCreateInfo(scope)
        };

        // ResourceLayouts
        {
            PipelineLayoutCreateInfo pipelineLayoutCreateInfo = new()
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = (uint)desc.ResourceLayouts.Length,
                PSetLayouts = (DescriptorSetLayout*)ZenithMarshal.AllocateAndFill(scope, [.. desc.ResourceLayouts.Select(static item => item.Vulkan().DescriptorSetLayout)])
            };

            context.Vk.CreatePipelineLayout(context.Device, &pipelineLayoutCreateInfo, null, out PipelineLayout).Success();

            createInfo.Layout = PipelineLayout;
        }

        context.Vk.CreateComputePipelines(context.Device, default, 1, &createInfo, null, out Pipeline).Success();
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
