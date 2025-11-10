using Silk.NET.Vulkan;

namespace Zenith.NET;

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

            createInfo.PRasterizationState = &rasterizationState;

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
                    Reference = (uint)desc.RenderStates.StencilReference
                },
                Back = new()
                {
                    FailOp = VKFormats.Vulkan(desc.RenderStates.DepthStencilState.BackFace.StencilFailOp),
                    PassOp = VKFormats.Vulkan(desc.RenderStates.DepthStencilState.BackFace.StencilPassOp),
                    DepthFailOp = VKFormats.Vulkan(desc.RenderStates.DepthStencilState.BackFace.StencilDepthFailOp),
                    CompareOp = VKFormats.Vulkan(desc.RenderStates.DepthStencilState.BackFace.StencilFunc),
                    CompareMask = desc.RenderStates.DepthStencilState.StencilReadMask,
                    WriteMask = desc.RenderStates.DepthStencilState.StencilWriteMask,
                    Reference = (uint)desc.RenderStates.StencilReference
                },
                MinDepthBounds = 0.0f,
                MaxDepthBounds = 1.0f
            };

            createInfo.PDepthStencilState = &depthStencilState;

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

            PipelineColorBlendAttachmentState* attachments = (PipelineColorBlendAttachmentState*)ZenithMarshal.Allocate<PipelineColorBlendAttachmentState>(scope, (uint)desc.Output.ColorAttachments.Length);

            for (int i = 0; i < desc.Output.ColorAttachments.Length; i++)
            {
                BlendStateRenderTarget usedBlendState = desc.RenderStates.BlendState.IndependentBlendEnable ? blendStateRenderTargets[i] : blendStateRenderTargets[0];

                attachments[i] = new()
                {
                    BlendEnable = usedBlendState.BlendEnable,
                    SrcColorBlendFactor = VKFormats.Vulkan(usedBlendState.SrcBlend),
                    DstColorBlendFactor = VKFormats.Vulkan(usedBlendState.DestBlend),
                    ColorBlendOp = VKFormats.Vulkan(usedBlendState.BlendOp),
                    SrcAlphaBlendFactor = VKFormats.Vulkan(usedBlendState.SrcBlendAlpha),
                    DstAlphaBlendFactor = VKFormats.Vulkan(usedBlendState.DestBlendAlpha),
                    AlphaBlendOp = VKFormats.Vulkan(usedBlendState.BlendOpAlpha),
                    ColorWriteMask = VKFormats.Vulkan(usedBlendState.Flags)
                };
            }

            PipelineColorBlendStateCreateInfo colorBlendState = new()
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                AttachmentCount = (uint)desc.Output.ColorAttachments.Length,
                PAttachments = attachments
            };

            if (desc.RenderStates.BlendFactor.HasValue)
            {
                colorBlendState.BlendConstants[0] = desc.RenderStates.BlendFactor.Value.X;
                colorBlendState.BlendConstants[1] = desc.RenderStates.BlendFactor.Value.Y;
                colorBlendState.BlendConstants[2] = desc.RenderStates.BlendFactor.Value.Z;
                colorBlendState.BlendConstants[3] = desc.RenderStates.BlendFactor.Value.W;
            }

            createInfo.PColorBlendState = &colorBlendState;
        }
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
