using System.Numerics;

namespace Zenith.NET;

public abstract class ValidationLayer(GraphicsContext context) : GraphicsResource(context)
{
    private static readonly Dictionary<SurfaceType, int> ExpectedSurfaceHandleCount = new()
    {
        [SurfaceType.Win32] = 1,
        [SurfaceType.Wayland] = 2,
        [SurfaceType.Xlib] = 2,
        [SurfaceType.Android] = 1,
        [SurfaceType.Apple] = 1,
        [SurfaceType.D3D11Interop] = 1
    };

    protected void Report(MessageSource source, MessageSeverity severity, string message)
    {
        Context.OnValidationMessage(new(source, severity, message));
    }

    private void ReportError(string message)
    {
        Report(MessageSource.Framework, MessageSeverity.Error, message);
    }

    private void ReportWarning(string message)
    {
        Report(MessageSource.Framework, MessageSeverity.Warning, message);
    }

    #region Descriptor Validation

    internal void ValidateDesc(SwapChainDesc desc)
    {
        CheckSurface("SwapChainDesc.Surface", desc.Surface);

        CheckEnum("SwapChainDesc.ColorFormat", desc.ColorFormat);

        if (desc.DepthStencilFormat is { } format)
        {
            CheckEnum("SwapChainDesc.DepthStencilFormat", format);
        }
    }

    internal void ValidateDesc(BufferDesc desc)
    {
        CheckGreaterThanZero("BufferDesc.SizeInBytes", desc.SizeInBytes);

        if (desc.StrideInBytes is 0)
        {
            ReportWarning(string.Format(ValidationMessages.IsZeroWarning, "BufferDesc.StrideInBytes", "buffer types"));
        }

        CheckEnum("BufferDesc.Access", desc.Access);
        CheckFlags("BufferDesc.Usages", desc.Usages);

        if (desc.Access is BufferAccess.CpuReadOnly or BufferAccess.CpuWriteOnly)
        {
            const BufferUsages GpuOnlyUsages = BufferUsages.StorageReadWrite
                                             | BufferUsages.Indirect
                                             | BufferUsages.AccelerationStructure;

            BufferUsages forbiddenUsages = desc.Usages & GpuOnlyUsages;

            if (forbiddenUsages is not BufferUsages.None)
            {
                ReportError(string.Format(ValidationMessages.UsagesIncompatibleWithAccess, "BufferDesc.Usages", forbiddenUsages, desc.Access));
            }
        }
    }

    internal void ValidateDesc(BufferViewDesc desc)
    {
        if (!CheckResource("BufferViewDesc.Buffer", desc.Buffer))
        {
            return;
        }

        CheckBufferRange("BufferViewDesc", desc.Buffer, desc.OffsetInBytes, desc.SizeInBytes);

        if (desc.StrideInBytes is 0)
        {
            ReportWarning(string.Format(ValidationMessages.IsZeroWarning, "BufferViewDesc.StrideInBytes", "structured buffer views"));
        }
    }

    internal void ValidateDesc(TextureDesc desc)
    {
        CheckEnum("TextureDesc.Type", desc.Type);
        CheckEnum("TextureDesc.Format", desc.Format);

        if (desc.Width is 0 || desc.Height is 0 || desc.Depth is 0)
        {
            ReportError(string.Format(ValidationMessages.MustBeGreaterThanZero, "TextureDesc dimensions (Width, Height, Depth)"));
        }

        CheckGreaterThanZero("TextureDesc.MipLevels", desc.MipLevels);
        CheckGreaterThanZero("TextureDesc.ArrayLayers", desc.ArrayLayers);

        if (desc.Type is TextureType.Texture3D && desc.ArrayLayers is not 1)
        {
            ReportError(string.Format(ValidationMessages.MustBeEqualTo, "TextureDesc.ArrayLayers", 1));
        }

        if (desc.Type is TextureType.TextureCube && desc.ArrayLayers is not ValidationConstants.CubeMapFaceCount)
        {
            ReportError(string.Format(ValidationMessages.MustBeEqualTo, "TextureDesc.ArrayLayers", ValidationConstants.CubeMapFaceCount));
        }

        if (desc.Type is TextureType.TextureCubeArray && desc.ArrayLayers % ValidationConstants.CubeMapFaceCount is not 0)
        {
            ReportError(string.Format(ValidationMessages.MustBeAMultipleOf, "TextureDesc.ArrayLayers", ValidationConstants.CubeMapFaceCount));
        }

        CheckEnum("TextureDesc.SampleCount", desc.SampleCount);
        CheckFlags("TextureDesc.Usages", desc.Usages);

        if (desc.Usages is TextureUsages.None)
        {
            ReportWarning(string.Format(ValidationMessages.IsSetToNoneWarning, "TextureDesc.Usages"));
        }
    }

    internal void ValidateDesc(TextureViewDesc desc)
    {
        if (!CheckResource("TextureViewDesc.Texture", desc.Texture))
        {
            return;
        }

        CheckEnum("TextureViewDesc.Type", desc.Type);
        CheckEnum("TextureViewDesc.Format", desc.Format);

        if (desc.Range.BaseMipLevel >= desc.Texture.Desc.MipLevels)
        {
            ReportError(string.Format(ValidationMessages.MustBeLessThan, "TextureViewDesc.Range.BaseMipLevel", "the number of mip levels in the texture"));
        }

        if (desc.Range.LevelCount is 0 || desc.Range.BaseMipLevel + desc.Range.LevelCount > desc.Texture.Desc.MipLevels)
        {
            ReportError(string.Format(ValidationMessages.MustBeWithinBounds, "TextureViewDesc.Range.LevelCount", "the texture mip levels"));
        }

        if (desc.Range.BaseArrayLayer >= desc.Texture.Desc.ArrayLayers)
        {
            ReportError(string.Format(ValidationMessages.MustBeLessThan, "TextureViewDesc.Range.BaseArrayLayer", "the number of array layers in the texture"));
        }

        if (desc.Range.LayerCount is 0 || desc.Range.BaseArrayLayer + desc.Range.LayerCount > desc.Texture.Desc.ArrayLayers)
        {
            ReportError(string.Format(ValidationMessages.MustBeWithinBounds, "TextureViewDesc.Range.LayerCount", "the texture array layers"));
        }

        if (desc.Type is TextureType.TextureCube)
        {
            if (desc.Range.BaseArrayLayer % ValidationConstants.CubeMapFaceCount is not 0)
            {
                ReportError(string.Format(ValidationMessages.MustBeAMultipleOf, "TextureViewDesc.Range.BaseArrayLayer", ValidationConstants.CubeMapFaceCount));
            }

            if (desc.Range.LayerCount is not ValidationConstants.CubeMapFaceCount)
            {
                ReportError(string.Format(ValidationMessages.MustDescribeACompleteCube, "TextureViewDesc.Range.LayerCount"));
            }
        }

        if (desc.Type is TextureType.TextureCubeArray)
        {
            if (desc.Range.BaseArrayLayer % ValidationConstants.CubeMapFaceCount is not 0)
            {
                ReportError(string.Format(ValidationMessages.MustBeAMultipleOf, "TextureViewDesc.Range.BaseArrayLayer", ValidationConstants.CubeMapFaceCount));
            }

            if (desc.Range.LayerCount % ValidationConstants.CubeMapFaceCount is not 0)
            {
                ReportError(string.Format(ValidationMessages.MustBeAMultipleOf, "TextureViewDesc.Range.LayerCount", ValidationConstants.CubeMapFaceCount));
            }
        }
    }

