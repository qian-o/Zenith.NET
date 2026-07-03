using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKGraphicsPipeline : GraphicsPipeline
{
    public VkPipeline Pipeline;

    public VKGraphicsPipeline(VKGraphicsContext context, GraphicsPipelineDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        GraphicsPipelineCreateInfo createInfo = new()
        {
            SType = StructureType.GraphicsPipelineCreateInfo,
            StageCount = 2,
            PStages = (PipelineShaderStageCreateInfo*)ZenithMarshal.AllocateAndFill(scope,
            [
                desc.VertexShader.Vulkan().GetPipelineShaderStageCreateInfo(scope, ShaderStageFlags.VertexBit),
                desc.FragmentShader.Vulkan().GetPipelineShaderStageCreateInfo(scope, ShaderStageFlags.FragmentBit)
            ])
        };

        // InputLayouts
        {
            uint vertexBindingDescriptionCount = (uint)desc.InputLayouts.Length;
            uint vertexAttributeDescriptionCount = (uint)desc.InputLayouts.Sum(static item => item.Elements.Length);

            VertexInputBindingDescription* vertexBindingDescriptions = (VertexInputBindingDescription*)ZenithMarshal.Allocate<VertexInputBindingDescription>(scope, vertexBindingDescriptionCount);
            VertexInputAttributeDescription* vertexAttributeDescriptions = (VertexInputAttributeDescription*)ZenithMarshal.Allocate<VertexInputAttributeDescription>(scope, vertexAttributeDescriptionCount);

            uint binding = 0;
            uint attribute = 0;
            foreach (InputLayout inputLayout in desc.InputLayouts)
            {
                vertexBindingDescriptions[binding] = new()
                {
                    Binding = binding,
                    Stride = inputLayout.StrideInBytes
                };

                foreach (InputElement element in inputLayout.Elements)
                {
                    vertexAttributeDescriptions[attribute] = new()
                    {
                        Location = attribute,
                        Binding = binding,
                        Format = VKFormats.Vulkan(element.Format),
                        Offset = element.OffsetInBytes
                    };

                    attribute++;
                }

                binding++;
            }

            PipelineVertexInputStateCreateInfo vertexInputState = new()
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = vertexBindingDescriptionCount,
                PVertexBindingDescriptions = vertexBindingDescriptions,
                VertexAttributeDescriptionCount = vertexAttributeDescriptionCount,
                PVertexAttributeDescriptions = vertexAttributeDescriptions
            };

            createInfo.PVertexInputState = &vertexInputState;
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
