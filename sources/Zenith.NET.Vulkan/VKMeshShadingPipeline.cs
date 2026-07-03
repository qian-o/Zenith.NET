using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKMeshShadingPipeline : MeshShadingPipeline
{
    public VkPipeline Pipeline;

    public VKMeshShadingPipeline(VKGraphicsContext context, MeshShadingPipelineDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        GraphicsPipelineCreateInfo createInfo = new()
        {
            SType = StructureType.GraphicsPipelineCreateInfo,
            StageCount = 2,
            PStages = (PipelineShaderStageCreateInfo*)ZenithMarshal.AllocateAndFill(scope,
            [
                desc.MeshShader.Vulkan().GetPipelineShaderStageCreateInfo(scope, ShaderStageFlags.MeshBitExt),
                desc.FragmentShader.Vulkan().GetPipelineShaderStageCreateInfo(scope, ShaderStageFlags.FragmentBit)
            ])
        };

        if (desc.TaskShader is not null)
        {
            createInfo.StageCount = 3;
            createInfo.PStages = (PipelineShaderStageCreateInfo*)ZenithMarshal.AllocateAndFill(scope,
            [
                desc.TaskShader.Vulkan().GetPipelineShaderStageCreateInfo(scope, ShaderStageFlags.TaskBitExt),
                desc.MeshShader.Vulkan().GetPipelineShaderStageCreateInfo(scope, ShaderStageFlags.MeshBitExt),
                desc.FragmentShader.Vulkan().GetPipelineShaderStageCreateInfo(scope, ShaderStageFlags.FragmentBit)
            ]);
        }

        // PrimitiveTopology
        {
            PipelineInputAssemblyStateCreateInfo inputAssemblyState = new()
            {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = VKFormats.Vulkan(desc.PrimitiveTopology)
            };

            createInfo.PInputAssemblyState = &inputAssemblyState;
        }

        // AttachmentFormats
        {
            PipelineRenderingCreateInfo rendering = new()
            {
                SType = StructureType.PipelineRenderingCreateInfo,
                ColorAttachmentCount = (uint)desc.AttachmentFormats.ColorFormats.Length,
                PColorAttachmentFormats = (Format*)ZenithMarshal.AllocateAndFill(scope, [.. desc.AttachmentFormats.ColorFormats.Select(static item => VKFormats.Vulkan(item))]),
                DepthAttachmentFormat = VKFormats.Vulkan(desc.AttachmentFormats.DepthStencilFormat ?? PixelFormat.Unknown).Format,
                StencilAttachmentFormat = VKFormats.Vulkan(desc.AttachmentFormats.DepthStencilFormat ?? PixelFormat.Unknown).Format
            };

            createInfo.PNext = &rendering;
        }

        // RenderState
        {
        }

        PipelineDynamicStateCreateInfo dynamicState = new()
        {
            SType = StructureType.PipelineDynamicStateCreateInfo,
            DynamicStateCount = 4,
            PDynamicStates = (DynamicState*)ZenithMarshal.AllocateAndFill(scope, [DynamicState.Viewport, DynamicState.Scissor, DynamicState.BlendConstants, DynamicState.StencilReference])
        };

        createInfo.PDynamicState = &dynamicState;

        createInfo.AddNext(out PipelineCreateFlags2CreateInfo flags2CreateInfo);
        flags2CreateInfo.Flags = PipelineCreateFlags2.Vk2DescriptorHeapBitExt();

        context.Vk.CreateGraphicsPipelines(context.Device, default, 1, &createInfo, default, out Pipeline).Success();
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