    internal void ValidateDesc(SamplerDesc desc)
    {
        CheckEnum("SamplerDesc.MinFilter", desc.MinFilter);
        CheckEnum("SamplerDesc.MagFilter", desc.MagFilter);
        CheckEnum("SamplerDesc.MipFilter", desc.MipFilter);
        CheckEnum("SamplerDesc.AddressU", desc.AddressU);
        CheckEnum("SamplerDesc.AddressV", desc.AddressV);
        CheckEnum("SamplerDesc.AddressW", desc.AddressW);
        CheckEnum("SamplerDesc.CompareOp", desc.CompareOp);

        if (desc.MaxAnisotropy > ValidationConstants.MaxAnisotropy)
        {
            ReportError(string.Format(ValidationMessages.MustBeLessThanOrEqualTo, "SamplerDesc.MaxAnisotropy", ValidationConstants.MaxAnisotropy));
        }

        if (desc.MinLod > desc.MaxLod)
        {
            ReportError(string.Format(ValidationMessages.MustBeLessThanOrEqualTo, "SamplerDesc.MinLod", "MaxLod"));
        }

        CheckEnum("SamplerDesc.BorderColor", desc.BorderColor);
    }

    internal void ValidateDesc(ShaderDesc desc)
    {
        CheckArrayNotEmpty("ShaderDesc.Bytecode", desc.Bytecode);
        CheckStringNotWhitespace("ShaderDesc.EntryPoint", desc.EntryPoint);
        CheckEnum("ShaderDesc.Stage", desc.Stage);
    }

    internal void ValidateDesc(GraphicsPipelineDesc desc)
    {
        CheckResource("GraphicsPipelineDesc.VertexShader", desc.VertexShader);
        CheckResource("GraphicsPipelineDesc.FragmentShader", desc.FragmentShader);

        if (desc.InputLayouts is null)
        {
            ReportError(string.Format(ValidationMessages.MustNotBeNull, "GraphicsPipelineDesc.InputLayouts"));
        }
        else
        {
            for (int index = 0; index < desc.InputLayouts.Length; index++)
            {
                CheckInputLayout($"GraphicsPipelineDesc.InputLayouts[{index}]", desc.InputLayouts[index]);
            }
        }

        CheckEnum("GraphicsPipelineDesc.PrimitiveTopology", desc.PrimitiveTopology);
        CheckAttachmentFormats("GraphicsPipelineDesc.AttachmentFormats", desc.AttachmentFormats);
        CheckRenderState("GraphicsPipelineDesc.RenderState", desc.RenderState);
    }

    internal void ValidateDesc(ComputePipelineDesc desc)
    {
        CheckResource("ComputePipelineDesc.ComputeShader", desc.ComputeShader);
    }

    internal void ValidateDesc(MeshShadingPipelineDesc desc)
    {
        if (desc.TaskShader is not null)
        {
            CheckResource("MeshShadingPipelineDesc.TaskShader", desc.TaskShader);
        }

        CheckResource("MeshShadingPipelineDesc.MeshShader", desc.MeshShader);
        CheckResource("MeshShadingPipelineDesc.FragmentShader", desc.FragmentShader);

        if (desc.PrimitiveTopology is not PrimitiveTopology.LineList and not PrimitiveTopology.TriangleList)
        {
            ReportError(string.Format(ValidationMessages.MustBeOneOf, "MeshShadingPipelineDesc.PrimitiveTopology", "LineList, TriangleList"));
        }

        CheckAttachmentFormats("MeshShadingPipelineDesc.AttachmentFormats", desc.AttachmentFormats);
        CheckRenderState("MeshShadingPipelineDesc.RenderState", desc.RenderState);
    }

    internal void ValidateDesc(QueryHeapDesc desc)
    {
        CheckEnum("QueryHeapDesc.Type", desc.Type);
        CheckGreaterThanZero("QueryHeapDesc.Count", desc.Count);
    }

    internal void ValidateDesc(BottomLevelAccelerationStructureDesc desc)
    {
        if (CheckArrayNotEmpty("BottomLevelAccelerationStructureDesc.Geometries", desc.Geometries))
        {
            for (int index = 0; index < desc.Geometries.Length; index++)
            {
                CheckRayTracingGeometry($"BottomLevelAccelerationStructureDesc.Geometries[{index}]", desc.Geometries[index]);
            }
        }

        CheckFlags("BottomLevelAccelerationStructureDesc.BuildFlags", desc.BuildFlags);
    }

    internal void ValidateDesc(TopLevelAccelerationStructureDesc desc)
    {
        if (CheckArrayNotEmpty("TopLevelAccelerationStructureDesc.Instances", desc.Instances))
        {
            for (int index = 0; index < desc.Instances.Length; index++)
            {
                CheckRayTracingInstance($"TopLevelAccelerationStructureDesc.Instances[{index}]", desc.Instances[index]);
            }
        }

        CheckFlags("TopLevelAccelerationStructureDesc.BuildFlags", desc.BuildFlags);
    }

    internal void ValidateDesc(TopLevelAccelerationStructureDesc oldDesc, TopLevelAccelerationStructureDesc newDesc)
    {
        if (!oldDesc.BuildFlags.HasFlag(AccelerationStructureBuildFlags.AllowUpdate))
        {
            ReportError(string.Format(ValidationMessages.MustHaveFlag, "TopLevelAccelerationStructureDesc.BuildFlags", AccelerationStructureBuildFlags.AllowUpdate));
        }

        ValidateDesc(newDesc);

        if (newDesc.Instances is null)
        {
            return;
        }

        if (oldDesc.Instances.Length != newDesc.Instances.Length)
        {
            ReportError(ValidationMessages.InstanceCountMustRemainSame);
        }
    }

    #endregion

    #region Resource Validation

    internal bool ValidateResource<T>(string name, T? resource) where T : GraphicsResource
    {
        return CheckResource(name, resource);
    }

    internal bool ValidateMap(Buffer buffer)
    {
        if (!CheckResource("Map.buffer", buffer))
        {
            return false;
        }

        if (buffer.Desc.Access is not (BufferAccess.CpuReadOnly or BufferAccess.CpuWriteOnly))
        {
            ReportError(string.Format(ValidationMessages.MustBeCpuAccessible, "Map.buffer"));

            return false;
        }

        return true;
    }

    #endregion

    #region SwapChain Validation

    internal bool ValidateResize(uint width, uint height)
    {
        bool isValid = CheckGreaterThanZero("Resize.width", width);
        isValid &= CheckGreaterThanZero("Resize.height", height);

        return isValid;
    }

    internal void ValidateRefresh(Surface surface)
    {
        CheckSurface("Refresh.surface", surface);
    }

    #endregion

    #region Transition Validation

    internal bool ValidateTransition(Texture texture, TextureSubresource subresource, TextureState state)
    {
        bool isValid = CheckResource("Transition.texture", texture);

        if (isValid)
        {
            isValid &= CheckTextureSubresource("Transition.subresource", texture, subresource);
        }

        isValid &= CheckEnum("Transition.state", state);

        return isValid;
    }

    #endregion

    #region Transfer Validation

    internal bool ValidateUpload(Buffer buffer, uint offsetInBytes, BufferData data)
    {
        bool isValid = CheckResource("Upload.buffer", buffer);
        isValid &= CheckBufferData("Upload.data", data);

        if (isValid)
        {
            isValid &= CheckBufferUsage("Upload.buffer", buffer, BufferUsages.CopyDst);
            isValid &= CheckBufferRange("Upload.buffer", buffer, offsetInBytes, data.SizeInBytes);
        }

        return isValid;
    }

    internal bool ValidateDownload(Buffer buffer, uint offsetInBytes, BufferData data)
    {
        bool isValid = CheckResource("Download.buffer", buffer);
        isValid &= CheckBufferData("Download.data", data);

        if (isValid)
        {
            isValid &= CheckBufferUsage("Download.buffer", buffer, BufferUsages.CopySrc);
            isValid &= CheckBufferRange("Download.buffer", buffer, offsetInBytes, data.SizeInBytes);
        }

        return isValid;
    }

    internal bool ValidateUpload(Texture texture, TextureSubresource subresource, Offset3D offset, Extent3D extent, TextureData data)
    {
        bool isValid = CheckResource("Upload.texture", texture);
        isValid &= CheckTextureData("Upload.data", data, isValid ? texture.Desc.Format : PixelFormat.Unknown, extent);

        if (isValid)
        {
            isValid &= CheckTextureUsage("Upload.texture", texture, TextureUsages.CopyDst);
            isValid &= CheckTextureRange("Upload.texture", texture, subresource, offset, extent);
        }

        return isValid;
    }

