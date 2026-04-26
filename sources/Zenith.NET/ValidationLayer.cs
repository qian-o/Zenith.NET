namespace Zenith.NET;

public abstract class ValidationLayer(GraphicsContext context) : GraphicsResource(context)
{
    protected void Report(MessageSource source, MessageSeverity severity, string message)
    {
        Context.OnValidationMessage(new(source, severity, message));
    }

    internal void ValidateDesc(SwapChainDesc desc)
    {
        if (desc.Surface.Handles is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "SwapChainDesc.Surface.Handles"));

            return;
        }

        switch (desc.Surface.Type)
        {
            case SurfaceType.Win32:
                if (desc.Surface.Handles.Length is not 1)
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustHaveExactlyNHandles, "SwapChainDesc.Surface.Handles", 1, "SurfaceType.Win32"));
                }
                else if (desc.Surface.Handles[0] is 0)
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeValidHandle, "SwapChainDesc.Surface.Handles[0]", "SurfaceType.Win32"));
                }
                break;

            case SurfaceType.Wayland:
                if (desc.Surface.Handles.Length is not 2)
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustHaveExactlyNHandles, "SwapChainDesc.Surface.Handles", 2, "SurfaceType.Wayland"));
                }
                else if (desc.Surface.Handles[0] is 0 || desc.Surface.Handles[1] is 0)
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeValidHandles, "SwapChainDesc.Surface.Handles", "SurfaceType.Wayland"));
                }
                break;

            case SurfaceType.Xlib:
                if (desc.Surface.Handles.Length is not 2)
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustHaveExactlyNHandles, "SwapChainDesc.Surface.Handles", 2, "SurfaceType.Xlib"));
                }
                else if (desc.Surface.Handles[0] is 0 || desc.Surface.Handles[1] is 0)
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeValidHandles, "SwapChainDesc.Surface.Handles", "SurfaceType.Xlib"));
                }
                break;

            case SurfaceType.Android:
                if (desc.Surface.Handles.Length is not 1)
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustHaveExactlyNHandles, "SwapChainDesc.Surface.Handles", 1, "SurfaceType.Android"));
                }
                else if (desc.Surface.Handles[0] is 0)
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeValidHandle, "SwapChainDesc.Surface.Handles[0]", "SurfaceType.Android"));
                }
                break;

            case SurfaceType.Apple:
                if (desc.Surface.Handles.Length is not 1)
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustHaveExactlyNHandles, "SwapChainDesc.Surface.Handles", 1, "SurfaceType.Apple"));
                }
                else if (desc.Surface.Handles[0] is 0)
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeValidHandle, "SwapChainDesc.Surface.Handles[0]", "SurfaceType.Apple"));
                }
                break;
            case SurfaceType.D3D11Interop:
                if (desc.Surface.Handles.Length is not 1)
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustHaveExactlyNHandles, "SwapChainDesc.Surface.Handles", 1, "SurfaceType.D3D11Interop"));
                }
                else if (desc.Surface.Handles[0] is 0)
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeValidHandle, "SwapChainDesc.Surface.Handles[0]", "SurfaceType.D3D11Interop"));
                }
                break;

            default:
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasUnsupportedSurfaceType, "SwapChainDesc.Surface", desc.Surface.Type));
                break;
        }

        if (!Enum.IsDefined(desc.ColorTargetFormat))
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, "SwapChainDesc.ColorTargetFormat", desc.ColorTargetFormat));
        }

        if (desc.DepthStencilTargetFormat is not null && !Enum.IsDefined(desc.DepthStencilTargetFormat.Value))
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, "SwapChainDesc.DepthStencilTargetFormat", desc.DepthStencilTargetFormat.Value));
        }
    }

    internal void ValidateDesc(ShaderDesc desc)
    {
        if (desc.ShaderBytes is null || desc.ShaderBytes.Length is 0)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNullOrEmpty, "ShaderDesc.ShaderBytes"));
        }

        if (string.IsNullOrWhiteSpace(desc.EntryPoint))
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNullOrWhitespace, "ShaderDesc.EntryPoint"));
        }

        if (!Enum.IsDefined(desc.Stage))
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, "ShaderDesc.Stage", desc.Stage));
        }
    }

    internal void ValidateDesc(BufferDesc desc)
    {
        if (desc.SizeInBytes is 0)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeGreaterThanZero, "BufferDesc.SizeInBytes"));
        }

        if (desc.StrideInBytes is 0)
        {
            ReportFrameworkMessage(MessageSeverity.Warning, string.Format(ValidationMessages.IsZeroWarning, "BufferDesc.StrideInBytes", "buffer types"));
        }

        if (desc.Flags is BufferUsageFlags.None)
        {
            ReportFrameworkMessage(MessageSeverity.Warning, string.Format(ValidationMessages.IsSetToNoneWarning, "BufferDesc.Flags"));
        }
    }

    internal void ValidateDesc(TextureDesc desc)
    {
        if (!Enum.IsDefined(desc.Type))
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, "TextureDesc.Type", desc.Type));
        }

        if (!Enum.IsDefined(desc.Format))
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, "TextureDesc.Format", desc.Format));
        }

        if (desc.Width is 0 || desc.Height is 0 || desc.Depth is 0)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeGreaterThanZero, "TextureDesc dimensions (Width, Height, Depth)"));
        }

        if (desc.MipLevels is 0)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeGreaterThanZero, "TextureDesc.MipLevels"));
        }

        if (desc.ArrayLayers is 0)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeGreaterThanZero, "TextureDesc.ArrayLayers"));
        }

        if (desc.Type is TextureType.Texture3D && desc.ArrayLayers is not 1)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeEqualTo, "TextureDesc.ArrayLayers", 1));
        }

        if (desc.Type is TextureType.TextureCube && desc.ArrayLayers is not ValidationConstants.CubeMapFaceCount)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeEqualTo, "TextureDesc.ArrayLayers", ValidationConstants.CubeMapFaceCount));
        }

        if (desc.Type is TextureType.TextureCubeArray && desc.ArrayLayers % ValidationConstants.CubeMapFaceCount is not 0)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeAMultipleOf, "TextureDesc.ArrayLayers", ValidationConstants.CubeMapFaceCount));
        }

        if (!Enum.IsDefined(desc.SampleCount))
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, "TextureDesc.SampleCount", desc.SampleCount));
        }

        if (desc.Flags is TextureUsageFlags.None)
        {
            ReportFrameworkMessage(MessageSeverity.Warning, string.Format(ValidationMessages.IsSetToNoneWarning, "TextureDesc.Flags"));
        }
    }

    internal void ValidateDesc(TextureViewDesc desc)
    {
        if (desc.Texture is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "TextureViewDesc.Texture"));

            return;
        }

        if (desc.Texture.IsDisposed)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeDisposed, "TextureViewDesc.Texture"));

            return;
        }

        if (!Enum.IsDefined(desc.Type))
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, "TextureViewDesc.Type", desc.Type));
        }

        if (!Enum.IsDefined(desc.Format))
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, "TextureViewDesc.Format", desc.Format));
        }

        if (desc.Range.BaseMipLevel >= desc.Texture.Desc.MipLevels)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeLessThan, "TextureViewDesc.Range.BaseMipLevel", "the number of mip levels in the texture"));
        }

        if (desc.Range.LevelCount is 0 || desc.Range.BaseMipLevel + desc.Range.LevelCount > desc.Texture.Desc.MipLevels)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeWithinBounds, "TextureViewDesc.Range.LevelCount", "the texture mip levels"));
        }

        if (desc.Range.BaseArrayLayer >= desc.Texture.Desc.ArrayLayers)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeLessThan, "TextureViewDesc.Range.BaseArrayLayer", "the number of array layers in the texture"));
        }

        if (desc.Range.LayerCount is 0 || desc.Range.BaseArrayLayer + desc.Range.LayerCount > desc.Texture.Desc.ArrayLayers)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeWithinBounds, "TextureViewDesc.Range.LayerCount", "the texture array layers"));
        }

        if (desc.Type is TextureType.TextureCube)
        {
            if (desc.Range.BaseArrayLayer % ValidationConstants.CubeMapFaceCount is not 0)
            {
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeAMultipleOf, "TextureViewDesc.Range.BaseArrayLayer", ValidationConstants.CubeMapFaceCount));
            }

            if (desc.Range.LayerCount is not ValidationConstants.CubeMapFaceCount)
            {
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustDescribeACompleteCube, "TextureViewDesc.Range.LayerCount"));
            }
        }

        if (desc.Type is TextureType.TextureCubeArray)
        {
            if (desc.Range.BaseArrayLayer % ValidationConstants.CubeMapFaceCount is not 0)
            {
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeAMultipleOf, "TextureViewDesc.Range.BaseArrayLayer", ValidationConstants.CubeMapFaceCount));
            }

            if (desc.Range.LayerCount % ValidationConstants.CubeMapFaceCount is not 0)
            {
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeAMultipleOf, "TextureViewDesc.Range.LayerCount", ValidationConstants.CubeMapFaceCount));
            }
        }
    }

    internal void ValidateDesc(SamplerDesc desc)
    {
        if (!Enum.IsDefined(desc.Filter))
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, "SamplerDesc.Filter", desc.Filter));
        }

        if (!Enum.IsDefined(desc.U))
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, "SamplerDesc.U", desc.U));
        }

        if (!Enum.IsDefined(desc.V))
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, "SamplerDesc.V", desc.V));
        }

        if (!Enum.IsDefined(desc.W))
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, "SamplerDesc.W", desc.W));
        }

        if (!Enum.IsDefined(desc.ComparisonFunc))
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, "SamplerDesc.ComparisonFunc", desc.ComparisonFunc));
        }

        if (desc.Filter is Filter.Anisotropic && desc.MaxAnisotropy is 0)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeGreaterThanZero, "SamplerDesc.MaxAnisotropy"));
        }

        if (desc.MaxAnisotropy > ValidationConstants.MaxAnisotropy)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeLessThanOrEqualTo, "SamplerDesc.MaxAnisotropy", ValidationConstants.MaxAnisotropy));
        }

        if (desc.MinLod > desc.MaxLod)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeLessThanOrEqualTo, "SamplerDesc.MinLod", "MaxLod"));
        }

        if (!Enum.IsDefined(desc.BorderColor))
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, "SamplerDesc.BorderColor", desc.BorderColor));
        }
    }

    internal void ValidateDesc(ResourceTableDesc desc)
    {
        if (desc.Bindings is null || desc.Bindings.Length is 0)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNullOrEmpty, "ResourceTableDesc.Bindings"));

            return;
        }

        for (int i = 0; i < desc.Bindings.Length; i++)
        {
            CheckResourceBinding($"ResourceTableDesc.Bindings[{i}]", desc.Bindings[i]);
        }
    }

    internal void ValidateDesc(GraphicsPipelineDesc desc)
    {
        CheckRenderStates("GraphicsPipelineDesc", desc.RenderStates);

        if (desc.Vertex is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "GraphicsPipelineDesc.Vertex"));
        }
        else if (desc.Vertex.IsDisposed)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeDisposed, "GraphicsPipelineDesc.Vertex"));
        }

        if (desc.Pixel is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "GraphicsPipelineDesc.Pixel"));
        }
        else if (desc.Pixel.IsDisposed)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeDisposed, "GraphicsPipelineDesc.Pixel"));
        }

        if (desc.ResourceBindings is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "GraphicsPipelineDesc.ResourceBindings"));
        }
        else
        {
            for (int i = 0; i < desc.ResourceBindings.Length; i++)
            {
                CheckResourceBinding($"GraphicsPipelineDesc.ResourceBindings[{i}]", desc.ResourceBindings[i]);
            }
        }

        if (desc.InputLayouts is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "GraphicsPipelineDesc.InputLayouts"));
        }
        else
        {
            for (int i = 0; i < desc.InputLayouts.Length; i++)
            {
                CheckInputLayout($"GraphicsPipelineDesc.InputLayouts[{i}]", desc.InputLayouts[i]);
            }
        }

        if (!Enum.IsDefined(desc.PrimitiveTopology))
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, "GraphicsPipelineDesc.PrimitiveTopology", desc.PrimitiveTopology));
        }

        CheckOutput("GraphicsPipelineDesc.Output", desc.Output);

        void CheckInputLayout(string name, InputLayout inputLayout)
        {
            if (inputLayout.Elements is null || inputLayout.Elements.Length is 0)
            {
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNullOrEmpty, $"{name}.Elements"));

                return;
            }

            foreach (InputElement element in inputLayout.Elements)
            {
                if (!Enum.IsDefined(element.Format))
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, $"{name}.Elements.ElementFormat", element.Format));
                }

                if (!Enum.IsDefined(element.Semantic))
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, $"{name}.Elements.ElementSemantic", element.Semantic));
                }
            }
        }
    }

    internal void ValidateDesc(ComputePipelineDesc desc)
    {
        if (desc.Compute is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "ComputePipelineDesc.Compute"));
        }
        else if (desc.Compute.IsDisposed)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeDisposed, "ComputePipelineDesc.Compute"));
        }

        if (desc.ResourceBindings is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "ComputePipelineDesc.ResourceBindings"));
        }
        else
        {
            for (int i = 0; i < desc.ResourceBindings.Length; i++)
            {
                CheckResourceBinding($"ComputePipelineDesc.ResourceBindings[{i}]", desc.ResourceBindings[i]);
            }
        }

        if (desc.ThreadGroupSizeX is 0 || desc.ThreadGroupSizeY is 0 || desc.ThreadGroupSizeZ is 0)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeGreaterThanZero, "ComputePipelineDesc thread group sizes (ThreadGroupSizeX, ThreadGroupSizeY, ThreadGroupSizeZ)"));
        }
    }

    internal void ValidateDesc(MeshShadingPipelineDesc desc)
    {
        CheckRenderStates("MeshShadingPipelineDesc", desc.RenderStates);

        if (desc.Amplification?.IsDisposed is true)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeDisposed, "MeshShadingPipelineDesc.Amplification"));
        }

        if (desc.Mesh is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "MeshShadingPipelineDesc.Mesh"));
        }
        else if (desc.Mesh.IsDisposed)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeDisposed, "MeshShadingPipelineDesc.Mesh"));
        }

        if (desc.Pixel is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "MeshShadingPipelineDesc.Pixel"));
        }
        else if (desc.Pixel.IsDisposed)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeDisposed, "MeshShadingPipelineDesc.Pixel"));
        }

        if (desc.ResourceBindings is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "MeshShadingPipelineDesc.ResourceBindings"));
        }
        else
        {
            for (int i = 0; i < desc.ResourceBindings.Length; i++)
            {
                CheckResourceBinding($"MeshShadingPipelineDesc.ResourceBindings[{i}]", desc.ResourceBindings[i]);
            }
        }

        if (desc.PrimitiveTopology is not PrimitiveTopology.LineList and not PrimitiveTopology.TriangleList)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeOneOf, "MeshShadingPipelineDesc.PrimitiveTopology", "LineList, TriangleList"));
        }

        CheckOutput("MeshShadingPipelineDesc.Output", desc.Output);

        if (desc.Amplification is not null && (desc.AmplificationThreadGroupSizeX is 0 || desc.AmplificationThreadGroupSizeY is 0 || desc.AmplificationThreadGroupSizeZ is 0))
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeGreaterThanZero, "MeshShadingPipelineDesc amplification thread group sizes (AmplificationThreadGroupSizeX, AmplificationThreadGroupSizeY, AmplificationThreadGroupSizeZ)"));
        }

        if (desc.MeshThreadGroupSizeX is 0 || desc.MeshThreadGroupSizeY is 0 || desc.MeshThreadGroupSizeZ is 0)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeGreaterThanZero, "MeshShadingPipelineDesc mesh thread group sizes (MeshThreadGroupSizeX, MeshThreadGroupSizeY, MeshThreadGroupSizeZ)"));
        }
    }

    internal void ValidateDesc(QueryHeapDesc desc)
    {
        if (!Enum.IsDefined(desc.Type))
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, "QueryHeapDesc.Type", desc.Type));
        }

        if (desc.Count is 0)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeGreaterThanZero, "QueryHeapDesc.Count"));
        }
    }

    internal void ValidateDesc(BottomLevelAccelerationStructureDesc desc)
    {
        if (desc.Geometries is null || desc.Geometries.Length is 0)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNullOrEmpty, "BottomLevelAccelerationStructureDesc.Geometries"));

            return;
        }

        for (int i = 0; i < desc.Geometries.Length; i++)
        {
            CheckRayTracingGeometry($"BottomLevelAccelerationStructureDesc.Geometries[{i}]", desc.Geometries[i]);
        }

        void CheckRayTracingGeometry(string name, RayTracingGeometry rayTracingGeometry)
        {
            switch (rayTracingGeometry.Type)
            {
                case RayTracingGeometryType.Triangles:
                    {
                        RayTracingTriangles triangles = rayTracingGeometry.Triangles;

                        if (triangles.VertexBuffer is null)
                        {
                            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, $"{name}.Triangles.VertexBuffer"));

                            break;
                        }

                        if (triangles.VertexBuffer.IsDisposed)
                        {
                            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeDisposed, $"{name}.Triangles.VertexBuffer"));

                            break;
                        }

                        if (!Enum.IsDefined(triangles.VertexFormat))
                        {
                            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, $"{name}.Triangles.VertexFormat", triangles.VertexFormat));
                        }

                        if (triangles.VertexCount is 0)
                        {
                            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeGreaterThanZero, $"{name}.Triangles.VertexCount"));
                        }

                        if (triangles.VertexStrideInBytes is 0)
                        {
                            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeGreaterThanZero, $"{name}.Triangles.VertexStrideInBytes"));
                        }

                        if (triangles.VertexOffsetInBytes + (triangles.VertexCount * triangles.VertexStrideInBytes) > triangles.VertexBuffer.Desc.SizeInBytes)
                        {
                            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeWithinBounds, $"{name}.Triangles.VertexCount", "the vertex buffer"));
                        }

                        if (triangles.IndexBuffer is null)
                        {
                            break;
                        }

                        if (triangles.IndexBuffer.IsDisposed)
                        {
                            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeDisposed, $"{name}.Triangles.IndexBuffer"));

                            break;
                        }

                        if (!Enum.IsDefined(triangles.IndexFormat))
                        {
                            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, $"{name}.Triangles.IndexFormat", triangles.IndexFormat));
                        }

                        if (triangles.IndexCount is 0)
                        {
                            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeGreaterThanZero, $"{name}.Triangles.IndexCount"));
                        }

                        uint indexSizeInBytes = triangles.IndexFormat switch
                        {
                            IndexFormat.UInt16 => ValidationConstants.IndexSizeUInt16,
                            IndexFormat.UInt32 => ValidationConstants.IndexSizeUInt32,
                            _ => 0
                        };

                        if (triangles.IndexOffsetInBytes + (triangles.IndexCount * indexSizeInBytes) > triangles.IndexBuffer.Desc.SizeInBytes)
                        {
                            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeWithinBounds, $"{name}.Triangles.IndexCount", "the index buffer"));
                        }
                    }
                    break;

                case RayTracingGeometryType.AABBs:
                    {
                        RayTracingAABBs aABBs = rayTracingGeometry.AABBs;

                        if (aABBs.Buffer is null)
                        {
                            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, $"{name}.AABBs.Buffer"));

                            break;
                        }

                        if (aABBs.Buffer.IsDisposed)
                        {
                            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeDisposed, $"{name}.AABBs.Buffer"));

                            break;
                        }

                        if (aABBs.Count is 0)
                        {
                            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeGreaterThanZero, $"{name}.AABBs.Count"));
                        }

                        if (aABBs.StrideInBytes is 0)
                        {
                            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeGreaterThanZero, $"{name}.AABBs.StrideInBytes"));
                        }

                        if (aABBs.OffsetInBytes + (aABBs.Count * aABBs.StrideInBytes) > aABBs.Buffer.Desc.SizeInBytes)
                        {
                            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeWithinBounds, $"{name}.AABBs.Count", "the AABBs buffer"));
                        }
                    }
                    break;

                default:
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, $"{name}.Type", rayTracingGeometry.Type));
                    break;
            }
        }
    }

    internal void ValidateDesc(TopLevelAccelerationStructureDesc desc)
    {
        if (desc.Instances is null || desc.Instances.Length is 0)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNullOrEmpty, "TopLevelAccelerationStructureDesc.Instances"));

            return;
        }

        for (int i = 0; i < desc.Instances.Length; i++)
        {
            CheckRayTracingInstance($"TopLevelAccelerationStructureDesc.Instances[{i}]", desc.Instances[i]);
        }

        void CheckRayTracingInstance(string name, RayTracingInstance rayTracingInstance)
        {
            if (rayTracingInstance.AccelerationStructure is null)
            {
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, $"{name}.AccelerationStructure"));

                return;
            }

            if (rayTracingInstance.AccelerationStructure.IsDisposed)
            {
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeDisposed, $"{name}.AccelerationStructure"));

                return;
            }

            if (rayTracingInstance.ID > ValidationConstants.MaxRayTracingInstanceID)
            {
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeLessThanOrEqualTo, $"{name}.ID", ValidationConstants.MaxRayTracingInstanceID));
            }
        }
    }

    internal void ValidateDesc(TopLevelAccelerationStructureDesc oldDesc, TopLevelAccelerationStructureDesc newDesc)
    {
        ValidateDesc(newDesc);

        if (newDesc.Instances is null)
        {
            return;
        }

        if (oldDesc.Instances.Length != newDesc.Instances.Length)
        {
            ReportFrameworkMessage(MessageSeverity.Error, ValidationMessages.InstanceCountMustRemainSame);
        }
    }

    private void CheckResourceBinding(string name, ResourceBinding resourceBinding)
    {
        if (!Enum.IsDefined(resourceBinding.Type))
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, $"{name}.Type", resourceBinding.Type));
        }

        if (resourceBinding.Count is 0)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeGreaterThanZero, $"{name}.Count"));
        }
    }

    private void CheckRenderStates(string name, RenderStates renderStates)
    {
        if (!Enum.IsDefined(renderStates.RasterizerState.CullMode))
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, $"{name}.RenderStates.RasterizerState.CullMode", renderStates.RasterizerState.CullMode));
        }

        if (!Enum.IsDefined(renderStates.RasterizerState.FillMode))
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, $"{name}.RenderStates.RasterizerState.FillMode", renderStates.RasterizerState.FillMode));
        }

        if (!Enum.IsDefined(renderStates.RasterizerState.FrontFace))
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, $"{name}.RenderStates.RasterizerState.FrontFace", renderStates.RasterizerState.FrontFace));
        }

        if (!Enum.IsDefined(renderStates.DepthStencilState.DepthFunc))
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, $"{name}.RenderStates.DepthStencilState.DepthFunc", renderStates.DepthStencilState.DepthFunc));
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
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, $"{name}.StencilFailOp", depthStencilStateOp.StencilFailOp));
            }

            if (!Enum.IsDefined(depthStencilStateOp.StencilDepthFailOp))
            {
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, $"{name}.StencilDepthFailOp", depthStencilStateOp.StencilDepthFailOp));
            }

            if (!Enum.IsDefined(depthStencilStateOp.StencilPassOp))
            {
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, $"{name}.StencilPassOp", depthStencilStateOp.StencilPassOp));
            }

            if (!Enum.IsDefined(depthStencilStateOp.StencilFunc))
            {
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, $"{name}.StencilFunc", depthStencilStateOp.StencilFunc));
            }
        }

        void CheckBlendStateRenderTarget(string name, BlendStateRenderTarget blendStateRenderTarget)
        {
            if (!Enum.IsDefined(blendStateRenderTarget.SrcBlend))
            {
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, $"{name}.SrcBlend", blendStateRenderTarget.SrcBlend));
            }

            if (!Enum.IsDefined(blendStateRenderTarget.DestBlend))
            {
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, $"{name}.DestBlend", blendStateRenderTarget.DestBlend));
            }

            if (!Enum.IsDefined(blendStateRenderTarget.BlendOp))
            {
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, $"{name}.BlendOp", blendStateRenderTarget.BlendOp));
            }

            if (!Enum.IsDefined(blendStateRenderTarget.SrcBlendAlpha))
            {
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, $"{name}.SrcBlendAlpha", blendStateRenderTarget.SrcBlendAlpha));
            }

            if (!Enum.IsDefined(blendStateRenderTarget.DestBlendAlpha))
            {
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, $"{name}.DestBlendAlpha", blendStateRenderTarget.DestBlendAlpha));
            }

            if (!Enum.IsDefined(blendStateRenderTarget.BlendOpAlpha))
            {
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, $"{name}.BlendOpAlpha", blendStateRenderTarget.BlendOpAlpha));
            }
        }
    }

    private void CheckOutput(string name, Output output)
    {
        if (output.ColorAttachments is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, $"{name}.ColorAttachments"));

            return;
        }

        for (int i = 0; i < output.ColorAttachments.Length; i++)
        {
            if (!Enum.IsDefined(output.ColorAttachments[i]))
            {
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, $"{name}.ColorAttachments[{i}]", output.ColorAttachments[i]));
            }
        }

        if (output.DepthStencilAttachment is not null && !Enum.IsDefined(output.DepthStencilAttachment.Value))
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, $"{name}.DepthStencilAttachment", output.DepthStencilAttachment.Value));
        }

        if (output.ColorAttachments.Length is 0 && output.DepthStencilAttachment is null)
        {
            ReportFrameworkMessage(MessageSeverity.Warning, string.Format(ValidationMessages.HasNoAttachments, name));
        }
    }

    private void ReportFrameworkMessage(MessageSeverity severity, string message)
    {
        Report(MessageSource.Framework, severity, message);
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

    public const string InstanceCountMustRemainSame = "When updating a TopLevelAccelerationStructure, the number of instances must remain the same.";
}