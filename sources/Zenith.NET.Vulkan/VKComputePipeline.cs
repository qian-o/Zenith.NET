using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

namespace Zenith.NET;

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
            DescriptorSetLayout* setLayouts = (DescriptorSetLayout*)ZenithMarshal.Allocate<DescriptorSetLayout>(scope, (uint)desc.ResourceLayouts.Length);
            for (int i = 0; i < desc.ResourceLayouts.Length; i++)
            {
                setLayouts[i] = desc.ResourceLayouts[i].Vulkan().DescriptorSetLayout;
            }

            PipelineLayoutCreateInfo pipelineLayoutCreateInfo = new()
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = (uint)desc.ResourceLayouts.Length,
                PSetLayouts = setLayouts
            };

            context.Vk.CreatePipelineLayout(context.Device, &pipelineLayoutCreateInfo, null, (PipelineLayout*)Unsafe.AsPointer(ref PipelineLayout)).Success();

            createInfo.Layout = PipelineLayout;
        }

        context.Vk.CreateComputePipelines(context.Device, default, 1, &createInfo, null, (VkPipeline*)Unsafe.AsPointer(ref Pipeline)).Success();
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