    internal bool ValidateDownload(Texture texture, TextureSubresource subresource, Offset3D offset, Extent3D extent, TextureData data)
    {
        bool isValid = CheckResource("Download.texture", texture);
        isValid &= CheckTextureData("Download.data", data, isValid ? texture.Desc.Format : PixelFormat.Unknown, extent);

        if (isValid)
        {
            isValid &= CheckTextureUsage("Download.texture", texture, TextureUsages.CopySrc);
            isValid &= CheckTextureRange("Download.texture", texture, subresource, offset, extent);
        }

        return isValid;
    }

    internal bool ValidateCopyBuffer(Buffer src, uint srcOffsetInBytes, Buffer dst, uint dstOffsetInBytes, uint sizeInBytes)
    {
        bool hasSrc = CheckResource("CopyBuffer.src", src);
        bool hasDst = CheckResource("CopyBuffer.dst", dst);
        bool isValid = hasSrc && hasDst;

        if (hasSrc)
        {
            isValid &= CheckBufferUsage("CopyBuffer.src", src, BufferUsages.CopySrc);
            isValid &= CheckBufferRange("CopyBuffer.src", src, srcOffsetInBytes, sizeInBytes);
        }

        if (hasDst)
        {
            isValid &= CheckBufferUsage("CopyBuffer.dst", dst, BufferUsages.CopyDst);
            isValid &= CheckBufferRange("CopyBuffer.dst", dst, dstOffsetInBytes, sizeInBytes);
        }

        return isValid;
    }

    internal bool ValidateCopyBufferToTexture(Buffer src, uint srcOffsetInBytes, TextureDataLayout srcLayout, Texture dst, TextureSubresource dstSubresource, Offset3D dstOffset, Extent3D dstExtent)
    {
        bool hasSrc = CheckResource("CopyBufferToTexture.src", src);
        bool hasDst = CheckResource("CopyBufferToTexture.dst", dst);
        bool isValid = hasSrc && hasDst;

        if (hasSrc)
        {
            isValid &= CheckBufferUsage("CopyBufferToTexture.src", src, BufferUsages.CopySrc);
            isValid &= CheckTextureDataLayout("CopyBufferToTexture.srcLayout", srcLayout, hasDst ? dst.Desc.Format : PixelFormat.Unknown, dstExtent);
            isValid &= CheckBufferRange("CopyBufferToTexture.src", src, srcOffsetInBytes, srcLayout.SizeInBytes);
        }

        if (hasDst)
        {
            isValid &= CheckTextureUsage("CopyBufferToTexture.dst", dst, TextureUsages.CopyDst);
            isValid &= CheckTextureRange("CopyBufferToTexture.dst", dst, dstSubresource, dstOffset, dstExtent);
        }

        return isValid;
    }

    internal bool ValidateCopyTexture(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Texture dst, TextureSubresource dstSubresource, Offset3D dstOffset, Extent3D extent)
    {
        bool hasSrc = CheckResource("CopyTexture.src", src);
        bool hasDst = CheckResource("CopyTexture.dst", dst);
        bool isValid = hasSrc && hasDst;

        if (hasSrc)
        {
            isValid &= CheckTextureUsage("CopyTexture.src", src, TextureUsages.CopySrc);
            isValid &= CheckTextureRange("CopyTexture.src", src, srcSubresource, srcOffset, extent);
        }

        if (hasDst)
        {
            isValid &= CheckTextureUsage("CopyTexture.dst", dst, TextureUsages.CopyDst);
            isValid &= CheckTextureRange("CopyTexture.dst", dst, dstSubresource, dstOffset, extent);
        }

        if (hasSrc && hasDst)
        {
            isValid &= CheckSameValue("CopyTexture.Format", src.Desc.Format, dst.Desc.Format);
            isValid &= CheckSameValue("CopyTexture.SampleCount", src.Desc.SampleCount, dst.Desc.SampleCount);
        }

        return isValid;
    }

    internal bool ValidateCopyTextureToBuffer(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Extent3D srcExtent, Buffer dst, uint dstOffsetInBytes, TextureDataLayout dstLayout)
    {
        bool hasSrc = CheckResource("CopyTextureToBuffer.src", src);
        bool hasDst = CheckResource("CopyTextureToBuffer.dst", dst);
        bool isValid = hasSrc && hasDst;

        if (hasSrc)
        {
            isValid &= CheckTextureUsage("CopyTextureToBuffer.src", src, TextureUsages.CopySrc);
            isValid &= CheckTextureRange("CopyTextureToBuffer.src", src, srcSubresource, srcOffset, srcExtent);
            isValid &= CheckTextureDataLayout("CopyTextureToBuffer.dstLayout", dstLayout, src.Desc.Format, srcExtent);
        }

        if (hasDst)
        {
            isValid &= CheckBufferUsage("CopyTextureToBuffer.dst", dst, BufferUsages.CopyDst);
            isValid &= CheckBufferRange("CopyTextureToBuffer.dst", dst, dstOffsetInBytes, dstLayout.SizeInBytes);
        }

        return isValid;
    }

    internal bool ValidateResolveTexture(Texture src, TextureSubresource srcSubresource, Texture dst, TextureSubresource dstSubresource)
    {
        bool hasSrc = CheckResource("ResolveTexture.src", src);
        bool hasDst = CheckResource("ResolveTexture.dst", dst);
        bool isValid = hasSrc && hasDst;

        if (hasSrc)
        {
            isValid &= CheckTextureSubresource("ResolveTexture.srcSubresource", src, srcSubresource);

            if (src.Desc.SampleCount is SampleCount.Count1)
            {
                ReportError(string.Format(ValidationMessages.MustBeMultisampled, "ResolveTexture.src"));
                isValid = false;
            }
        }

        if (hasDst)
        {
            isValid &= CheckTextureSubresource("ResolveTexture.dstSubresource", dst, dstSubresource);

            if (dst.Desc.SampleCount is not SampleCount.Count1)
            {
                ReportError(string.Format(ValidationMessages.MustBeSingleSampled, "ResolveTexture.dst"));
                isValid = false;
            }
        }

        if (hasSrc && hasDst)
        {
            isValid &= CheckSameValue("ResolveTexture.Format", src.Desc.Format, dst.Desc.Format);
            isValid &= CheckSameMipExtent("ResolveTexture", src, srcSubresource, dst, dstSubresource);
        }

        return isValid;
    }

    #endregion

    #region Render Pass / Viewport Validation

    internal bool ValidateRenderPass(ReadOnlySpan<ColorAttachment> colorAttachments, DepthStencilAttachment? depthStencilAttachment)
    {
        bool isValid = true;
        bool hasExtent = false;
        uint renderWidth = 0;
        uint renderHeight = 0;

        if (colorAttachments.Length > ValidationConstants.MaxColorAttachments)
        {
            ReportError(string.Format(ValidationMessages.MustBeLessThanOrEqualTo, "BeginRenderPass.colorAttachments.Length", ValidationConstants.MaxColorAttachments));
            isValid = false;
        }

        if (colorAttachments.Length is 0 && depthStencilAttachment is null)
        {
            ReportError(string.Format(ValidationMessages.HasNoAttachments, "BeginRenderPass"));
            isValid = false;
        }

        for (int index = 0; index < colorAttachments.Length; index++)
        {
            isValid &= CheckColorAttachment($"BeginRenderPass.colorAttachments[{index}]", colorAttachments[index], ref hasExtent, ref renderWidth, ref renderHeight);
        }

        if (depthStencilAttachment is { } attachment)
        {
            isValid &= CheckDepthStencilAttachment("BeginRenderPass.depthStencilAttachment", attachment, ref hasExtent, ref renderWidth, ref renderHeight);
        }

        return isValid;
    }

    internal bool ValidateScissors(ReadOnlySpan<Scissor> scissors)
    {
        bool isValid = true;

        if (scissors.Length is 0)
        {
            ReportError(string.Format(ValidationMessages.MustNotBeNullOrEmpty, "SetScissors.scissors"));
            return false;
        }

        for (int index = 0; index < scissors.Length; index++)
        {
            isValid &= CheckGreaterThanZero($"SetScissors.scissors[{index}].Width", scissors[index].Width);
            isValid &= CheckGreaterThanZero($"SetScissors.scissors[{index}].Height", scissors[index].Height);
        }

        return isValid;
    }

