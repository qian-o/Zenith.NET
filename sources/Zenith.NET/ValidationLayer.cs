using System.Numerics;

namespace Zenith.NET;

public abstract partial class ValidationLayer(GraphicsContext context) : GraphicsResource(context)
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
