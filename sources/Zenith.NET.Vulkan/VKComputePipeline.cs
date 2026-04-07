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

        // ResourceSlots
        {
            desc.ResourceSlots.Vulkan(out DescriptorSetLayoutBinding[] bindings, out _);

            DescriptorSetLayoutCreateInfo descriptorSetLayoutCreateInfo = new()
            {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = (uint)desc.ResourceSlots.Length,
                PBindings = (DescriptorSetLayoutBinding*)ZenithMarshal.AllocateAndFill(scope, bindings)
            };

            context.Vk.CreateDescriptorSetLayout(context.Device, &descriptorSetLayoutCreateInfo, null, out DescriptorSetLayout descriptorSetLayout).Success();

            PipelineLayoutCreateInfo pipelineLayoutCreateInfo = new()
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = 1,
                PSetLayouts = (DescriptorSetLayout*)ZenithMarshal.AllocateAndFill(scope, [descriptorSetLayout])
            };

            context.Vk.CreatePipelineLayout(context.Device, &pipelineLayoutCreateInfo, null, out PipelineLayout).Success();
            context.Vk.DestroyDescriptorSetLayout(context.Device, descriptorSetLayout, null);

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