    internal bool ValidateViewports(ReadOnlySpan<Viewport> viewports)
    {
        bool isValid = true;

        if (viewports.Length is 0)
        {
            ReportError(string.Format(ValidationMessages.MustNotBeNullOrEmpty, "SetViewports.viewports"));
            return false;
        }

        for (int index = 0; index < viewports.Length; index++)
        {
            Viewport viewport = viewports[index];

            isValid &= CheckFinite($"SetViewports.viewports[{index}].Width", viewport.Width);
            isValid &= CheckFinite($"SetViewports.viewports[{index}].Height", viewport.Height);
            isValid &= CheckFinite($"SetViewports.viewports[{index}].MinDepth", viewport.MinDepth);
            isValid &= CheckFinite($"SetViewports.viewports[{index}].MaxDepth", viewport.MaxDepth);

            if (viewport.Width <= 0)
            {
                ReportError(string.Format(ValidationMessages.MustBeGreaterThanZero, $"SetViewports.viewports[{index}].Width"));
                isValid = false;
            }

            if (viewport.Height <= 0)
            {
                ReportError(string.Format(ValidationMessages.MustBeGreaterThanZero, $"SetViewports.viewports[{index}].Height"));
                isValid = false;
            }

            if (viewport.MinDepth > viewport.MaxDepth)
            {
                ReportError(string.Format(ValidationMessages.MustBeLessThanOrEqualTo, $"SetViewports.viewports[{index}].MinDepth", $"SetViewports.viewports[{index}].MaxDepth"));
                isValid = false;
            }
        }

        return isValid;
    }

    #endregion

    #region Pipeline State Validation

    internal bool ValidateCurrentPipeline(string commandName)
    {
        ReportError(string.Format(ValidationMessages.MustHaveCurrentPipeline, commandName));

        return false;
    }

    internal bool ValidateCurrentPipeline<TPipeline>(string commandName, Pipeline? currentPipeline) where TPipeline : Pipeline
    {
        if (currentPipeline is null)
        {
            ReportError(string.Format(ValidationMessages.MustHaveCurrentPipeline, commandName));
        }
        else
        {
            ReportError(string.Format(ValidationMessages.MustHaveCurrentPipelineType, commandName, typeof(TPipeline).Name, currentPipeline.GetType().Name));
        }

        return false;
    }

    internal bool ValidateSetStencilReference(Pipeline? currentPipeline)
    {
        if (currentPipeline is null)
        {
            return ValidateCurrentPipeline("SetStencilReference");
        }

        if (currentPipeline is not GraphicsPipeline and not MeshShadingPipeline)
        {
            return ValidateCurrentPipeline<GraphicsPipeline>("SetStencilReference", currentPipeline);
        }

        return true;
    }

    internal bool ValidateSetBlendConstant(Pipeline? currentPipeline, Vector4 blendConstant)
    {
        bool isValid = true;

        if (currentPipeline is null)
        {
            isValid = ValidateCurrentPipeline("SetBlendConstant");
        }
        else if (currentPipeline is not GraphicsPipeline and not MeshShadingPipeline)
        {
            isValid = ValidateCurrentPipeline<GraphicsPipeline>("SetBlendConstant", currentPipeline);
        }

        isValid &= CheckFinite("SetBlendConstant.blendConstant.X", blendConstant.X);
        isValid &= CheckFinite("SetBlendConstant.blendConstant.Y", blendConstant.Y);
        isValid &= CheckFinite("SetBlendConstant.blendConstant.Z", blendConstant.Z);
        isValid &= CheckFinite("SetBlendConstant.blendConstant.W", blendConstant.W);

        return isValid;
    }

    internal bool ValidateSetVertexBuffer(Buffer buffer, uint offsetInBytes, uint slot, GraphicsPipeline pipeline)
    {
        bool isValid = CheckResource("SetVertexBuffer.buffer", buffer);

        if (isValid)
        {
            isValid &= CheckBufferUsage("SetVertexBuffer.buffer", buffer, BufferUsages.Vertex);
            isValid &= CheckBufferOffset("SetVertexBuffer.buffer", buffer, offsetInBytes);
        }

        if (pipeline.Desc.InputLayouts is not null && slot >= pipeline.Desc.InputLayouts.Length)
        {
            ReportError(string.Format(ValidationMessages.MustBeLessThan, "SetVertexBuffer.slot", "GraphicsPipelineDesc.InputLayouts.Length"));
            isValid = false;
        }

        return isValid;
    }

    internal bool ValidateSetIndexBuffer(Buffer buffer, uint offsetInBytes, IndexFormat indexFormat)
    {
        bool isValid = CheckResource("SetIndexBuffer.buffer", buffer);
        isValid &= CheckEnum("SetIndexBuffer.indexFormat", indexFormat);

        if (isValid)
        {
            isValid &= CheckBufferUsage("SetIndexBuffer.buffer", buffer, BufferUsages.Index);
            isValid &= CheckBufferOffset("SetIndexBuffer.buffer", buffer, offsetInBytes);
        }

        return isValid;
    }

    internal bool ValidateSetConstants<T>() where T : unmanaged, IConstantsLayout<T>
    {
        uint sizeInBytes = Context.GraphicsApi switch
        {
            GraphicsApi.DirectX12 => T.DirectX12SizeInBytes,
            GraphicsApi.Metal => T.MetalSizeInBytes,
            GraphicsApi.Vulkan => T.VulkanSizeInBytes,
            _ => 0
        };

        if (sizeInBytes is 0)
        {
            ReportError(string.Format(ValidationMessages.MustHaveNonZeroConstantsSize, typeof(T).Name, Context.GraphicsApi));

            return false;
        }

        return true;
    }

    #endregion

    #region Indirect / Query / Debug Validation

    internal bool ValidateDrawIndirect(Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        return ValidateIndirectBuffer(nameof(CommandBuffer.DrawIndirect), indirectBuffer, offsetInBytes, ValidationConstants.IndirectDrawArgsSizeInBytes, drawCount);
    }

    internal bool ValidateDrawIndexedIndirect(Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        return ValidateIndirectBuffer(nameof(CommandBuffer.DrawIndexedIndirect), indirectBuffer, offsetInBytes, ValidationConstants.IndirectDrawIndexedArgsSizeInBytes, drawCount);
    }

    internal bool ValidateDispatchIndirect(Buffer indirectBuffer, uint offsetInBytes)
    {
        return ValidateIndirectBuffer(nameof(CommandBuffer.DispatchIndirect), indirectBuffer, offsetInBytes, ValidationConstants.IndirectDispatchArgsSizeInBytes, 1);
    }

    internal bool ValidateDispatchMeshIndirect(Buffer indirectBuffer, uint offsetInBytes, uint dispatchCount)
    {
        return ValidateIndirectBuffer(nameof(CommandBuffer.DispatchMeshIndirect), indirectBuffer, offsetInBytes, ValidationConstants.IndirectDispatchMeshArgsSizeInBytes, dispatchCount);
    }

    private bool ValidateIndirectBuffer(string commandName, Buffer indirectBuffer, uint offsetInBytes, uint argsSizeInBytes, uint drawOrDispatchCount)
    {
        bool isValid = CheckResource($"{commandName}.indirectBuffer", indirectBuffer);

        if (isValid)
        {
            isValid &= CheckBufferUsage($"{commandName}.indirectBuffer", indirectBuffer, BufferUsages.Indirect);
            isValid &= CheckBufferRange($"{commandName}.indirectBuffer", indirectBuffer, offsetInBytes, (ulong)argsSizeInBytes * drawOrDispatchCount, allowZeroSize: true);
        }

        return isValid;
    }

    internal bool ValidateBeginQuery(QueryHeap queryHeap, uint index)
    {
        bool isValid = CheckQuery("BeginQuery", queryHeap, index);

        if (isValid && queryHeap.Desc.Type is QueryType.Timestamp)
        {
            ReportError(string.Format(ValidationMessages.MustNotBeTimestampQuery, "BeginQuery"));
            isValid = false;
        }

        return isValid;
    }

