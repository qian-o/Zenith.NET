using System.Runtime.InteropServices;

namespace Zenith.NET;

public abstract class ValidationLayer(GraphicsContext context) : GraphicsResource(context)
{
    protected void Report(MessageSource source, MessageSeverity severity, string message)
    {
        Context.OnValidationMessage(new(source, severity, message));
    }

    internal void InternalReport(MessageSeverity severity, string message)
    {
        Report(MessageSource.Framework, severity, message);
    }

    internal void ValidateDesc(SwapChainDesc desc)
    {
        if (desc.Surface.Handles is null)
        {
            InternalReport(MessageSeverity.Error, "SwapChainDesc.Surface must have valid handles.");

            return;
        }

        switch (desc.Surface.Type)
        {
            case SurfaceType.Win32:
                if (desc.Surface.Handles.Length is not 1)
                {
                    InternalReport(MessageSeverity.Error, "SwapChainDesc.Surface.Handles must have exactly one handle for SurfaceType.Win32.");
                }
                break;

            case SurfaceType.Wayland:
                if (desc.Surface.Handles.Length is not 2)
                {
                    InternalReport(MessageSeverity.Error, "SwapChainDesc.Surface.Handles must have exactly two handles for SurfaceType.Wayland.");
                }
                break;

            case SurfaceType.Xlib:
                if (desc.Surface.Handles.Length is not 2)
                {
                    InternalReport(MessageSeverity.Error, "SwapChainDesc.Surface.Handles must have exactly two handles for SurfaceType.Xlib.");
                }
                break;

            case SurfaceType.Android:
                if (desc.Surface.Handles.Length is not 1)
                {
                    InternalReport(MessageSeverity.Error, "SwapChainDesc.Surface.Handles must have exactly one handle for SurfaceType.Android.");
                }
                break;

            case SurfaceType.IOS:
                if (desc.Surface.Handles.Length is not 1)
                {
                    InternalReport(MessageSeverity.Error, "SwapChainDesc.Surface.Handles must have exactly one handle for SurfaceType.IOS.");
                }
                break;

            case SurfaceType.MacOS:
                if (desc.Surface.Handles.Length is not 1)
                {
                    InternalReport(MessageSeverity.Error, "SwapChainDesc.Surface.Handles must have exactly one handle for SurfaceType.MacOS.");
                }
                break;

            case SurfaceType.PixelBuffer:
                if (desc.Surface.Handles.Length is not 1)
                {
                    InternalReport(MessageSeverity.Error, "SwapChainDesc.Surface.Handles must have exactly one handle for SurfaceType.PixelBuffer.");
                }

                if (GCHandle.FromIntPtr(desc.Surface.Handles[0]).Target is not PixelBuffer)
                {
                    InternalReport(MessageSeverity.Error, "SwapChainDesc.Surface.Handles[0] must be a valid PixelBuffer handle for SurfaceType.PixelBuffer.");
                }
                break;

            default:
                InternalReport(MessageSeverity.Error, $"SwapChainDesc.Surface has unsupported SurfaceType '{desc.Surface.Type}'.");
                break;
        }

        if (!Enum.IsDefined(desc.ColorTargetFormat))
        {
            InternalReport(MessageSeverity.Error, $"SwapChainDesc.ColorTargetFormat has an invalid value '{desc.ColorTargetFormat}'.");
        }

        if (desc.DepthStencilTargetFormat is not null && !Enum.IsDefined(desc.DepthStencilTargetFormat.Value))
        {
            InternalReport(MessageSeverity.Error, $"SwapChainDesc.DepthStencilTargetFormat has an invalid value '{desc.DepthStencilTargetFormat.Value}'.");
        }
    }

