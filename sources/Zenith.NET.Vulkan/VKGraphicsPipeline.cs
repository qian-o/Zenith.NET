using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKGraphicsPipeline : GraphicsPipeline
{
    public PipelineLayout PipelineLayout;

    public VkPipeline Pipeline;

    public VKGraphicsPipeline(VKGraphicsContext context, GraphicsPipelineDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        GraphicsPipelineCreateInfo createInfo = new()
        {
            SType = StructureType.GraphicsPipelineCreateInfo
        };

        // RenderStates - Output
        {
            BlendStateRenderTarget[] blendStateRenderTargets =
            [
                desc.RenderStates.BlendState.RenderTarget0,
                desc.RenderStates.BlendState.RenderTarget1,
                desc.RenderStates.BlendState.RenderTarget2,
                desc.RenderStates.BlendState.RenderTarget3,
                desc.RenderStates.BlendState.RenderTarget4,
                desc.RenderStates.BlendState.RenderTarget5,
                desc.RenderStates.BlendState.RenderTarget6,
                desc.RenderStates.BlendState.RenderTarget7
            ];

            uint colorAttachmentCount = (uint)desc.Output.ColorAttachments.Length;
            bool hasDepthStencilAttachment = desc.Output.DepthStencilAttachment.HasValue;

            PipelineColorBlendAttachmentState* attachments = (PipelineColorBlendAttachmentState*)ZenithMarshal.Allocate<PipelineColorBlendAttachmentState>(scope, colorAttachmentCount);
            Format* colorAttachmentFormats = (Format*)ZenithMarshal.Allocate<Format>(scope, colorAttachmentCount);
            for (uint i = 0; i < colorAttachmentCount; i++)
            {
                BlendStateRenderTarget target = desc.RenderStates.BlendState.IndependentBlendEnable ? blendStateRenderTargets[i] : blendStateRenderTargets[0];

                attachments[i] = new()
                {
                    BlendEnable = target.BlendEnable,
                    SrcColorBlendFactor = VKFormats.Vulkan(target.SrcBlend),
                    DstColorBlendFactor = VKFormats.Vulkan(target.DestBlend),
                    ColorBlendOp = VKFormats.Vulkan(target.BlendOp),
                    SrcAlphaBlendFactor = VKFormats.Vulkan(target.SrcBlendAlpha),
                    DstAlphaBlendFactor = VKFormats.Vulkan(target.DestBlendAlpha),
                    AlphaBlendOp = VKFormats.Vulkan(target.BlendOpAlpha),
                    ColorWriteMask = VKFormats.Vulkan(target.Flags)
                };

                colorAttachmentFormats[i] = VKFormats.Vulkan(desc.Output.ColorAttachments[i], TextureUsageFlags.None).Format;
            }

            Format depthStencilFormat = hasDepthStencilAttachment ? VKFormats.Vulkan(desc.Output.DepthStencilAttachment!.Value, TextureUsageFlags.None).Format : Format.Undefined;

            PipelineRasterizationStateCreateInfo rasterizationState = new()
            {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                DepthClampEnable = desc.RenderStates.RasterizerState.DepthClipEnable,
                PolygonMode = VKFormats.Vulkan(desc.RenderStates.RasterizerState.FillMode),
                CullMode = VKFormats.Vulkan(desc.RenderStates.RasterizerState.CullMode),
                FrontFace = VKFormats.Vulkan(desc.RenderStates.RasterizerState.FrontFace),
                DepthBiasEnable = true,
                DepthBiasConstantFactor = desc.RenderStates.RasterizerState.DepthBias,
                DepthBiasClamp = desc.RenderStates.RasterizerState.DepthBiasClamp,
                DepthBiasSlopeFactor = desc.RenderStates.RasterizerState.SlopeScaledDepthBias,
                LineWidth = 1.0f
            };
            PipelineDepthStencilStateCreateInfo depthStencilState = new()
            {
                SType = StructureType.PipelineDepthStencilStateCreateInfo,
                DepthTestEnable = desc.RenderStates.DepthStencilState.DepthEnable,
                DepthWriteEnable = desc.RenderStates.DepthStencilState.DepthWriteEnable,
                DepthCompareOp = VKFormats.Vulkan(desc.RenderStates.DepthStencilState.DepthFunc),
                StencilTestEnable = desc.RenderStates.DepthStencilState.StencilEnable,
                Front = new()
                {
                    FailOp = VKFormats.Vulkan(desc.RenderStates.DepthStencilState.FrontFace.StencilFailOp),
                    PassOp = VKFormats.Vulkan(desc.RenderStates.DepthStencilState.FrontFace.StencilPassOp),
                    DepthFailOp = VKFormats.Vulkan(desc.RenderStates.DepthStencilState.FrontFace.StencilDepthFailOp),
                    CompareOp = VKFormats.Vulkan(desc.RenderStates.DepthStencilState.FrontFace.StencilFunc),
                    CompareMask = desc.RenderStates.DepthStencilState.StencilReadMask,
                    WriteMask = desc.RenderStates.DepthStencilState.StencilWriteMask,
                    Reference = desc.RenderStates.StencilReference
                },
                Back = new()
                {
                    FailOp = VKFormats.Vulkan(desc.RenderStates.DepthStencilState.BackFace.StencilFailOp),
                    PassOp = VKFormats.Vulkan(desc.RenderStates.DepthStencilState.BackFace.StencilPassOp),
                    DepthFailOp = VKFormats.Vulkan(desc.RenderStates.DepthStencilState.BackFace.StencilDepthFailOp),
                    CompareOp = VKFormats.Vulkan(desc.RenderStates.DepthStencilState.BackFace.StencilFunc),
                    CompareMask = desc.RenderStates.DepthStencilState.StencilReadMask,
                    WriteMask = desc.RenderStates.DepthStencilState.StencilWriteMask,
                    Reference = desc.RenderStates.StencilReference
                },
                MinDepthBounds = 0.0f,
                MaxDepthBounds = 1.0f
            };
            PipelineColorBlendStateCreateInfo colorBlendState = new()
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                AttachmentCount = colorAttachmentCount,
                PAttachments = attachments
            };
            PipelineViewportStateCreateInfo viewportState = new()
            {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = colorAttachmentCount,
                ScissorCount = colorAttachmentCount
            };
            PipelineMultisampleStateCreateInfo multisampleState = new()
            {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                RasterizationSamples = VKFormats.Vulkan(desc.Output.SampleCount),
                AlphaToCoverageEnable = desc.RenderStates.BlendState.AlphaToCoverageEnable
            };
            PipelineRenderingCreateInfo rendering = new()
            {
                SType = StructureType.PipelineRenderingCreateInfo,
                ColorAttachmentCount = colorAttachmentCount,
                PColorAttachmentFormats = colorAttachmentFormats,
                DepthAttachmentFormat = depthStencilFormat,
                StencilAttachmentFormat = depthStencilFormat
            };

            if (desc.RenderStates.BlendFactor.HasValue)
            {
                colorBlendState.BlendConstants[0] = desc.RenderStates.BlendFactor.Value.X;
                colorBlendState.BlendConstants[1] = desc.RenderStates.BlendFactor.Value.Y;
                colorBlendState.BlendConstants[2] = desc.RenderStates.BlendFactor.Value.Z;
                colorBlendState.BlendConstants[3] = desc.RenderStates.BlendFactor.Value.W;
            }

            createInfo.PRasterizationState = &rasterizationState;
            createInfo.PDepthStencilState = &depthStencilState;
            createInfo.PColorBlendState = &colorBlendState;
            createInfo.PViewportState = &viewportState;
            createInfo.PMultisampleState = &multisampleState;
            createInfo.PNext = &rendering;
        }

        // Vertex - Hull - Domain - Geometry - Pixel
        {
            List<PipelineShaderStageCreateInfo> pipelineShaderStageCreateInfos =
            [
                desc.Vertex.Vulkan().GetPipelineShaderStageCreateInfo(scope),
                desc.Pixel.Vulkan().GetPipelineShaderStageCreateInfo(scope)
            ];

            if (desc.Hull is not null)
            {
                pipelineShaderStageCreateInfos.Add(desc.Hull.Vulkan().GetPipelineShaderStageCreateInfo(scope));
            }

            if (desc.Domain is not null)
            {
                pipelineShaderStageCreateInfos.Add(desc.Domain.Vulkan().GetPipelineShaderStageCreateInfo(scope));
            }

            if (desc.Geometry is not null)
            {
                pipelineShaderStageCreateInfos.Add(desc.Geometry.Vulkan().GetPipelineShaderStageCreateInfo(scope));
            }

            PipelineShaderStageCreateInfo* stages = (PipelineShaderStageCreateInfo*)ZenithMarshal.Allocate<PipelineShaderStageCreateInfo>(scope, (uint)pipelineShaderStageCreateInfos.Count);
            for (int i = 0; i < pipelineShaderStageCreateInfos.Count; i++)
            {
                stages[i] = pipelineShaderStageCreateInfos[i];
            }

            createInfo.StageCount = (uint)pipelineShaderStageCreateInfos.Count;
            createInfo.PStages = stages;
        }

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

                foreach (InputElement inputElement in inputLayout.Elements)
                {
                    vertexAttributeDescriptions[attribute] = new()
                    {
                        Location = attribute,
                        Binding = binding,
                        Format = VKFormats.Vulkan(inputElement.Format),
                        Offset = inputElement.OffsetInBytes
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

            if (desc.PrimitiveTopology >= PrimitiveTopology.PatchList)
            {
                PipelineTessellationStateCreateInfo tessellationState = new()
                {
                    SType = StructureType.PipelineTessellationStateCreateInfo,
                    PatchControlPoints = (uint)(desc.PrimitiveTopology - PrimitiveTopology.PatchList + 1)
                };

                createInfo.PTessellationState = &tessellationState;
            }
        }

        PipelineDynamicStateCreateInfo dynamicState = new()
        {
            SType = StructureType.PipelineDynamicStateCreateInfo,
            DynamicStateCount = 2,
            PDynamicStates = (DynamicState*)ZenithMarshal.AllocateAndFill(scope, [DynamicState.Viewport, DynamicState.Scissor])
        };

        createInfo.PDynamicState = &dynamicState;

        context.Vk.CreateGraphicsPipelines(context.Device, default, 1, &createInfo, null, out Pipeline).Success();
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