    internal bool ValidateEndQuery(QueryHeap queryHeap, uint index)
    {
        bool isValid = CheckQuery("EndQuery", queryHeap, index);

        if (isValid && queryHeap.Desc.Type is QueryType.Timestamp)
        {
            ReportError(string.Format(ValidationMessages.MustNotBeTimestampQuery, "EndQuery"));
            isValid = false;
        }

        return isValid;
    }

    internal bool ValidateWriteTimestamp(QueryHeap queryHeap, uint index)
    {
        bool isValid = CheckQuery("WriteTimestamp", queryHeap, index);

        if (isValid && queryHeap.Desc.Type is not QueryType.Timestamp)
        {
            ReportError(string.Format(ValidationMessages.MustBeTimestampQuery, "WriteTimestamp"));
            isValid = false;
        }

        return isValid;
    }

    internal bool ValidateDebugLabel(string commandName, string? label)
    {
        return CheckStringNotWhitespace($"{commandName}.label", label);
    }

    #endregion

    #region Primitive Checks

    private bool CheckResource<T>(string name, T? resource) where T : GraphicsResource
    {
        if (resource is null)
        {
            ReportError(string.Format(ValidationMessages.MustNotBeNull, name));

            return false;
        }

        if (resource.IsDisposed)
        {
            ReportError(string.Format(ValidationMessages.MustNotBeDisposed, name));

            return false;
        }

        return true;
    }

    private bool CheckArrayNotEmpty<T>(string name, T[]? array)
    {
        if (array is null || array.Length is 0)
        {
            ReportError(string.Format(ValidationMessages.MustNotBeNullOrEmpty, name));

            return false;
        }

        return true;
    }

    private bool CheckStringNotWhitespace(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ReportError(string.Format(ValidationMessages.MustNotBeNullOrWhitespace, name));

            return false;
        }