    internal void ValidateDesc(FrameBufferDesc desc)
    {
        if (desc.ColorTargets is null)
        {
            InternalReport(MessageSeverity.Error, "FrameBufferDesc.ColorTargets must not be null.");

            return;
        }

        for (int i = 0; i < desc.ColorTargets.Length; i++)
        {
            CheckFrameBufferAttachment($"FrameBufferDesc.ColorTargets[{i}]", desc.ColorTargets[i]);
        }

        if (desc.DepthStencilTarget is not null)
        {
            CheckFrameBufferAttachment("FrameBufferDesc.DepthStencilTarget", desc.DepthStencilTarget.Value);
        }

        if (desc.ColorTargets.Length is 0 && desc.DepthStencilTarget is null)
        {
            InternalReport(MessageSeverity.Warning, "FrameBufferDesc has no attachments.");
        }

        void CheckFrameBufferAttachment(string name, FrameBufferAttachment frameBufferAttachment)
        {
            if (frameBufferAttachment.Target is null)
            {
                InternalReport(MessageSeverity.Error, $"{name}.Target must not be null.");

                return;
            }

            if (frameBufferAttachment.Slice.Face is >= 6)
            {
                InternalReport(MessageSeverity.Error, $"{name}.Slice.Face must be less than 6.");
            }

            if (frameBufferAttachment.Slice.Layer >= frameBufferAttachment.Target.Desc.Layers)
            {
                InternalReport(MessageSeverity.Error, $"{name}.Slice.Layer must be less than the number of layers in the texture.");
            }

            if (frameBufferAttachment.Slice.MipLevel >= frameBufferAttachment.Target.Desc.MipLevels)
            {
                InternalReport(MessageSeverity.Error, $"{name}.Slice.MipLevel must be less than the number of mip levels in the texture.");
            }
        }
    }

    internal void ValidateDesc(ShaderDesc desc)
    {
        if (desc.ShaderBytes is null || desc.ShaderBytes.Length is 0)
        {
            InternalReport(MessageSeverity.Error, "ShaderDesc.ShaderBytes must not be null or empty.");
        }

        if (string.IsNullOrWhiteSpace(desc.EntryPoint))
        {
            InternalReport(MessageSeverity.Error, "ShaderDesc.EntryPoint must not be null or whitespace.");
        }

        if (!Enum.IsDefined(desc.Stage))
        {
            InternalReport(MessageSeverity.Error, $"ShaderDesc.Stage has an invalid value '{desc.Stage}'.");
        }
    }

    internal void ValidateDesc(BufferDesc desc)
    {
        if (desc.SizeInBytes is 0)
        {
            InternalReport(MessageSeverity.Error, "BufferDesc.SizeInBytes must be greater than zero.");
        }

        if (desc.StrideInBytes is 0)
        {
            InternalReport(MessageSeverity.Warning, "BufferDesc.StrideInBytes is zero, which may be valid for some buffer types but could indicate an issue.");
        }

        if (desc.Flags is BufferUsageFlags.None)
        {
            InternalReport(MessageSeverity.Warning, "BufferDesc.Flags is set to None, which may be valid but could indicate an issue.");
        }
    }

    internal void ValidateDesc(BufferViewDesc desc)
    {
        if (desc.Buffer is null)
        {
            InternalReport(MessageSeverity.Error, "BufferViewDesc.Buffer must not be null.");

            return;
        }

        if (desc.OffsetInBytes >= desc.Buffer.Desc.SizeInBytes)
        {
            InternalReport(MessageSeverity.Error, "BufferViewDesc.OffsetInBytes must be less than the size of the buffer.");
        }

        if (desc.SizeInBytes is 0 || desc.OffsetInBytes + desc.SizeInBytes > desc.Buffer.Desc.SizeInBytes)
        {
            InternalReport(MessageSeverity.Error, "BufferViewDesc.SizeInBytes must be greater than zero and within the bounds of the buffer.");
        }

        if (desc.StrideInBytes is 0)
        {
            InternalReport(MessageSeverity.Warning, "BufferViewDesc.StrideInBytes is zero, which may be valid for some buffer views but could indicate an issue.");
        }
    }

