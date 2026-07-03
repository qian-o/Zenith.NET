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
                PColorAttachmentFormats = (Format*)ZenithMarshal.AllocateAndFill(scope, [.. desc.AttachmentFormats.ColorFormats.Select(static item => VKFormats.Vulkan(item).Format)])
            };

            if (desc.AttachmentFormats.DepthStencilFormat.HasValue)
            {
                PixelFormat depthStencilFormat = desc.AttachmentFormats.DepthStencilFormat.Value;

                rendering.DepthAttachmentFormat = ZenithHelper.HasDepth(depthStencilFormat) ? VKFormats.Vulkan(depthStencilFormat).Format : Format.Undefined;
                rendering.StencilAttachmentFormat = ZenithHelper.HasStencil(depthStencilFormat) ? VKFormats.Vulkan(depthStencilFormat).Format : Format.Undefined;
            }

            createInfo.PNext = &rendering;
        }

        // RenderState
        {
            ColorAttachmentBlendState[] states =
            [
                desc.RenderState.Blend.ColorAttachment0,
                desc.RenderState.Blend.ColorAttachment1,
                desc.RenderState.Blend.ColorAttachment2,
                desc.RenderState.Blend.ColorAttachment3,
                desc.RenderState.Blend.ColorAttachment4,
                desc.RenderState.Blend.ColorAttachment5,
                desc.RenderState.Blend.ColorAttachment6,
                desc.RenderState.Blend.ColorAttachment7
            ];

            uint attachmentCount = (uint)desc.AttachmentFormats.ColorFormats.Length;
            PipelineColorBlendAttachmentState* attachments = (PipelineColorBlendAttachmentState*)ZenithMarshal.Allocate<PipelineColorBlendAttachmentState>(scope, attachmentCount);
            for (uint i = 0; i < attachmentCount; i++)
            {
                ColorAttachmentBlendState state = desc.RenderState.Blend.IsIndependentBlendEnabled ? states[i] : states[0];

                attachments[i] = new()
                {
                    BlendEnable = state.IsBlendingEnabled,
                    SrcColorBlendFactor = VKFormats.Vulkan(state.SrcRgbFactor),
                    DstColorBlendFactor = VKFormats.Vulkan(state.DstRgbFactor),
                    ColorBlendOp = VKFormats.Vulkan(state.RgbOp),
                    SrcAlphaBlendFactor = VKFormats.Vulkan(state.SrcAlphaFactor),
                    DstAlphaBlendFactor = VKFormats.Vulkan(state.DstAlphaFactor),
                    AlphaBlendOp = VKFormats.Vulkan(state.AlphaOp),
                    ColorWriteMask = VKFormats.Vulkan(state.ColorWrites)
                };
            }

            PipelineRasterizationStateCreateInfo rasterizationState = new()
            {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                DepthClampEnable = !desc.RenderState.Rasterizer.IsDepthClipEnabled,
                PolygonMode = VKFormats.Vulkan(desc.RenderState.Rasterizer.FillMode),
                CullMode = VKFormats.Vulkan(desc.RenderState.Rasterizer.CullMode),
                FrontFace = VKFormats.Vulkan(desc.RenderState.Rasterizer.FrontFace),
                DepthBiasEnable = desc.RenderState.Rasterizer.DepthBias is not 0 || desc.RenderState.Rasterizer.DepthBiasSlopeScale is not 0.0f,
                DepthBiasConstantFactor = desc.RenderState.Rasterizer.DepthBias,
                DepthBiasClamp = desc.RenderState.Rasterizer.DepthBiasClamp,
                DepthBiasSlopeFactor = desc.RenderState.Rasterizer.DepthBiasSlopeScale,
                LineWidth = 1.0f
            };

            PipelineDepthStencilStateCreateInfo depthStencilState = new()
            {
                SType = StructureType.PipelineDepthStencilStateCreateInfo,
                DepthTestEnable = desc.RenderState.DepthStencil.IsDepthEnabled,
                DepthWriteEnable = desc.RenderState.DepthStencil.IsDepthWriteEnabled,
                DepthCompareOp = VKFormats.Vulkan(desc.RenderState.DepthStencil.DepthCompareOp),
                StencilTestEnable = desc.RenderState.DepthStencil.IsStencilEnabled,
                Front = new()
                {
                    FailOp = VKFormats.Vulkan(desc.RenderState.DepthStencil.FrontFace.FailOp),
                    PassOp = VKFormats.Vulkan(desc.RenderState.DepthStencil.FrontFace.PassOp),
                    DepthFailOp = VKFormats.Vulkan(desc.RenderState.DepthStencil.FrontFace.DepthFailOp),
                    CompareOp = VKFormats.Vulkan(desc.RenderState.DepthStencil.FrontFace.CompareOp),
                    CompareMask = desc.RenderState.DepthStencil.StencilReadMask,
                    WriteMask = desc.RenderState.DepthStencil.StencilWriteMask
                },
                Back = new()
                {
                    FailOp = VKFormats.Vulkan(desc.RenderState.DepthStencil.BackFace.FailOp),
                    PassOp = VKFormats.Vulkan(desc.RenderState.DepthStencil.BackFace.PassOp),
                    DepthFailOp = VKFormats.Vulkan(desc.RenderState.DepthStencil.BackFace.DepthFailOp),
                    CompareOp = VKFormats.Vulkan(desc.RenderState.DepthStencil.BackFace.CompareOp),
                    CompareMask = desc.RenderState.DepthStencil.StencilReadMask,
                    WriteMask = desc.RenderState.DepthStencil.StencilWriteMask
                },
                MaxDepthBounds = 1.0f
            };

            PipelineColorBlendStateCreateInfo colorBlendState = new()
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                AttachmentCount = attachmentCount,
                PAttachments = attachments
            };

            PipelineViewportStateCreateInfo viewportState = new()
            {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = attachmentCount,
                ScissorCount = attachmentCount
            };

            PipelineMultisampleStateCreateInfo multisampleState = new()
            {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                RasterizationSamples = VKFormats.Vulkan(desc.AttachmentFormats.SampleCount),
                AlphaToCoverageEnable = desc.RenderState.Blend.IsAlphaToCoverageEnabled
            };

            createInfo.PRasterizationState = &rasterizationState;
            createInfo.PDepthStencilState = &depthStencilState;
            createInfo.PColorBlendState = &colorBlendState;
            createInfo.PViewportState = &viewportState;
            createInfo.PMultisampleState = &multisampleState;
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