        return true;
    }

    private bool CheckEnum<T>(string name, T value) where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            ReportError(string.Format(ValidationMessages.HasInvalidValue, name, value));

            return false;
        }

        return true;
    }

    private bool CheckFlags<T>(string name, T value) where T : struct, Enum
    {
        ulong valueBits = unchecked((ulong)Convert.ToInt64(value));
        ulong validBits = 0;

        foreach (T definedValue in Enum.GetValues<T>())
        {
            validBits |= unchecked((ulong)Convert.ToInt64(definedValue));
        }

        if ((valueBits & ~validBits) is not 0)
        {
            ReportError(string.Format(ValidationMessages.HasInvalidValue, name, value));

            return false;
        }

        return true;
    }

    private bool CheckGreaterThanZero<T>(string name, T value) where T : struct, INumber<T>
    {
        if (value <= T.Zero)
        {
            ReportError(string.Format(ValidationMessages.MustBeGreaterThanZero, name));

            return false;
        }

        return true;
    }

    private bool CheckFinite(string name, float value)
    {
        if (!float.IsFinite(value))
        {
            ReportError(string.Format(ValidationMessages.MustBeFinite, name));

            return false;
        }

        return true;
    }

    private bool CheckSameValue<T>(string name, T first, T second)
    {
        if (!EqualityComparer<T>.Default.Equals(first, second))
        {
            ReportError(string.Format(ValidationMessages.MustHaveSameValue, name, first, second));

            return false;
        }

        return true;
    }

    #endregion

    #region Buffer Checks

    private bool CheckBufferData(string name, BufferData data)
    {
        bool isValid = true;

        if (data.Pointer == 0)
        {
            ReportError(string.Format(ValidationMessages.MustNotBeZero, $"{name}.Pointer"));
            isValid = false;
        }

        isValid &= CheckGreaterThanZero($"{name}.SizeInBytes", data.SizeInBytes);

        return isValid;
    }

    private bool CheckBufferUsage(string name, Buffer buffer, BufferUsages requiredUsage)
    {
        if (!buffer.Desc.Usages.HasFlag(requiredUsage))
        {
            ReportError(string.Format(ValidationMessages.MustHaveUsage, name, requiredUsage));

            return false;
        }

        return true;
    }

    private bool CheckBufferOffset(string name, Buffer buffer, uint offsetInBytes)
    {
        if (offsetInBytes > buffer.Desc.SizeInBytes)
        {
            ReportError(string.Format(ValidationMessages.MustBeWithinResourceBounds, $"{name}.OffsetInBytes", name));

            return false;
        }

        return true;
    }

    private bool CheckBufferRange(string name, Buffer buffer, uint offsetInBytes, ulong sizeInBytes, bool allowZeroSize = false)
    {
        bool isValid = CheckBufferOffset(name, buffer, offsetInBytes);

        if (!allowZeroSize && sizeInBytes is 0)
        {
            ReportError(string.Format(ValidationMessages.MustBeGreaterThanZero, $"{name}.SizeInBytes"));
            isValid = false;
        }

        if ((ulong)offsetInBytes + sizeInBytes > buffer.Desc.SizeInBytes)
        {
            ReportError(string.Format(ValidationMessages.MustBeWithinResourceBounds, name, "the buffer"));
            isValid = false;
        }

        return isValid;
    }

    #endregion

    #region Texture Checks

    private bool CheckTextureData(string name, TextureData data, PixelFormat format, Extent3D extent)
    {
        bool isValid = true;

        if (data.Pointer == 0)
        {
            ReportError(string.Format(ValidationMessages.MustNotBeZero, $"{name}.Pointer"));
            isValid = false;
        }

        isValid &= CheckTextureDataLayout($"{name}.Layout", data.Layout, format, extent);

        return isValid;
    }

    private bool CheckTextureDataLayout(string name, TextureDataLayout layout, PixelFormat format, Extent3D extent)
    {
        bool isValid = true;

        isValid &= CheckGreaterThanZero($"{name}.SizeInBytes", layout.SizeInBytes);
        isValid &= CheckGreaterThanZero($"{name}.RowStrideInBytes", layout.RowStrideInBytes);
        isValid &= CheckGreaterThanZero($"{name}.SliceStrideInBytes", layout.SliceStrideInBytes);

        if (!CheckTextureExtent($"{name}.Extent", extent))
        {
            return false;
        }

        uint minRowStrideInBytes = ZenithHelper.RowStrideInBytes(format, extent.Width, extent.Height);

        if (minRowStrideInBytes is 0)
        {
            return isValid;
        }

        if (layout.RowStrideInBytes < minRowStrideInBytes)
        {
            ReportError(string.Format(ValidationMessages.MustBeGreaterThanOrEqualTo, $"{name}.RowStrideInBytes", minRowStrideInBytes));
            isValid = false;
        }

        (_, _, _, uint blocksHigh) = ZenithHelper.BlockLayout(format, extent.Width, extent.Height);
        ulong minSliceStrideInBytes = (ulong)layout.RowStrideInBytes * blocksHigh;

        if (layout.SliceStrideInBytes < minSliceStrideInBytes)
        {
            ReportError(string.Format(ValidationMessages.MustBeGreaterThanOrEqualTo, $"{name}.SliceStrideInBytes", minSliceStrideInBytes));
            isValid = false;
        }

        ulong minSizeInBytes = extent.Depth is 0 ? 0 : ((ulong)layout.SliceStrideInBytes * (extent.Depth - 1)) + minSliceStrideInBytes;

        if (layout.SizeInBytes < minSizeInBytes)
        {
            ReportError(string.Format(ValidationMessages.MustBeGreaterThanOrEqualTo, $"{name}.SizeInBytes", minSizeInBytes));
            isValid = false;
        }

        return isValid;
    }

    private bool CheckTextureUsage(string name, Texture texture, TextureUsages requiredUsage)
    {
        if (!texture.Desc.Usages.HasFlag(requiredUsage))
        {
            ReportError(string.Format(ValidationMessages.MustHaveUsage, name, requiredUsage));

            return false;
        }

        return true;
    }

    private bool CheckTextureSubresource(string name, Texture texture, TextureSubresource subresource)
    {
        bool isValid = true;

        if (subresource.MipLevel >= texture.Desc.MipLevels)
        {
            ReportError(string.Format(ValidationMessages.MustBeLessThan, $"{name}.MipLevel", "TextureDesc.MipLevels"));
            isValid = false;
        }

        if (subresource.ArrayLayer >= texture.Desc.ArrayLayers)
        {
            ReportError(string.Format(ValidationMessages.MustBeLessThan, $"{name}.ArrayLayer", "TextureDesc.ArrayLayers"));
            isValid = false;
        }

        return isValid;
    }

    private bool CheckTextureExtent(string name, Extent3D extent)
    {
        if (extent.Width is 0 || extent.Height is 0 || extent.Depth is 0)
        {
            ReportError(string.Format(ValidationMessages.MustBeGreaterThanZero, $"{name} dimensions (Width, Height, Depth)"));

            return false;
        }

        return true;
    }

    private bool CheckTextureRange(string name, Texture texture, TextureSubresource subresource, Offset3D offset, Extent3D extent)
    {
        bool isValid = CheckTextureSubresource($"{name}.Subresource", texture, subresource);
        isValid &= CheckTextureExtent($"{name}.Extent", extent);

        if (!isValid)
        {
            return false;
        }

        ZenithHelper.MipDimensions(texture.Desc.Width, texture.Desc.Height, texture.Desc.Depth, subresource.MipLevel, out uint mipWidth, out uint mipHeight, out uint mipDepth);

        if ((ulong)offset.X + extent.Width > mipWidth)
        {
            ReportError(string.Format(ValidationMessages.MustBeWithinResourceBounds, $"{name}.X range", "the texture subresource width"));
            isValid = false;
        }

        if ((ulong)offset.Y + extent.Height > mipHeight)
        {
            ReportError(string.Format(ValidationMessages.MustBeWithinResourceBounds, $"{name}.Y range", "the texture subresource height"));
            isValid = false;
        }

        if ((ulong)offset.Z + extent.Depth > mipDepth)
        {
            ReportError(string.Format(ValidationMessages.MustBeWithinResourceBounds, $"{name}.Z range", "the texture subresource depth"));
            isValid = false;
        }

        return isValid;
    }

    private bool CheckSameMipExtent(string name, Texture first, TextureSubresource firstSubresource, Texture second, TextureSubresource secondSubresource)
    {
        if (!CheckTextureSubresource($"{name}.firstSubresource", first, firstSubresource) || !CheckTextureSubresource($"{name}.secondSubresource", second, secondSubresource))
        {
            return false;
        }

        ZenithHelper.MipDimensions(first.Desc.Width, first.Desc.Height, first.Desc.Depth, firstSubresource.MipLevel, out uint firstWidth, out uint firstHeight, out uint firstDepth);
        ZenithHelper.MipDimensions(second.Desc.Width, second.Desc.Height, second.Desc.Depth, secondSubresource.MipLevel, out uint secondWidth, out uint secondHeight, out uint secondDepth);

        bool isValid = true;
        isValid &= CheckSameValue($"{name}.Width", firstWidth, secondWidth);
        isValid &= CheckSameValue($"{name}.Height", firstHeight, secondHeight);
        isValid &= CheckSameValue($"{name}.Depth", firstDepth, secondDepth);

        return isValid;
    }

    private bool CheckColorFormat(string name, PixelFormat format)
    {
        if (ZenithHelper.HasDepth(format) || ZenithHelper.HasStencil(format))
        {
            ReportError(string.Format(ValidationMessages.MustNotBeDepthStencilFormat, name));

            return false;
        }

        return true;
    }

    private bool CheckDepthStencilFormat(string name, PixelFormat format)
    {
        if (!ZenithHelper.HasDepth(format) && !ZenithHelper.HasStencil(format))
        {
            ReportError(string.Format(ValidationMessages.MustBeDepthStencilFormat, name));

            return false;
        }

        return true;
    }

    #endregion

    #region Render Pass Checks

    private bool CheckColorAttachment(string name, ColorAttachment attachment, ref bool hasExtent, ref uint renderWidth, ref uint renderHeight)
    {
        bool hasTexture = CheckResource($"{name}.Texture", attachment.Texture);
        bool isValid = hasTexture;

        isValid &= CheckEnum($"{name}.LoadOp", attachment.LoadOp);
        isValid &= CheckEnum($"{name}.StoreOp", attachment.StoreOp);

        if (hasTexture)
        {
            isValid &= CheckTextureUsage($"{name}.Texture", attachment.Texture, TextureUsages.ColorAttachment);
            isValid &= CheckColorFormat($"{name}.Texture.Desc.Format", attachment.Texture.Desc.Format);

            if (CheckTextureSubresource($"{name}.Subresource", attachment.Texture, attachment.Subresource))
            {
                isValid &= CheckRenderPassExtent(name, attachment.Texture, attachment.Subresource, ref hasExtent, ref renderWidth, ref renderHeight);
            }
            else
            {
                isValid = false;
            }
        }

        if (attachment.ResolveTexture is { } resolveTexture)
        {
            bool hasResolveTexture = CheckResource($"{name}.ResolveTexture", resolveTexture);
            isValid &= hasResolveTexture;

            if (hasResolveTexture)
            {
                isValid &= CheckTextureUsage($"{name}.ResolveTexture", resolveTexture, TextureUsages.ColorAttachment);
                isValid &= CheckColorFormat($"{name}.ResolveTexture.Desc.Format", resolveTexture.Desc.Format);

                if (hasTexture)
                {
                    isValid &= CheckSameValue($"{name}.ResolveTexture.Desc.Format", attachment.Texture.Desc.Format, resolveTexture.Desc.Format);

                    if (attachment.Texture.Desc.SampleCount is SampleCount.Count1)
                    {
                        ReportError(string.Format(ValidationMessages.MustBeMultisampled, $"{name}.Texture"));
                        isValid = false;
                    }
                }

                if (resolveTexture.Desc.SampleCount is not SampleCount.Count1)
                {
                    ReportError(string.Format(ValidationMessages.MustBeSingleSampled, $"{name}.ResolveTexture"));
                    isValid = false;
                }

                if (CheckTextureSubresource($"{name}.ResolveSubresource", resolveTexture, attachment.ResolveSubresource))
                {
                    isValid &= CheckRenderPassExtent($"{name}.ResolveTexture", resolveTexture, attachment.ResolveSubresource, ref hasExtent, ref renderWidth, ref renderHeight);
                }
                else
                {
                    isValid = false;
                }
            }
        }

        return isValid;
    }

    private bool CheckDepthStencilAttachment(string name, DepthStencilAttachment attachment, ref bool hasExtent, ref uint renderWidth, ref uint renderHeight)
    {
        bool hasTexture = CheckResource($"{name}.Texture", attachment.Texture);
        bool isValid = hasTexture;

        isValid &= CheckEnum($"{name}.DepthLoadOp", attachment.DepthLoadOp);
        isValid &= CheckEnum($"{name}.DepthStoreOp", attachment.DepthStoreOp);
        isValid &= CheckEnum($"{name}.StencilLoadOp", attachment.StencilLoadOp);
        isValid &= CheckEnum($"{name}.StencilStoreOp", attachment.StencilStoreOp);

        if (attachment.ClearDepth is < 0.0f or > 1.0f)
        {
            ReportError(string.Format(ValidationMessages.MustBeBetween, $"{name}.ClearDepth", 0.0f, 1.0f));
            isValid = false;
        }

        if (hasTexture)
        {
            isValid &= CheckTextureUsage($"{name}.Texture", attachment.Texture, TextureUsages.DepthStencil);
            isValid &= CheckDepthStencilFormat($"{name}.Texture.Desc.Format", attachment.Texture.Desc.Format);

            if (CheckTextureSubresource($"{name}.Subresource", attachment.Texture, attachment.Subresource))
            {
                isValid &= CheckRenderPassExtent(name, attachment.Texture, attachment.Subresource, ref hasExtent, ref renderWidth, ref renderHeight);
            }
            else
            {
                isValid = false;
            }
        }

        return isValid;
    }

    private bool CheckRenderPassExtent(string name, Texture texture, TextureSubresource subresource, ref bool hasExtent, ref uint renderWidth, ref uint renderHeight)
    {
        ZenithHelper.MipDimensions(texture.Desc.Width, texture.Desc.Height, texture.Desc.Depth, subresource.MipLevel, out uint width, out uint height, out _);

        if (!hasExtent)
        {
            renderWidth = width;
            renderHeight = height;
            hasExtent = true;

            return true;
        }

        bool isValid = true;

        if (width != renderWidth)
        {
            ReportError(string.Format(ValidationMessages.MustHaveSameValue, $"{name}.Width", width, renderWidth));
            isValid = false;
        }

        if (height != renderHeight)
        {
            ReportError(string.Format(ValidationMessages.MustHaveSameValue, $"{name}.Height", height, renderHeight));
            isValid = false;
        }

        return isValid;
    }

    #endregion

    #region Pipeline State Checks

    private void CheckRenderState(string name, RenderState renderState)
    {
        CheckRasterizerState($"{name}.RasterizerState", renderState.RasterizerState);
        CheckDepthStencilState($"{name}.DepthStencilState", renderState.DepthStencilState);
        CheckBlendState($"{name}.BlendState", renderState.BlendState);
    }

    private void CheckRasterizerState(string name, RasterizerState rasterizerState)
    {
        CheckEnum($"{name}.FillMode", rasterizerState.FillMode);
        CheckEnum($"{name}.CullMode", rasterizerState.CullMode);
        CheckEnum($"{name}.FrontFace", rasterizerState.FrontFace);
    }

    private void CheckDepthStencilState(string name, DepthStencilState depthStencilState)
    {
        CheckEnum($"{name}.DepthCompareOp", depthStencilState.DepthCompareOp);
        CheckStencilFaceState($"{name}.FrontFace", depthStencilState.FrontFace);
        CheckStencilFaceState($"{name}.BackFace", depthStencilState.BackFace);
    }

    private void CheckBlendState(string name, BlendState blendState)
    {
        CheckColorAttachmentBlendState($"{name}.ColorAttachment0", blendState.ColorAttachment0);
        CheckColorAttachmentBlendState($"{name}.ColorAttachment1", blendState.ColorAttachment1);
        CheckColorAttachmentBlendState($"{name}.ColorAttachment2", blendState.ColorAttachment2);
        CheckColorAttachmentBlendState($"{name}.ColorAttachment3", blendState.ColorAttachment3);
        CheckColorAttachmentBlendState($"{name}.ColorAttachment4", blendState.ColorAttachment4);
        CheckColorAttachmentBlendState($"{name}.ColorAttachment5", blendState.ColorAttachment5);
        CheckColorAttachmentBlendState($"{name}.ColorAttachment6", blendState.ColorAttachment6);
        CheckColorAttachmentBlendState($"{name}.ColorAttachment7", blendState.ColorAttachment7);
    }

    private void CheckStencilFaceState(string name, StencilFaceState faceState)
    {
        CheckEnum($"{name}.FailOp", faceState.FailOp);
        CheckEnum($"{name}.DepthFailOp", faceState.DepthFailOp);
        CheckEnum($"{name}.PassOp", faceState.PassOp);
        CheckEnum($"{name}.CompareOp", faceState.CompareOp);
    }

    private void CheckColorAttachmentBlendState(string name, ColorAttachmentBlendState blendState)
    {
        CheckEnum($"{name}.SrcRgbFactor", blendState.SrcRgbFactor);
        CheckEnum($"{name}.DstRgbFactor", blendState.DstRgbFactor);
        CheckEnum($"{name}.RgbOp", blendState.RgbOp);
        CheckEnum($"{name}.SrcAlphaFactor", blendState.SrcAlphaFactor);
        CheckEnum($"{name}.DstAlphaFactor", blendState.DstAlphaFactor);
        CheckEnum($"{name}.AlphaOp", blendState.AlphaOp);
        CheckFlags($"{name}.ColorWrites", blendState.ColorWrites);
    }

    private void CheckAttachmentFormats(string name, AttachmentFormats attachmentFormats)
    {
        if (attachmentFormats.ColorFormats is null)
        {
            ReportError(string.Format(ValidationMessages.MustNotBeNull, $"{name}.ColorFormats"));

            return;
        }

        for (int index = 0; index < attachmentFormats.ColorFormats.Length; index++)
        {
            CheckEnum($"{name}.ColorFormats[{index}]", attachmentFormats.ColorFormats[index]);
        }

        if (attachmentFormats.DepthStencilFormat is { } depthStencilFormat)
        {
            CheckEnum($"{name}.DepthStencilFormat", depthStencilFormat);
        }

        CheckEnum($"{name}.SampleCount", attachmentFormats.SampleCount);

        if (attachmentFormats.ColorFormats.Length is 0 && attachmentFormats.DepthStencilFormat is null)
        {
            ReportWarning(string.Format(ValidationMessages.HasNoAttachments, name));
        }
    }

    private void CheckInputLayout(string name, InputLayout inputLayout)
    {
        if (CheckArrayNotEmpty($"{name}.InputElements", inputLayout.InputElements))
        {
            for (int index = 0; index < inputLayout.InputElements.Length; index++)
            {
                CheckInputElement($"{name}.InputElements[{index}]", inputLayout.InputElements[index]);
            }
        }

        CheckGreaterThanZero($"{name}.StrideInBytes", inputLayout.StrideInBytes);
    }

    private void CheckInputElement(string name, InputElement inputElement)
    {
        CheckEnum($"{name}.Format", inputElement.Format);
        CheckEnum($"{name}.Semantic", inputElement.Semantic);
    }

    #endregion

    #region Surface / Query / Ray Tracing Checks

    private void CheckSurface(string name, Surface surface)
    {
        if (!CheckEnum($"{name}.Type", surface.Type))
        {
            return;
        }

        if (!ExpectedSurfaceHandleCount.TryGetValue(surface.Type, out int expectedHandleCount))
        {
            ReportError(string.Format(ValidationMessages.HasUnsupportedSurfaceType, name, surface.Type));

            return;
        }

        if (surface.NativeHandles is null)
        {
            ReportError(string.Format(ValidationMessages.MustNotBeNull, $"{name}.NativeHandles"));

            return;
        }

        if (surface.NativeHandles.Length != expectedHandleCount)
        {
            ReportError(string.Format(ValidationMessages.MustHaveExactlyNHandles, $"{name}.NativeHandles", expectedHandleCount, surface.Type));

            return;
        }

        for (int index = 0; index < surface.NativeHandles.Length; index++)
        {
            if (surface.NativeHandles[index] is 0)
            {
                if (expectedHandleCount is 1)
                {
                    ReportError(string.Format(ValidationMessages.MustBeValidHandle, $"{name}.NativeHandles[0]", surface.Type));
                }
                else
                {
                    ReportError(string.Format(ValidationMessages.MustBeValidHandles, $"{name}.NativeHandles", surface.Type));
                }

                return;
            }
        }

        CheckGreaterThanZero($"{name}.Width", surface.Width);
        CheckGreaterThanZero($"{name}.Height", surface.Height);
    }

    private bool CheckQuery(string commandName, QueryHeap queryHeap, uint index)
    {
        bool isValid = CheckResource($"{commandName}.queryHeap", queryHeap);

        if (isValid)
        {
            isValid &= CheckEnum($"{commandName}.queryHeap.Desc.Type", queryHeap.Desc.Type);

            if (index >= queryHeap.Desc.Count)
            {
                ReportError(string.Format(ValidationMessages.MustBeLessThan, $"{commandName}.index", "QueryHeapDesc.Count"));
                isValid = false;
            }
        }

        return isValid;
    }

    private void CheckRayTracingGeometry(string name, RayTracingGeometry geometry)
    {
        if (!CheckEnum($"{name}.Type", geometry.Type))
        {
            return;
        }

        if (geometry.Type is RayTracingGeometryType.Triangle)
        {
            CheckRayTracingTriangleGeometry($"{name}.TriangleGeometry", geometry.TriangleGeometry);
        }

        if (geometry.Type is RayTracingGeometryType.Aabb)
        {
            CheckRayTracingAabbGeometry($"{name}.AabbGeometry", geometry.AabbGeometry);
        }
    }

    private void CheckRayTracingTriangleGeometry(string name, RayTracingTriangleGeometry triangleGeometry)
    {
        bool hasVertexBuffer = CheckResource($"{name}.VertexBuffer", triangleGeometry.VertexBuffer);
        bool hasIndexBuffer = triangleGeometry.IndexBuffer is not null && CheckResource($"{name}.IndexBuffer", triangleGeometry.IndexBuffer);

        CheckEnum($"{name}.VertexFormat", triangleGeometry.VertexFormat);
        bool hasVertexCount = CheckGreaterThanZero($"{name}.VertexCount", triangleGeometry.VertexCount);
        bool hasVertexStride = CheckGreaterThanZero($"{name}.VertexStrideInBytes", triangleGeometry.VertexStrideInBytes);

        if (hasVertexBuffer && hasVertexCount && hasVertexStride && triangleGeometry.VertexOffsetInBytes + (triangleGeometry.VertexCount * triangleGeometry.VertexStrideInBytes) > triangleGeometry.VertexBuffer.Desc.SizeInBytes)
        {
            ReportError(string.Format(ValidationMessages.MustBeWithinBounds, $"{name}.VertexCount", "the vertex buffer"));
        }

        if (triangleGeometry.IndexBuffer is null)
        {
            return;
        }

        CheckEnum($"{name}.IndexFormat", triangleGeometry.IndexFormat);
        bool hasIndexCount = CheckGreaterThanZero($"{name}.IndexCount", triangleGeometry.IndexCount);

        uint indexSizeInBytes = triangleGeometry.IndexFormat switch
        {
            IndexFormat.UInt16 => ValidationConstants.IndexSizeUInt16,
            IndexFormat.UInt32 => ValidationConstants.IndexSizeUInt32,
            _ => 0
        };

        if (hasIndexBuffer && hasIndexCount && indexSizeInBytes is not 0 && triangleGeometry.IndexOffsetInBytes + (triangleGeometry.IndexCount * indexSizeInBytes) > triangleGeometry.IndexBuffer.Desc.SizeInBytes)
        {
            ReportError(string.Format(ValidationMessages.MustBeWithinBounds, $"{name}.IndexCount", "the index buffer"));
        }
    }

    private void CheckRayTracingAabbGeometry(string name, RayTracingAabbGeometry aabbGeometry)
    {
        bool hasBuffer = CheckResource($"{name}.Buffer", aabbGeometry.Buffer);

        bool hasCount = CheckGreaterThanZero($"{name}.Count", aabbGeometry.Count);
        bool hasStride = CheckGreaterThanZero($"{name}.StrideInBytes", aabbGeometry.StrideInBytes);

        if (hasBuffer && hasCount && hasStride && aabbGeometry.OffsetInBytes + (aabbGeometry.Count * aabbGeometry.StrideInBytes) > aabbGeometry.Buffer.Desc.SizeInBytes)
        {
            ReportError(string.Format(ValidationMessages.MustBeWithinBounds, $"{name}.Count", "the Aabb geometry buffer"));
        }
    }

    private void CheckRayTracingInstance(string name, RayTracingInstance instance)
    {
        CheckResource($"{name}.AccelerationStructure", instance.AccelerationStructure);

        if (instance.InstanceId > ValidationConstants.MaxRayTracingInstanceId)
        {
            ReportError(string.Format(ValidationMessages.MustBeLessThanOrEqualTo, $"{name}.InstanceId", ValidationConstants.MaxRayTracingInstanceId));
        }

        CheckFlags($"{name}.Flags", instance.Flags);
    }

    #endregion
}