    internal void ValidateDesc(TextureDesc desc)
    {
        if (!Enum.IsDefined(desc.Type))
        {
            InternalReport(MessageSeverity.Error, $"TextureDesc.Type has an invalid value '{desc.Type}'.");
        }

        if (!Enum.IsDefined(desc.Format))
        {
            InternalReport(MessageSeverity.Error, $"TextureDesc.Format has an invalid value '{desc.Format}'.");
        }

        if (desc.Width is 0 || desc.Height is 0 || desc.Depth is 0)
        {
            InternalReport(MessageSeverity.Error, "TextureDesc dimensions (Width, Height, Depth) must be greater than zero.");
        }

        if (desc.Layers is 0)
        {
            InternalReport(MessageSeverity.Error, "TextureDesc.Layers must be greater than zero.");
        }

        if (desc.MipLevels is 0)
        {
            InternalReport(MessageSeverity.Error, "TextureDesc.MipLevels must be greater than zero.");
        }

        if (!Enum.IsDefined(desc.SampleCount))
        {
            InternalReport(MessageSeverity.Error, $"TextureDesc.SampleCount has an invalid value '{desc.SampleCount}'.");
        }

        if (desc.Flags is TextureUsageFlags.None)
        {
            InternalReport(MessageSeverity.Warning, "TextureDesc.Flags is set to None, which may be valid but could indicate an issue.");
        }
    }

    internal void ValidateDesc(TextureViewDesc desc)
    {
        if (desc.Texture is null)
        {
            InternalReport(MessageSeverity.Error, "TextureViewDesc.Texture must not be null.");

            return;
        }

        if (desc.FirstLayer >= desc.Texture.Desc.Layers)
        {
            InternalReport(MessageSeverity.Error, "TextureViewDesc.FirstLayer must be less than the number of layers in the texture.");
        }

        if (desc.LayerCount is 0 || desc.FirstLayer + desc.LayerCount > desc.Texture.Desc.Layers)
        {
            InternalReport(MessageSeverity.Error, "TextureViewDesc.LayerCount must be greater than zero and within the bounds of the texture layers.");
        }

        if (desc.FirstMipLevel >= desc.Texture.Desc.MipLevels)
        {
            InternalReport(MessageSeverity.Error, "TextureViewDesc.FirstMipLevel must be less than the number of mip levels in the texture.");
        }

        if (desc.MipLevelCount is 0 || desc.FirstMipLevel + desc.MipLevelCount > desc.Texture.Desc.MipLevels)
        {
            InternalReport(MessageSeverity.Error, "TextureViewDesc.MipLevelCount must be greater than zero and within the bounds of the texture mip levels.");
        }
    }

    internal void ValidateDesc(SamplerDesc desc)
    {
        if (!Enum.IsDefined(desc.U))
        {
            InternalReport(MessageSeverity.Error, $"SamplerDesc.U has an invalid value '{desc.U}'.");
        }

        if (!Enum.IsDefined(desc.V))
        {
            InternalReport(MessageSeverity.Error, $"SamplerDesc.V has an invalid value '{desc.V}'.");
        }

        if (!Enum.IsDefined(desc.W))
        {
            InternalReport(MessageSeverity.Error, $"SamplerDesc.W has an invalid value '{desc.W}'.");
        }

        if (!Enum.IsDefined(desc.Filter))
        {
            InternalReport(MessageSeverity.Error, $"SamplerDesc.Filter has an invalid value '{desc.Filter}'.");
        }

        if (!Enum.IsDefined(desc.ComparisonFunc))
        {
            InternalReport(MessageSeverity.Error, $"SamplerDesc.ComparisonFunc has an invalid value '{desc.ComparisonFunc}'.");
        }

        if (desc.MinLod > desc.MaxLod)
        {
            InternalReport(MessageSeverity.Error, "SamplerDesc.MinLod must be less than or equal to MaxLod.");
        }

        if (!Enum.IsDefined(desc.BorderColor))
        {
            InternalReport(MessageSeverity.Error, $"SamplerDesc.BorderColor has an invalid value '{desc.BorderColor}'.");
        }
    }

