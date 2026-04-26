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

    internal void ValidateDesc(SwapChainDesc desc)
    {
        CheckSurface("SwapChainDesc.Surface", desc.Surface);

        CheckEnum("SwapChainDesc.ColorTargetFormat", desc.ColorTargetFormat);

        if (desc.DepthStencilTargetFormat is { } format)
        {
            CheckEnum("SwapChainDesc.DepthStencilTargetFormat", format);
        }
    }

    internal void ValidateDesc(BufferDesc desc)
    {
        CheckGreaterThanZero("BufferDesc.SizeInBytes", desc.SizeInBytes);

        if (desc.StrideInBytes is 0)
        {
            ReportWarning(string.Format(ValidationMessages.IsZeroWarning, "BufferDesc.StrideInBytes", "buffer types"));
        }

        if (desc.Flags is BufferUsageFlags.None)
        {
            ReportWarning(string.Format(ValidationMessages.IsSetToNoneWarning, "BufferDesc.Flags"));
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

        if (desc.Flags is TextureUsageFlags.None)
        {
            ReportWarning(string.Format(ValidationMessages.IsSetToNoneWarning, "TextureDesc.Flags"));
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
        CheckEnum("SamplerDesc.Filter", desc.Filter);
        CheckEnum("SamplerDesc.U", desc.U);
        CheckEnum("SamplerDesc.V", desc.V);
        CheckEnum("SamplerDesc.W", desc.W);
        CheckEnum("SamplerDesc.ComparisonFunc", desc.ComparisonFunc);

        if (desc.Filter is Filter.Anisotropic && desc.MaxAnisotropy is 0)
        {
            ReportError(string.Format(ValidationMessages.MustBeGreaterThanZero, "SamplerDesc.MaxAnisotropy"));
        }

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

    internal void ValidateDesc(ResourceTableDesc desc)
    {
        if (!CheckArrayNotEmpty("ResourceTableDesc.Bindings", desc.Bindings))
        {
            return;
        }

        for (int i = 0; i < desc.Bindings.Length; i++)
        {
            CheckResourceBinding($"ResourceTableDesc.Bindings[{i}]", desc.Bindings[i]);
        }
    }

    internal void ValidateDesc(ShaderDesc desc)
    {
        CheckArrayNotEmpty("ShaderDesc.ShaderBytes", desc.ShaderBytes);
        CheckStringNotWhitespace("ShaderDesc.EntryPoint", desc.EntryPoint);
        CheckEnum("ShaderDesc.Stage", desc.Stage);
    }

    internal void ValidateDesc(GraphicsPipelineDesc desc)
    {
        CheckRenderStates("GraphicsPipelineDesc.RenderStates", desc.RenderStates);

        CheckResource("GraphicsPipelineDesc.Vertex", desc.Vertex);
        CheckResource("GraphicsPipelineDesc.Pixel", desc.Pixel);

        CheckResourceBindings("GraphicsPipelineDesc.ResourceBindings", desc.ResourceBindings);

        if (desc.InputLayouts is null)
        {
            ReportError(string.Format(ValidationMessages.MustNotBeNull, "GraphicsPipelineDesc.InputLayouts"));
        }
        else
        {
            for (int i = 0; i < desc.InputLayouts.Length; i++)
            {
                CheckInputLayout($"GraphicsPipelineDesc.InputLayouts[{i}]", desc.InputLayouts[i]);
            }
        }

        CheckEnum("GraphicsPipelineDesc.PrimitiveTopology", desc.PrimitiveTopology);
        CheckOutput("GraphicsPipelineDesc.Output", desc.Output);
    }

    internal void ValidateDesc(ComputePipelineDesc desc)
    {
        CheckResource("ComputePipelineDesc.Compute", desc.Compute);

        CheckResourceBindings("ComputePipelineDesc.ResourceBindings", desc.ResourceBindings);

        if (desc.ThreadGroupSizeX is 0 || desc.ThreadGroupSizeY is 0 || desc.ThreadGroupSizeZ is 0)
        {
            ReportError(string.Format(ValidationMessages.MustBeGreaterThanZero, "ComputePipelineDesc thread group sizes (ThreadGroupSizeX, ThreadGroupSizeY, ThreadGroupSizeZ)"));
        }
    }

    internal void ValidateDesc(MeshShadingPipelineDesc desc)
    {
        CheckRenderStates("MeshShadingPipelineDesc.RenderStates", desc.RenderStates);

        if (desc.Amplification is not null)
        {
            CheckResource("MeshShadingPipelineDesc.Amplification", desc.Amplification);
        }

        CheckResource("MeshShadingPipelineDesc.Mesh", desc.Mesh);
        CheckResource("MeshShadingPipelineDesc.Pixel", desc.Pixel);

        CheckResourceBindings("MeshShadingPipelineDesc.ResourceBindings", desc.ResourceBindings);

        if (desc.PrimitiveTopology is not PrimitiveTopology.LineList and not PrimitiveTopology.TriangleList)
        {
            ReportError(string.Format(ValidationMessages.MustBeOneOf, "MeshShadingPipelineDesc.PrimitiveTopology", "LineList, TriangleList"));
        }

        CheckOutput("MeshShadingPipelineDesc.Output", desc.Output);

        if (desc.Amplification is not null && (desc.AmplificationThreadGroupSizeX is 0 || desc.AmplificationThreadGroupSizeY is 0 || desc.AmplificationThreadGroupSizeZ is 0))
        {
            ReportError(string.Format(ValidationMessages.MustBeGreaterThanZero, "MeshShadingPipelineDesc amplification thread group sizes (AmplificationThreadGroupSizeX, AmplificationThreadGroupSizeY, AmplificationThreadGroupSizeZ)"));
        }

        if (desc.MeshThreadGroupSizeX is 0 || desc.MeshThreadGroupSizeY is 0 || desc.MeshThreadGroupSizeZ is 0)
        {
            ReportError(string.Format(ValidationMessages.MustBeGreaterThanZero, "MeshShadingPipelineDesc mesh thread group sizes (MeshThreadGroupSizeX, MeshThreadGroupSizeY, MeshThreadGroupSizeZ)"));
        }
    }

    internal void ValidateDesc(QueryHeapDesc desc)
    {
        CheckEnum("QueryHeapDesc.Type", desc.Type);
        CheckGreaterThanZero("QueryHeapDesc.Count", desc.Count);
    }

    internal void ValidateDesc(BottomLevelAccelerationStructureDesc desc)
    {
        if (!CheckArrayNotEmpty("BottomLevelAccelerationStructureDesc.Geometries", desc.Geometries))
        {
            return;
        }

        for (int i = 0; i < desc.Geometries.Length; i++)
        {
            CheckRayTracingGeometry($"BottomLevelAccelerationStructureDesc.Geometries[{i}]", desc.Geometries[i]);
        }
    }

    internal void ValidateDesc(TopLevelAccelerationStructureDesc desc)
    {
        if (!CheckArrayNotEmpty("TopLevelAccelerationStructureDesc.Instances", desc.Instances))
        {
            return;
        }

        for (int i = 0; i < desc.Instances.Length; i++)
        {
            CheckRayTracingInstance($"TopLevelAccelerationStructureDesc.Instances[{i}]", desc.Instances[i]);
        }
    }

    internal void ValidateDesc(TopLevelAccelerationStructureDesc oldDesc, TopLevelAccelerationStructureDesc newDesc)
    {
        if (!oldDesc.Flags.HasFlag(AccelerationStructureBuildFlags.AllowUpdate))
        {
            ReportError(string.Format(ValidationMessages.MustHaveFlag, "TopLevelAccelerationStructureDesc.Flags", AccelerationStructureBuildFlags.AllowUpdate));
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

    private bool CheckGreaterThanZero<T>(string name, T value) where T : struct, INumber<T>
    {
        if (value <= T.Zero)
        {
            ReportError(string.Format(ValidationMessages.MustBeGreaterThanZero, name));

            return false;
        }

        return true;
    }

    private void CheckSurface(string name, Surface surface)
    {
        if (surface.Handles is null)
        {
            ReportError(string.Format(ValidationMessages.MustNotBeNull, $"{name}.Handles"));

            return;
        }

        if (!ExpectedSurfaceHandleCount.TryGetValue(surface.Type, out int expected))
        {
            ReportError(string.Format(ValidationMessages.HasUnsupportedSurfaceType, name, surface.Type));

            return;
        }

        if (surface.Handles.Length != expected)
        {
            ReportError(string.Format(ValidationMessages.MustHaveExactlyNHandles, $"{name}.Handles", expected, surface.Type));

            return;
        }

        for (int i = 0; i < surface.Handles.Length; i++)
        {
            if (surface.Handles[i] is 0)
            {
                if (expected is 1)
                {
                    ReportError(string.Format(ValidationMessages.MustBeValidHandle, $"{name}.Handles[0]", surface.Type));
                }
                else
                {
                    ReportError(string.Format(ValidationMessages.MustBeValidHandles, $"{name}.Handles", surface.Type));
                }

                return;
            }
        }
    }

    private void CheckResourceBindings(string name, ResourceBinding[]? bindings)
    {
        if (bindings is null)
        {
            ReportError(string.Format(ValidationMessages.MustNotBeNull, name));

            return;
        }

        for (int i = 0; i < bindings.Length; i++)
        {
            CheckResourceBinding($"{name}[{i}]", bindings[i]);
        }
    }

    private void CheckResourceBinding(string name, ResourceBinding binding)
    {
        CheckEnum($"{name}.Type", binding.Type);
        CheckGreaterThanZero($"{name}.Count", binding.Count);
    }

    private void CheckInputLayout(string name, InputLayout inputLayout)
    {
        if (!CheckArrayNotEmpty($"{name}.Elements", inputLayout.Elements))
        {
            return;
        }

        foreach (InputElement element in inputLayout.Elements)
        {
            CheckEnum($"{name}.Elements.Format", element.Format);
            CheckEnum($"{name}.Elements.Semantic", element.Semantic);
        }
    }

    private void CheckRenderStates(string name, RenderStates renderStates)
    {
        CheckEnum($"{name}.RasterizerState.FillMode", renderStates.RasterizerState.FillMode);
        CheckEnum($"{name}.RasterizerState.CullMode", renderStates.RasterizerState.CullMode);
        CheckEnum($"{name}.RasterizerState.FrontFace", renderStates.RasterizerState.FrontFace);

        CheckEnum($"{name}.DepthStencilState.DepthFunc", renderStates.DepthStencilState.DepthFunc);
        CheckDepthStencilStateOp($"{name}.DepthStencilState.FrontFace", renderStates.DepthStencilState.FrontFace);
        CheckDepthStencilStateOp($"{name}.DepthStencilState.BackFace", renderStates.DepthStencilState.BackFace);

        CheckBlendStateRenderTarget($"{name}.BlendState.RenderTarget0", renderStates.BlendState.RenderTarget0);
        CheckBlendStateRenderTarget($"{name}.BlendState.RenderTarget1", renderStates.BlendState.RenderTarget1);
        CheckBlendStateRenderTarget($"{name}.BlendState.RenderTarget2", renderStates.BlendState.RenderTarget2);
        CheckBlendStateRenderTarget($"{name}.BlendState.RenderTarget3", renderStates.BlendState.RenderTarget3);
        CheckBlendStateRenderTarget($"{name}.BlendState.RenderTarget4", renderStates.BlendState.RenderTarget4);
        CheckBlendStateRenderTarget($"{name}.BlendState.RenderTarget5", renderStates.BlendState.RenderTarget5);
        CheckBlendStateRenderTarget($"{name}.BlendState.RenderTarget6", renderStates.BlendState.RenderTarget6);
        CheckBlendStateRenderTarget($"{name}.BlendState.RenderTarget7", renderStates.BlendState.RenderTarget7);
    }

    private void CheckDepthStencilStateOp(string name, DepthStencilStateOp op)
    {
        CheckEnum($"{name}.StencilFailOp", op.StencilFailOp);
        CheckEnum($"{name}.StencilDepthFailOp", op.StencilDepthFailOp);
        CheckEnum($"{name}.StencilPassOp", op.StencilPassOp);
        CheckEnum($"{name}.StencilFunc", op.StencilFunc);
    }

    private void CheckBlendStateRenderTarget(string name, BlendStateRenderTarget renderTarget)
    {
        CheckEnum($"{name}.SrcBlend", renderTarget.SrcBlend);
        CheckEnum($"{name}.DestBlend", renderTarget.DestBlend);
        CheckEnum($"{name}.BlendOp", renderTarget.BlendOp);
        CheckEnum($"{name}.SrcBlendAlpha", renderTarget.SrcBlendAlpha);
        CheckEnum($"{name}.DestBlendAlpha", renderTarget.DestBlendAlpha);
        CheckEnum($"{name}.BlendOpAlpha", renderTarget.BlendOpAlpha);
    }

    private void CheckOutput(string name, Output output)
    {
        if (output.ColorAttachments is null)
        {
            ReportError(string.Format(ValidationMessages.MustNotBeNull, $"{name}.ColorAttachments"));

            return;
        }

        for (int i = 0; i < output.ColorAttachments.Length; i++)
        {
            CheckEnum($"{name}.ColorAttachments[{i}]", output.ColorAttachments[i]);
        }

        if (output.DepthStencilAttachment is { } format)
        {
            CheckEnum($"{name}.DepthStencilAttachment", format);
        }

        CheckEnum($"{name}.SampleCount", output.SampleCount);

        if (output.ColorAttachments.Length is 0 && output.DepthStencilAttachment is null)
        {
            ReportWarning(string.Format(ValidationMessages.HasNoAttachments, name));
        }
    }

    private void CheckRayTracingGeometry(string name, RayTracingGeometry geometry)
    {
        switch (geometry.Type)
        {
            case RayTracingGeometryType.Triangles:
                CheckRayTracingTriangles(name, geometry.Triangles);
                break;

            case RayTracingGeometryType.AABBs:
                CheckRayTracingAABBs(name, geometry.AABBs);
                break;

            default:
                ReportError(string.Format(ValidationMessages.HasInvalidValue, $"{name}.Type", geometry.Type));
                break;
        }
    }

    private void CheckRayTracingTriangles(string name, RayTracingTriangles triangles)
    {
        if (!CheckResource($"{name}.Triangles.VertexBuffer", triangles.VertexBuffer))
        {
            return;
        }

        CheckEnum($"{name}.Triangles.VertexFormat", triangles.VertexFormat);
        CheckGreaterThanZero($"{name}.Triangles.VertexCount", triangles.VertexCount);
        CheckGreaterThanZero($"{name}.Triangles.VertexStrideInBytes", triangles.VertexStrideInBytes);

        if (triangles.VertexOffsetInBytes + (triangles.VertexCount * triangles.VertexStrideInBytes) > triangles.VertexBuffer.Desc.SizeInBytes)
        {
            ReportError(string.Format(ValidationMessages.MustBeWithinBounds, $"{name}.Triangles.VertexCount", "the vertex buffer"));
        }

        if (triangles.IndexBuffer is null)
        {
            return;
        }

        if (!CheckResource($"{name}.Triangles.IndexBuffer", triangles.IndexBuffer))
        {
            return;
        }

        CheckEnum($"{name}.Triangles.IndexFormat", triangles.IndexFormat);
        CheckGreaterThanZero($"{name}.Triangles.IndexCount", triangles.IndexCount);

        uint indexSizeInBytes = triangles.IndexFormat switch
        {
            IndexFormat.UInt16 => ValidationConstants.IndexSizeUInt16,
            IndexFormat.UInt32 => ValidationConstants.IndexSizeUInt32,
            _ => 0
        };

        if (triangles.IndexOffsetInBytes + (triangles.IndexCount * indexSizeInBytes) > triangles.IndexBuffer.Desc.SizeInBytes)
        {
            ReportError(string.Format(ValidationMessages.MustBeWithinBounds, $"{name}.Triangles.IndexCount", "the index buffer"));
        }
    }

    private void CheckRayTracingAABBs(string name, RayTracingAABBs aabbs)
    {
        if (!CheckResource($"{name}.AABBs.Buffer", aabbs.Buffer))
        {
            return;
        }

        CheckGreaterThanZero($"{name}.AABBs.Count", aabbs.Count);
        CheckGreaterThanZero($"{name}.AABBs.StrideInBytes", aabbs.StrideInBytes);

        if (aabbs.OffsetInBytes + (aabbs.Count * aabbs.StrideInBytes) > aabbs.Buffer.Desc.SizeInBytes)
        {
            ReportError(string.Format(ValidationMessages.MustBeWithinBounds, $"{name}.AABBs.Count", "the AABBs buffer"));
        }
    }

    private void CheckRayTracingInstance(string name, RayTracingInstance instance)
    {
        if (!CheckResource($"{name}.AccelerationStructure", instance.AccelerationStructure))
        {
            return;
        }

        if (instance.ID > ValidationConstants.MaxRayTracingInstanceID)
        {
            ReportError(string.Format(ValidationMessages.MustBeLessThanOrEqualTo, $"{name}.ID", ValidationConstants.MaxRayTracingInstanceID));
        }
    }

    private void ReportError(string message)
    {
        Report(MessageSource.Framework, MessageSeverity.Error, message);
    }

    private void ReportWarning(string message)
    {
        Report(MessageSource.Framework, MessageSeverity.Warning, message);
    }
}

file static class ValidationConstants
{
    public const int CubeMapFaceCount = 6;

    public const int MaxAnisotropy = 16;

    public const int IndexSizeUInt16 = 2;

    public const int IndexSizeUInt32 = 4;

    public const int MaxRayTracingInstanceID = 16777215;
}

file static class ValidationMessages
{
    public const string MustNotBeNull = "{0} must not be null.";

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

    public const string MustBeEqualTo = "{0} must be equal to {1}.";

    public const string MustBeAMultipleOf = "{0} must be a multiple of {1}.";

    public const string MustDescribeACompleteCube = "{0} must describe a complete cube view.";

    public const string MustBeOneOf = "{0} must be one of: {1}.";

    public const string MustHaveFlag = "{0} must have the flag '{1}' set.";

    public const string InstanceCountMustRemainSame = "When updating a TopLevelAccelerationStructure, the number of instances must remain the same.";
}
