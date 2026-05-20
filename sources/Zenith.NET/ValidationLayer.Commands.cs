using System.Numerics;

namespace Zenith.NET;

partial class ValidationLayer
{
    #region Resources

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

    #region SwapChain

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

    #region Transitions

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

    #region Transfer

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

    #region Render Pass / Viewport

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

    #region Pipeline State

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

    internal bool ValidateSetConstants<T>(T data) where T : unmanaged, IConstantsLayout<T>
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

    #region Indirect / Queries / Debug

    internal bool ValidateIndirectBuffer(string commandName, Buffer indirectBuffer, uint offsetInBytes, uint argsSizeInBytes, uint drawOrDispatchCount)
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
}