    internal void ValidateDesc(ResourceLayoutDesc desc)
    {
        if (desc.Bindings is null)
        {
            InternalReport(MessageSeverity.Error, "ResourceLayoutDesc.Bindings must not be null.");

            return;
        }

        foreach (ResourceBinding binding in desc.Bindings)
        {
            if (!Enum.IsDefined(binding.Type))
            {
                InternalReport(MessageSeverity.Error, $"ResourceLayoutBinding.Type has an invalid value '{binding.Type}'.");
            }
        }

        if (desc.Bindings.Length is 0)
        {
            InternalReport(MessageSeverity.Warning, "ResourceLayoutDesc has no bindings.");
        }
    }

    internal void ValidateDesc(ResourceSetDesc desc)
    {
        if (desc.Layout is null)
        {
            InternalReport(MessageSeverity.Error, "ResourceSetDesc.Layout must not be null.");

            return;
        }

        if (desc.Resources is null)
        {
            InternalReport(MessageSeverity.Error, "ResourceSetDesc.Resources must not be null.");

            return;
        }

        if (desc.Resources.Length != desc.Layout.Desc.Bindings.Length)
        {
            InternalReport(MessageSeverity.Error, "ResourceSetDesc.Resources length must match the number of bindings in the layout.");

            return;
        }

        int i = 0;
        foreach (IBindableResource resource in desc.Resources)
        {
            switch (desc.Layout.Desc.Bindings[i++].Type)
            {
                case ResourceType.ConstantBuffer:
                    if (resource is not Buffer or BufferView)
                    {
                        InternalReport(MessageSeverity.Error, "ResourceSetDesc.Resources item must be a Buffer or BufferView for ConstantBuffer binding.");
                    }
                    break;

                case ResourceType.StructuredBuffer:
                    if (resource is not Buffer or BufferView)
                    {
                        InternalReport(MessageSeverity.Error, "ResourceSetDesc.Resources item must be a Buffer or BufferView for StructuredBuffer binding.");
                    }
                    break;

                case ResourceType.StructuredBufferReadWrite:
                    if (resource is not Buffer or BufferView)
                    {
                        InternalReport(MessageSeverity.Error, "ResourceSetDesc.Resources item must be a Buffer or BufferView for StructuredBufferReadWrite binding.");
                    }
                    break;

                case ResourceType.Texture:
                    if (resource is not Texture or TextureView)
                    {
                        InternalReport(MessageSeverity.Error, "ResourceSetDesc.Resources item must be a Texture or TextureView for Texture binding.");
                    }
                    break;

                case ResourceType.TextureReadWrite:
                    if (resource is not Texture or TextureView)
                    {
                        InternalReport(MessageSeverity.Error, "ResourceSetDesc.Resources item must be a Texture or TextureView for TextureReadWrite binding.");
                    }
                    break;

                case ResourceType.Sampler:
                    if (resource is not Sampler)
                    {
                        InternalReport(MessageSeverity.Error, "ResourceSetDesc.Resources item must be a Sampler for Sampler binding.");
                    }
                    break;

                case ResourceType.AccelerationStructure:
                    if (resource is not TopLevelAccelerationStructure)
                    {
                        InternalReport(MessageSeverity.Error, "ResourceSetDesc.Resources item must be a TopLevelAccelerationStructure for AccelerationStructure binding.");
                    }
                    break;
            }
        }
    }

