namespace Zenith.NET;

partial class ValidationLayer
{
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
        CheckEnum("SamplerDesc.MipmapFilter", desc.MipmapFilter);
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
        CheckRenderState("GraphicsPipelineDesc.RenderState", desc.RenderState);

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
    }

    internal void ValidateDesc(ComputePipelineDesc desc)
    {
        CheckResource("ComputePipelineDesc.ComputeShader", desc.ComputeShader);
    }

    internal void ValidateDesc(MeshShadingPipelineDesc desc)
    {
        CheckRenderState("MeshShadingPipelineDesc.RenderState", desc.RenderState);

        CheckResource("MeshShadingPipelineDesc.MeshShader", desc.MeshShader);
        CheckResource("MeshShadingPipelineDesc.FragmentShader", desc.FragmentShader);

        if (desc.PrimitiveTopology is not PrimitiveTopology.LineList and not PrimitiveTopology.TriangleList)
        {
            ReportError(string.Format(ValidationMessages.MustBeOneOf, "MeshShadingPipelineDesc.PrimitiveTopology", "LineList, TriangleList"));
        }

        CheckAttachmentFormats("MeshShadingPipelineDesc.AttachmentFormats", desc.AttachmentFormats);

        if (desc.TaskShader is not null)
        {
            CheckResource("MeshShadingPipelineDesc.TaskShader", desc.TaskShader);
        }
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
}