file static class ValidationConstants
{
    public const int CubeMapFaceCount = 6;

    public const int MaxAnisotropy = 16;

    public const int MaxColorAttachments = 8;

    public const uint IndirectDrawArgsSizeInBytes = 16;

    public const uint IndirectDrawIndexedArgsSizeInBytes = 20;

    public const uint IndirectDispatchArgsSizeInBytes = 12;

    public const uint IndirectDispatchMeshArgsSizeInBytes = 12;

    public const int IndexSizeUInt16 = 2;

    public const int IndexSizeUInt32 = 4;

    public const int MaxRayTracingInstanceId = 16777215;
}

file static class ValidationMessages
{
    public const string MustNotBeNull = "{0} must not be null.";

    public const string MustNotBeZero = "{0} must not be zero.";

    public const string MustHaveExactlyNHandles = "{0} must have exactly {1} handles for {2}.";

    public const string MustBeValidHandle = "{0} must be a valid handle for {1}.";

    public const string MustBeValidHandles = "{0} must be valid handles for {1}.";

    public const string HasUnsupportedSurfaceType = "{0} has unsupported SurfaceType '{1}'.";

    public const string HasInvalidValue = "{0} has an invalid value '{1}'.";

    public const string HasNoAttachments = "{0} has no attachments.";

    public const string MustNotBeDisposed = "{0} must not be disposed.";

    public const string MustBeLessThan = "{0} must be less than {1}.";

    public const string MustNotBeNullOrEmpty = "{0} must not be null or empty.";

    public const string MustNotBeNullOrWhitespace = "{0} must not be null or whitespace.";

    public const string MustBeGreaterThanZero = "{0} must be greater than zero.";

    public const string IsZeroWarning = "{0} is zero, which may be valid for some {1} but could indicate an issue.";

    public const string IsSetToNoneWarning = "{0} is set to None, which may be valid but could indicate an issue.";

    public const string MustBeWithinBounds = "{0} must be greater than zero and within the bounds of {1}.";

    public const string MustBeLessThanOrEqualTo = "{0} must be less than or equal to {1}.";

    public const string MustBeGreaterThanOrEqualTo = "{0} must be greater than or equal to {1}.";

    public const string MustBeEqualTo = "{0} must be equal to {1}.";

    public const string MustBeBetween = "{0} must be between {1} and {2}.";

    public const string MustBeAMultipleOf = "{0} must be a multiple of {1}.";

    public const string MustDescribeACompleteCube = "{0} must describe a complete cube view.";

    public const string MustBeOneOf = "{0} must be one of: {1}.";

    public const string MustHaveFlag = "{0} must have the flag '{1}' set.";

    public const string MustHaveUsage = "{0} must have usage flag '{1}'.";

    public const string MustHaveSameValue = "{0} must match. First value: '{1}', second value: '{2}'.";

    public const string MustBeWithinResourceBounds = "{0} must be within the bounds of {1}.";

    public const string MustNotBeDepthStencilFormat = "{0} must not use a depth/stencil format.";

    public const string MustBeDepthStencilFormat = "{0} must use a depth/stencil format.";

    public const string MustBeSingleSampled = "{0} must use SampleCount.Count1.";

    public const string MustBeMultisampled = "{0} must use a sample count greater than SampleCount.Count1.";

    public const string MustBeFinite = "{0} must be finite.";

    public const string MustHaveCurrentPipeline = "{0} requires a pipeline to be set.";

    public const string MustHaveCurrentPipelineType = "{0} requires current pipeline type {1}, but current pipeline is {2}.";

    public const string MustNotBeTimestampQuery = "{0} cannot be used with QueryType.Timestamp.";

    public const string MustBeTimestampQuery = "{0} requires QueryType.Timestamp.";

    public const string InstanceCountMustRemainSame = "When updating a TopLevelAccelerationStructure, the number of instances must remain the same.";

    public const string UsagesIncompatibleWithAccess = "{0} contains flags '{1}' that require GPU read-write access and cannot be combined with BufferAccess.{2}.";

    public const string MustHaveNonZeroConstantsSize = "{0} reports a zero size for {1}; the constants layout has no payload for the current backend.";

    public const string MustBeCpuAccessible = "{0} must use BufferAccess.CpuReadOnly or BufferAccess.CpuWriteOnly to be mappable.";
}
