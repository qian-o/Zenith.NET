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

            BufferUsages forbidden = desc.Usages & GpuOnlyUsages;

            if (forbidden is not BufferUsages.None)
            {
                ReportError(string.Format(ValidationMessages.UsagesIncompatibleWithAccess, "BufferDesc.Usages", forbidden, desc.Access));
            }
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
        CheckEnum("SamplerDesc.MipmapFilter", desc.MipmapFilter);
        CheckEnum("SamplerDesc.AddressU", desc.AddressU);
        CheckEnum("SamplerDesc.AddressV", desc.AddressV);
        CheckEnum("SamplerDesc.AddressW", desc.AddressW);
        CheckEnum("SamplerDesc.CompareFunction", desc.CompareFunction);

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
        if (!CheckArrayNotEmpty("ResourceTableDesc.ResourceLayouts", desc.ResourceLayouts))
        {
            return;
        }

        for (int i = 0; i < desc.ResourceLayouts.Length; i++)
        {
            CheckResourceLayout($"ResourceTableDesc.ResourceLayouts[{i}]", desc.ResourceLayouts[i]);
        }
    }

    internal void ValidateDesc(ShaderDesc desc)
    {
        CheckArrayNotEmpty("ShaderDesc.Bytecode", desc.Bytecode);
        CheckStringNotWhitespace("ShaderDesc.EntryPoint", desc.EntryPoint);
        CheckEnum("ShaderDesc.Stage", desc.Stage);
    }

    internal void ValidateDesc(GraphicsPipelineDesc desc)
    {
        CheckRenderState("GraphicsPipelineDesc.RenderState", desc.RenderState);

        CheckResource("GraphicsPipelineDesc.VertexShader", desc.VertexShader);
        CheckResource("GraphicsPipelineDesc.FragmentShader", desc.FragmentShader);

        CheckResourceLayouts("GraphicsPipelineDesc.ResourceLayouts", desc.ResourceLayouts);

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
        CheckAttachmentFormats("GraphicsPipelineDesc.AttachmentFormats", desc.AttachmentFormats);
    }

    internal void ValidateDesc(ComputePipelineDesc desc)
    {
        CheckResource("ComputePipelineDesc.ComputeShader", desc.ComputeShader);

        CheckResourceLayouts("ComputePipelineDesc.ResourceLayouts", desc.ResourceLayouts);
    }

    internal void ValidateDesc(MeshShadingPipelineDesc desc)
    {
        CheckRenderState("MeshShadingPipelineDesc.RenderState", desc.RenderState);

        if (desc.TaskShader is not null)
        {
            CheckResource("MeshShadingPipelineDesc.TaskShader", desc.TaskShader);
        }

        CheckResource("MeshShadingPipelineDesc.MeshShader", desc.MeshShader);
        CheckResource("MeshShadingPipelineDesc.FragmentShader", desc.FragmentShader);

        CheckResourceLayouts("MeshShadingPipelineDesc.ResourceLayouts", desc.ResourceLayouts);

        if (desc.PrimitiveTopology is not PrimitiveTopology.LineList and not PrimitiveTopology.TriangleList)
        {
            ReportError(string.Format(ValidationMessages.MustBeOneOf, "MeshShadingPipelineDesc.PrimitiveTopology", "LineList, TriangleList"));
        }

        CheckAttachmentFormats("MeshShadingPipelineDesc.AttachmentFormats", desc.AttachmentFormats);
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
            for (int i = 0; i < desc.Geometries.Length; i++)
            {
                CheckRayTracingGeometry($"BottomLevelAccelerationStructureDesc.Geometries[{i}]", desc.Geometries[i]);
            }
        }

        CheckFlags("BottomLevelAccelerationStructureDesc.BuildFlags", desc.BuildFlags);
    }

    internal void ValidateDesc(TopLevelAccelerationStructureDesc desc)
    {
        if (CheckArrayNotEmpty("TopLevelAccelerationStructureDesc.Instances", desc.Instances))
        {
            for (int i = 0; i < desc.Instances.Length; i++)
            {
                CheckRayTracingInstance($"TopLevelAccelerationStructureDesc.Instances[{i}]", desc.Instances[i]);
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

    private void CheckSurface(string name, Surface surface)
    {
        if (!CheckEnum($"{name}.Type", surface.Type))
        {
            return;
        }

        if (!ExpectedSurfaceHandleCount.TryGetValue(surface.Type, out int expected))
        {
            ReportError(string.Format(ValidationMessages.HasUnsupportedSurfaceType, name, surface.Type));

            return;
        }

        if (surface.NativeHandles is null)
        {
            ReportError(string.Format(ValidationMessages.MustNotBeNull, $"{name}.NativeHandles"));

            return;
        }

        if (surface.NativeHandles.Length != expected)
        {
            ReportError(string.Format(ValidationMessages.MustHaveExactlyNHandles, $"{name}.NativeHandles", expected, surface.Type));

            return;
        }

        for (int i = 0; i < surface.NativeHandles.Length; i++)
        {
            if (surface.NativeHandles[i] is 0)
            {
                if (expected is 1)
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

    private void CheckResourceLayouts(string name, ResourceLayout[]? resourceLayouts)
    {
        if (resourceLayouts is null)
        {
            ReportError(string.Format(ValidationMessages.MustNotBeNull, name));

            return;
        }

        for (int i = 0; i < resourceLayouts.Length; i++)
        {
            CheckResourceLayout($"{name}[{i}]", resourceLayouts[i]);
        }
    }

    private void CheckResourceLayout(string name, ResourceLayout resourceLayout)
    {
        CheckEnum($"{name}.Type", resourceLayout.Type);
        CheckGreaterThanZero($"{name}.Count", resourceLayout.Count);
    }

    private void CheckInputLayout(string name, InputLayout inputLayout)
    {
        if (CheckArrayNotEmpty($"{name}.InputElements", inputLayout.InputElements))
        {
            for (int i = 0; i < inputLayout.InputElements.Length; i++)
            {
                CheckInputElement($"{name}.InputElements[{i}]", inputLayout.InputElements[i]);
            }
        }

        CheckGreaterThanZero($"{name}.StrideInBytes", inputLayout.StrideInBytes);
    }

    private void CheckInputElement(string name, InputElement inputElement)
    {
        CheckEnum($"{name}.Format", inputElement.Format);
        CheckEnum($"{name}.Semantic", inputElement.Semantic);
    }

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
        CheckEnum($"{name}.DepthCompareFunction", depthStencilState.DepthCompareFunction);
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

    private void CheckStencilFaceState(string name, StencilFaceState face)
    {
        CheckEnum($"{name}.FailOperation", face.FailOperation);
        CheckEnum($"{name}.DepthFailOperation", face.DepthFailOperation);
        CheckEnum($"{name}.PassOperation", face.PassOperation);
        CheckEnum($"{name}.CompareFunction", face.CompareFunction);
    }

    private void CheckColorAttachmentBlendState(string name, ColorAttachmentBlendState colorAttachment)
    {
        CheckEnum($"{name}.SourceRgbBlendFactor", colorAttachment.SourceRgbBlendFactor);
        CheckEnum($"{name}.DestinationRgbBlendFactor", colorAttachment.DestinationRgbBlendFactor);
        CheckEnum($"{name}.RgbBlendOperation", colorAttachment.RgbBlendOperation);
        CheckEnum($"{name}.SourceAlphaBlendFactor", colorAttachment.SourceAlphaBlendFactor);
        CheckEnum($"{name}.DestinationAlphaBlendFactor", colorAttachment.DestinationAlphaBlendFactor);
        CheckEnum($"{name}.AlphaBlendOperation", colorAttachment.AlphaBlendOperation);
        CheckFlags($"{name}.ColorWrites", colorAttachment.ColorWrites);
    }

    private void CheckAttachmentFormats(string name, AttachmentFormats attachmentFormats)
    {
        if (attachmentFormats.ColorFormats is null)
        {
            ReportError(string.Format(ValidationMessages.MustNotBeNull, $"{name}.ColorFormats"));

            return;
        }

        for (int i = 0; i < attachmentFormats.ColorFormats.Length; i++)
        {
            CheckEnum($"{name}.ColorFormats[{i}]", attachmentFormats.ColorFormats[i]);
        }

        if (attachmentFormats.DepthStencilFormat is { } format)
        {
            CheckEnum($"{name}.DepthStencilFormat", format);
        }

        CheckEnum($"{name}.SampleCount", attachmentFormats.SampleCount);

        if (attachmentFormats.ColorFormats.Length is 0 && attachmentFormats.DepthStencilFormat is null)
        {
            ReportWarning(string.Format(ValidationMessages.HasNoAttachments, name));
        }
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

        bool hasIndexBuffer = CheckResource($"{name}.IndexBuffer", triangleGeometry.IndexBuffer);

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

    public const int MaxRayTracingInstanceId = 16777215;
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

    public const string UsagesIncompatibleWithAccess = "{0} contains flags '{1}' that require GPU read-write access and cannot be combined with BufferAccess.{2}.";
}