    internal void ValidateDesc(GraphicsPipelineDesc desc)
    {
        CheckRenderStates("GraphicsPipelineDesc", desc.RenderStates);

        if (desc.Vertex is null)
        {
            InternalReport(MessageSeverity.Error, "GraphicsPipelineDesc.Vertex must not be null.");
        }

        if (desc.Pixel is null)
        {
            InternalReport(MessageSeverity.Error, "GraphicsPipelineDesc.Pixel must not be null.");
        }

        if (desc.ResourceLayouts is null)
        {
            InternalReport(MessageSeverity.Error, "GraphicsPipelineDesc.ResourceLayouts must not be null.");
        }

        if (desc.InputLayouts is null)
        {
            InternalReport(MessageSeverity.Error, "GraphicsPipelineDesc.InputLayouts must not be null.");
        }
        else if (desc.InputLayouts.Length is 0)
        {
            InternalReport(MessageSeverity.Warning, "GraphicsPipelineDesc has no input layouts, which may be valid for pipelines without vertex input but could indicate an issue.");
        }
        else
        {
            for (int i = 0; i < desc.InputLayouts.Length; i++)
            {
                CheckInputLayout($"GraphicsPipelineDesc.InputLayouts[{i}]", desc.InputLayouts[i]);
            }
        }

        CheckOutput("GraphicsPipelineDesc.Output", desc.Output);

        void CheckInputLayout(string name, InputLayout inputLayout)
        {
            if (inputLayout.Elements is null)
            {
                InternalReport(MessageSeverity.Error, $"{name}.Elements must not be null.");

                return;
            }

            if (inputLayout.Elements.Length is 0)
            {
                InternalReport(MessageSeverity.Warning, $"{name} has no input elements, which may be valid for pipelines without vertex input but could indicate an issue.");
            }
            else
            {
                foreach (InputElement element in inputLayout.Elements)
                {
                    if (!Enum.IsDefined(element.Format))
                    {
                        InternalReport(MessageSeverity.Error, $"{name}.Elements has an invalid ElementFormat '{element.Format}'.");
                    }

                    if (!Enum.IsDefined(element.Semantic))
                    {
                        InternalReport(MessageSeverity.Error, $"{name}.Elements has an invalid ElementSemantic '{element.Semantic}'.");
                    }
                }
            }
        }
    }

    internal void ValidateDesc(ComputePipelineDesc desc)
    {
        if (desc.Compute is null)
        {
            InternalReport(MessageSeverity.Error, "ComputePipelineDesc.Compute must not be null.");
        }

        if (desc.ResourceLayouts is null)
        {
            InternalReport(MessageSeverity.Error, "GraphicsPipelineDesc.ResourceLayouts must not be null.");
        }

        if (desc.ThreadGroupSizeX is 0 || desc.ThreadGroupSizeY is 0 || desc.ThreadGroupSizeZ is 0)
        {
            InternalReport(MessageSeverity.Error, "ComputePipelineDesc thread group sizes (ThreadGroupSizeX, ThreadGroupSizeY, ThreadGroupSizeZ) must be greater than zero.");
        }
    }

    internal void ValidateDesc(RayTracingPipelineDesc desc)
    {
        if (desc.RayGeneration is null)
        {
            InternalReport(MessageSeverity.Error, "RayTracingPipelineDesc.RayGeneration must not be null.");
        }

        if (desc.Miss is null)
        {
            InternalReport(MessageSeverity.Error, "RayTracingPipelineDesc.Miss must not be null.");
        }

        if (desc.AnyHit is null)
        {
            InternalReport(MessageSeverity.Error, "RayTracingPipelineDesc.AnyHit must not be null.");
        }

        if (desc.Intersection is null)
        {
            InternalReport(MessageSeverity.Error, "RayTracingPipelineDesc.Intersection must not be null.");
        }

        if (desc.ClosestHit is null)
        {
            InternalReport(MessageSeverity.Error, "RayTracingPipelineDesc.ClosestHit must not be null.");
        }

        if (desc.HitGroups is null)
        {
            InternalReport(MessageSeverity.Error, "RayTracingPipelineDesc.HitGroups must not be null.");
        }

        if (desc.ResourceLayouts is null)
        {
            InternalReport(MessageSeverity.Error, "RayTracingPipelineDesc.ResourceLayouts must not be null.");
        }

        if (desc.MaxTraceRecursionDepth > 31)
        {
            InternalReport(MessageSeverity.Error, "RayTracingPipelineDesc.MaxTraceRecursionDepth must be less than or equal to 31.");
        }

        if (desc.MaxPayloadSizeInBytes is 0)
        {
            InternalReport(MessageSeverity.Error, "RayTracingPipelineDesc.MaxPayloadSizeInBytes must be greater than zero.");
        }

        if (desc.MaxAttributeSizeInBytes is 0)
        {
            InternalReport(MessageSeverity.Error, "RayTracingPipelineDesc.MaxAttributeSizeInBytes must be greater than zero.");
        }
    }

    internal void ValidateDesc(MeshShadingPipelineDesc desc)
    {
        CheckRenderStates("MeshShadingPipelineDesc", desc.RenderStates);

        if (desc.Mesh is null)
        {
            InternalReport(MessageSeverity.Error, "MeshShadingPipelineDesc.Mesh must not be null.");
        }

        if (desc.Pixel is null)
        {
            InternalReport(MessageSeverity.Error, "MeshShadingPipelineDesc.Pixel must not be null.");
        }

        if (desc.ResourceLayouts is null)
        {
            InternalReport(MessageSeverity.Error, "GraphicsPipelineDesc.ResourceLayouts must not be null.");
        }

        CheckOutput("MeshShadingPipelineDesc.Output", desc.Output);
    }

    internal void ValidateDesc(BottomLevelAccelerationStructureDesc desc)
    {
        throw new NotImplementedException();
    }

    internal void ValidateDesc(TopLevelAccelerationStructureDesc desc)
    {
        throw new NotImplementedException();
    }

    internal void ValidateDesc(TopLevelAccelerationStructureDesc oldDesc, TopLevelAccelerationStructureDesc newDesc)
    {
        throw new NotImplementedException();
    }

    private void CheckRenderStates(string name, RenderStates renderStates)
    {
        if (!Enum.IsDefined(renderStates.RasterizerState.CullMode))
        {
            InternalReport(MessageSeverity.Error, $"{name}.RenderStates.RasterizerState.CullMode has an invalid value '{renderStates.RasterizerState.CullMode}'.");
        }

        if (!Enum.IsDefined(renderStates.RasterizerState.FillMode))
        {
            InternalReport(MessageSeverity.Error, $"{name}.RenderStates.RasterizerState.FillMode has an invalid value '{renderStates.RasterizerState.FillMode}'.");
        }

        if (!Enum.IsDefined(renderStates.RasterizerState.FrontFace))
        {
            InternalReport(MessageSeverity.Error, $"{name}.RenderStates.RasterizerState.FrontFace has an invalid value '{renderStates.RasterizerState.FrontFace}'.");
        }

        if (!Enum.IsDefined(renderStates.DepthStencilState.DepthFunc))
        {
            InternalReport(MessageSeverity.Error, $"{name}.RenderStates.DepthStencilState.DepthFunc has an invalid value '{renderStates.DepthStencilState.DepthFunc}'.");
        }

        CheckDepthStencilStateOp($"{name}.RenderStates.DepthStencilState.FrontFace", renderStates.DepthStencilState.FrontFace);
        CheckDepthStencilStateOp($"{name}.RenderStates.DepthStencilState.BackFace", renderStates.DepthStencilState.BackFace);

        CheckBlendStateRenderTarget($"{name}.RenderStates.BlendState.RenderTarget0", renderStates.BlendState.RenderTarget0);
        CheckBlendStateRenderTarget($"{name}.RenderStates.BlendState.RenderTarget1", renderStates.BlendState.RenderTarget1);
        CheckBlendStateRenderTarget($"{name}.RenderStates.BlendState.RenderTarget2", renderStates.BlendState.RenderTarget2);
        CheckBlendStateRenderTarget($"{name}.RenderStates.BlendState.RenderTarget3", renderStates.BlendState.RenderTarget3);
        CheckBlendStateRenderTarget($"{name}.RenderStates.BlendState.RenderTarget4", renderStates.BlendState.RenderTarget4);
        CheckBlendStateRenderTarget($"{name}.RenderStates.BlendState.RenderTarget5", renderStates.BlendState.RenderTarget5);
        CheckBlendStateRenderTarget($"{name}.RenderStates.BlendState.RenderTarget6", renderStates.BlendState.RenderTarget6);
        CheckBlendStateRenderTarget($"{name}.RenderStates.BlendState.RenderTarget7", renderStates.BlendState.RenderTarget7);

        void CheckDepthStencilStateOp(string name, DepthStencilStateOp depthStencilStateOp)
        {
            if (!Enum.IsDefined(depthStencilStateOp.StencilFailOp))
            {
                InternalReport(MessageSeverity.Error, $"{name}.StencilFailOp has an invalid value '{depthStencilStateOp.StencilFailOp}'.");
            }

            if (!Enum.IsDefined(depthStencilStateOp.StencilDepthFailOp))
            {
                InternalReport(MessageSeverity.Error, $"{name}.StencilDepthFailOp has an invalid value '{depthStencilStateOp.StencilDepthFailOp}'.");
            }

            if (!Enum.IsDefined(depthStencilStateOp.StencilPassOp))
            {
                InternalReport(MessageSeverity.Error, $"{name}.StencilPassOp has an invalid value '{depthStencilStateOp.StencilPassOp}'.");
            }

            if (!Enum.IsDefined(depthStencilStateOp.StencilFunc))
            {
                InternalReport(MessageSeverity.Error, $"{name}.StencilFunc has an invalid value '{depthStencilStateOp.StencilFunc}'.");
            }
        }

        void CheckBlendStateRenderTarget(string name, BlendStateRenderTarget blendStateRenderTarget)
        {
            if (!Enum.IsDefined(blendStateRenderTarget.SrcBlend))
            {
                InternalReport(MessageSeverity.Error, $"{name}.SrcBlend has an invalid value '{blendStateRenderTarget.SrcBlend}'.");
            }

            if (!Enum.IsDefined(blendStateRenderTarget.DestBlend))
            {
                InternalReport(MessageSeverity.Error, $"{name}.DestBlend has an invalid value '{blendStateRenderTarget.DestBlend}'.");
            }

            if (!Enum.IsDefined(blendStateRenderTarget.BlendOp))
            {
                InternalReport(MessageSeverity.Error, $"{name}.BlendOp has an invalid value '{blendStateRenderTarget.BlendOp}'.");
            }

            if (!Enum.IsDefined(blendStateRenderTarget.SrcBlendAlpha))
            {
                InternalReport(MessageSeverity.Error, $"{name}.SrcBlendAlpha has an invalid value '{blendStateRenderTarget.SrcBlendAlpha}'.");
            }

            if (!Enum.IsDefined(blendStateRenderTarget.DestBlendAlpha))
            {
                InternalReport(MessageSeverity.Error, $"{name}.DestBlendAlpha has an invalid value '{blendStateRenderTarget.DestBlendAlpha}'.");
            }

            if (!Enum.IsDefined(blendStateRenderTarget.BlendOpAlpha))
            {
                InternalReport(MessageSeverity.Error, $"{name}.BlendOpAlpha has an invalid value '{blendStateRenderTarget.BlendOpAlpha}'.");
            }
        }
    }

    private void CheckOutput(string name, Output output)
    {
        if (output.ColorAttachments is null)
        {
            InternalReport(MessageSeverity.Error, $"{name}.ColorAttachments must not be null.");

            return;
        }

        for (int i = 0; i < output.ColorAttachments.Length; i++)
        {
            if (!Enum.IsDefined(output.ColorAttachments[i]))
            {
                InternalReport(MessageSeverity.Error, $"{name}.ColorAttachments[{i}] has an invalid value '{output.ColorAttachments[i]}'.");
            }
        }

        if (output.DepthStencilAttachment is not null && !Enum.IsDefined(output.DepthStencilAttachment.Value))
        {
            InternalReport(MessageSeverity.Error, $"{name}.DepthStencilAttachment has an invalid value '{output.DepthStencilAttachment.Value}'.");
        }


        if (output.ColorAttachments.Length is 0 && output.DepthStencilAttachment is null)
        {
            InternalReport(MessageSeverity.Warning, $"{name} has no attachments, which may be valid for pipelines without output but could indicate an issue.");
        }
    }
}
