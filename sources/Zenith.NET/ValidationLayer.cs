using System.Runtime.InteropServices;

namespace Zenith.NET;

public abstract class ValidationLayer(GraphicsContext context) : GraphicsResource(context)
{
    private static class ValidationConstants
    {
        public const int CubeMapFaceCount = 6;

        public const int MaxTraceRecursionDepth = 31;

        public const int MaxInstanceId = 16777215;

        public const int MaxHitGroupIndex = 65535;

        public const int IndexSizeUInt16 = 2;

        public const int IndexSizeUInt32 = 4;
    }

    private static class ValidationMessages
    {
        public const string MustNotBeNull = "{0} must not be null.";

        public const string MustBeGreaterThanZero = "{0} must be greater than zero.";

        public const string MustHaveExactlyNHandles = "{0} must have exactly {1} handle(s) for {2}.";

        public const string MustBeValidHandle = "{0} must be a valid handle for {1}.";

        public const string MustBeValidHandles = "{0} must be valid handles for {1}.";

        public const string HasInvalidValue = "{0} has an invalid value '{1}'.";

        public const string MustNotBeNullOrEmpty = "{0} must not be null or empty.";

        public const string MustNotBeNullOrWhitespace = "{0} must not be null or whitespace.";

        public const string MustBeLessThan = "{0} must be less than {1}.";

        public const string MustBeLessThanOrEqualTo = "{0} must be less than or equal to {1}.";

        public const string MustBeWithinBounds = "{0} must be greater than zero and within the bounds of {1}.";

        public const string LengthMustMatch = "{0} length must match {1}.";

        public const string MustBeOfType = "{0} item must be a {1} for {2} binding.";

        public const string MustBeValidPixelBufferHandle = "{0} must be a valid PixelBuffer handle for {1}.";

        public const string HasNoAttachments = "{0} has no attachments.";

        public const string IsZeroWarning = "{0} is zero, which may be valid for some {1} but could indicate an issue.";

        public const string IsSetToNoneWarning = "{0} is set to None, which may be valid but could indicate an issue.";

        public const string HasUnsupportedSurfaceType = "{0} has unsupported SurfaceType '{1}'.";

        public const string InstanceCountMustRemainSame = "When updating a TopLevelAccelerationStructure, the number of instances must remain the same.";
    }

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
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustHaveExactlyNHandles, "SwapChainDesc.Surface.Handles", "one", "SurfaceType.Win32"));
                }
                else if (desc.Surface.Handles[0] is 0)
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeValidHandle, "SwapChainDesc.Surface.Handles[0]", "SurfaceType.Win32"));
                }
                break;

            case SurfaceType.Wayland:
                if (desc.Surface.Handles.Length is not 2)
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustHaveExactlyNHandles, "SwapChainDesc.Surface.Handles", "two", "SurfaceType.Wayland"));
                }
                else if (desc.Surface.Handles[0] is 0 || desc.Surface.Handles[1] is 0)
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeValidHandles, "SwapChainDesc.Surface.Handles", "SurfaceType.Wayland"));
                }
                break;

            case SurfaceType.Xlib:
                if (desc.Surface.Handles.Length is not 2)
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustHaveExactlyNHandles, "SwapChainDesc.Surface.Handles", "two", "SurfaceType.Xlib"));
                }
                else if (desc.Surface.Handles[0] is 0 || desc.Surface.Handles[1] is 0)
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeValidHandles, "SwapChainDesc.Surface.Handles", "SurfaceType.Xlib"));
                }
                break;

            case SurfaceType.Android:
                if (desc.Surface.Handles.Length is not 1)
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustHaveExactlyNHandles, "SwapChainDesc.Surface.Handles", "one", "SurfaceType.Android"));
                }
                else if (desc.Surface.Handles[0] is 0)
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeValidHandle, "SwapChainDesc.Surface.Handles[0]", "SurfaceType.Android"));
                }
                break;

            case SurfaceType.IOS:
                if (desc.Surface.Handles.Length is not 1)
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustHaveExactlyNHandles, "SwapChainDesc.Surface.Handles", "one", "SurfaceType.IOS"));
                }
                else if (desc.Surface.Handles[0] is 0)
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeValidHandle, "SwapChainDesc.Surface.Handles[0]", "SurfaceType.IOS"));
                }
                break;

            case SurfaceType.MacOS:
                if (desc.Surface.Handles.Length is not 1)
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustHaveExactlyNHandles, "SwapChainDesc.Surface.Handles", "one", "SurfaceType.MacOS"));
                }
                else if (desc.Surface.Handles[0] is 0)
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeValidHandle, "SwapChainDesc.Surface.Handles[0]", "SurfaceType.MacOS"));
                }
                break;

            case SurfaceType.PixelBuffer:
                if (desc.Surface.Handles.Length is not 1)
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustHaveExactlyNHandles, "SwapChainDesc.Surface.Handles", "one", "SurfaceType.PixelBuffer"));
                }
                else if (desc.Surface.Handles[0] is 0 || GCHandle.FromIntPtr(desc.Surface.Handles[0]).Target is not IPixelBuffer)
                {
                    ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeValidPixelBufferHandle, "SwapChainDesc.Surface.Handles[0]", "SurfaceType.PixelBuffer"));
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

    internal void ValidateDesc(FrameBufferDesc desc)
    {
        if (desc.ColorAttachments is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "FrameBufferDesc.ColorAttachments"));

            return;
        }

        for (int i = 0; i < desc.ColorAttachments.Length; i++)
        {
            CheckFrameBufferAttachment($"FrameBufferDesc.ColorAttachments[{i}]", desc.ColorAttachments[i]);
        }

        if (desc.DepthStencilAttachment is not null)
        {
            CheckFrameBufferAttachment("FrameBufferDesc.DepthStencilAttachment", desc.DepthStencilAttachment.Value);
        }

        if (desc.ColorAttachments.Length is 0 && desc.DepthStencilAttachment is null)
        {
            ReportFrameworkMessage(MessageSeverity.Warning, string.Format(ValidationMessages.HasNoAttachments, "FrameBufferDesc"));
        }

        void CheckFrameBufferAttachment(string name, FrameBufferAttachment frameBufferAttachment)
        {
            if (frameBufferAttachment.Target is null)
            {
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, $"{name}.Target"));

                return;
            }

            if (frameBufferAttachment.Slice.Face >= ValidationConstants.CubeMapFaceCount)
            {
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeLessThan, $"{name}.Slice.Face", ValidationConstants.CubeMapFaceCount));
            }

            if (frameBufferAttachment.Slice.Layer >= frameBufferAttachment.Target.Desc.Layers)
            {
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeLessThan, $"{name}.Slice.Layer", "the number of layers in the texture"));
            }

            if (frameBufferAttachment.Slice.MipLevel >= frameBufferAttachment.Target.Desc.MipLevels)
            {
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeLessThan, $"{name}.Slice.MipLevel", "the number of mip levels in the texture"));
            }
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

    internal void ValidateDesc(BufferViewDesc desc)
    {
        if (desc.Buffer is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "BufferViewDesc.Buffer"));

            return;
        }

        if (desc.OffsetInBytes >= desc.Buffer.Desc.SizeInBytes)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeLessThan, "BufferViewDesc.OffsetInBytes", "the size of the buffer"));
        }

        if (desc.SizeInBytes is 0 || desc.OffsetInBytes + desc.SizeInBytes > desc.Buffer.Desc.SizeInBytes)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeWithinBounds, "BufferViewDesc.SizeInBytes", "the buffer"));
        }

        if (desc.StrideInBytes is 0)
        {
            ReportFrameworkMessage(MessageSeverity.Warning, string.Format(ValidationMessages.IsZeroWarning, "BufferViewDesc.StrideInBytes", "buffer views"));
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

        if (desc.Layers is 0)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeGreaterThanZero, "TextureDesc.Layers"));
        }

        if (desc.MipLevels is 0)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeGreaterThanZero, "TextureDesc.MipLevels"));
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

        if (desc.FirstLayer >= desc.Texture.Desc.Layers)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeLessThan, "TextureViewDesc.FirstLayer", "the number of layers in the texture"));
        }

        if (desc.LayerCount is 0 || desc.FirstLayer + desc.LayerCount > desc.Texture.Desc.Layers)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeWithinBounds, "TextureViewDesc.LayerCount", "the texture layers"));
        }

        if (desc.FirstMipLevel >= desc.Texture.Desc.MipLevels)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeLessThan, "TextureViewDesc.FirstMipLevel", "the number of mip levels in the texture"));
        }

        if (desc.MipLevelCount is 0 || desc.FirstMipLevel + desc.MipLevelCount > desc.Texture.Desc.MipLevels)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeWithinBounds, "TextureViewDesc.MipLevelCount", "the texture mip levels"));
        }
    }

    internal void ValidateDesc(SamplerDesc desc)
    {
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

        if (!Enum.IsDefined(desc.Filter))
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, "SamplerDesc.Filter", desc.Filter));
        }

        if (!Enum.IsDefined(desc.ComparisonFunc))
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, "SamplerDesc.ComparisonFunc", desc.ComparisonFunc));
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

    internal void ValidateDesc(ResourceLayoutDesc desc)
    {
        if (desc.Bindings is null || desc.Bindings.Length is 0)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNullOrEmpty, "ResourceLayoutDesc.Bindings"));

            return;
        }

        foreach (ResourceBinding binding in desc.Bindings)
        {
            if (!Enum.IsDefined(binding.Type))
            {
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.HasInvalidValue, "ResourceLayoutBinding.Type", binding.Type));
            }
        }
    }

    internal void ValidateDesc(ResourceSetDesc desc)
    {
        if (desc.Layout is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "ResourceSetDesc.Layout"));

            return;
        }

        if (desc.Resources is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "ResourceSetDesc.Resources"));

            return;
        }

        if (desc.Resources.Length != desc.Layout.Desc.Bindings.Length)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.LengthMustMatch, "ResourceSetDesc.Resources", "the number of bindings in the layout"));

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
                        ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeOfType, "ResourceSetDesc.Resources", "Buffer or BufferView", "ConstantBuffer"));
                    }
                    break;

                case ResourceType.StructuredBuffer:
                    if (resource is not Buffer or BufferView)
                    {
                        ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeOfType, "ResourceSetDesc.Resources", "Buffer or BufferView", "StructuredBuffer"));
                    }
                    break;

                case ResourceType.StructuredBufferReadWrite:
                    if (resource is not Buffer or BufferView)
                    {
                        ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeOfType, "ResourceSetDesc.Resources", "Buffer or BufferView", "StructuredBufferReadWrite"));
                    }
                    break;

                case ResourceType.Texture:
                    if (resource is not Texture or TextureView)
                    {
                        ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeOfType, "ResourceSetDesc.Resources", "Texture or TextureView", "Texture"));
                    }
                    break;

                case ResourceType.TextureReadWrite:
                    if (resource is not Texture or TextureView)
                    {
                        ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeOfType, "ResourceSetDesc.Resources", "Texture or TextureView", "TextureReadWrite"));
                    }
                    break;

                case ResourceType.Sampler:
                    if (resource is not Sampler)
                    {
                        ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeOfType, "ResourceSetDesc.Resources", "Sampler", "Sampler"));
                    }
                    break;

                case ResourceType.AccelerationStructure:
                    if (resource is not TopLevelAccelerationStructure)
                    {
                        ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeOfType, "ResourceSetDesc.Resources", "TopLevelAccelerationStructure", "AccelerationStructure"));
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
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "GraphicsPipelineDesc.Vertex"));
        }

        if (desc.Pixel is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "GraphicsPipelineDesc.Pixel"));
        }

        if (desc.ResourceLayouts is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "GraphicsPipelineDesc.ResourceLayouts"));
        }

        if (desc.InputLayouts is null || desc.InputLayouts.Length is 0)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNullOrEmpty, "GraphicsPipelineDesc.InputLayouts"));
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

        if (desc.ResourceLayouts is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "ComputePipelineDesc.ResourceLayouts"));
        }

        if (desc.ThreadGroupSizeX is 0 || desc.ThreadGroupSizeY is 0 || desc.ThreadGroupSizeZ is 0)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeGreaterThanZero, "ComputePipelineDesc thread group sizes (ThreadGroupSizeX, ThreadGroupSizeY, ThreadGroupSizeZ)"));
        }
    }

    internal void ValidateDesc(RayTracingPipelineDesc desc)
    {
        if (desc.RayGeneration is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "RayTracingPipelineDesc.RayGeneration"));
        }

        if (desc.Miss is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "RayTracingPipelineDesc.Miss"));
        }

        if (desc.AnyHit is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "RayTracingPipelineDesc.AnyHit"));
        }

        if (desc.Intersection is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "RayTracingPipelineDesc.Intersection"));
        }

        if (desc.ClosestHit is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "RayTracingPipelineDesc.ClosestHit"));
        }

        if (desc.HitGroups is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "RayTracingPipelineDesc.HitGroups"));
        }

        if (desc.ResourceLayouts is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "RayTracingPipelineDesc.ResourceLayouts"));
        }

        if (desc.MaxTraceRecursionDepth > ValidationConstants.MaxTraceRecursionDepth)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeLessThanOrEqualTo, "RayTracingPipelineDesc.MaxTraceRecursionDepth", ValidationConstants.MaxTraceRecursionDepth));
        }

        if (desc.MaxPayloadSizeInBytes is 0)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeGreaterThanZero, "RayTracingPipelineDesc.MaxPayloadSizeInBytes"));
        }

        if (desc.MaxAttributeSizeInBytes is 0)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeGreaterThanZero, "RayTracingPipelineDesc.MaxAttributeSizeInBytes"));
        }
    }

    internal void ValidateDesc(MeshShadingPipelineDesc desc)
    {
        CheckRenderStates("MeshShadingPipelineDesc", desc.RenderStates);

        if (desc.Mesh is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "MeshShadingPipelineDesc.Mesh"));
        }

        if (desc.Pixel is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "MeshShadingPipelineDesc.Pixel"));
        }

        if (desc.ResourceLayouts is null)
        {
            ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustNotBeNull, "MeshShadingPipelineDesc.ResourceLayouts"));
        }

        CheckOutput("MeshShadingPipelineDesc.Output", desc.Output);
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

            if (rayTracingInstance.InstanceID > ValidationConstants.MaxInstanceId)
            {
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeLessThanOrEqualTo, $"{name}.InstanceID", ValidationConstants.MaxInstanceId));
            }

            if (rayTracingInstance.InstanceContributionToHitGroupIndex > ValidationConstants.MaxHitGroupIndex)
            {
                ReportFrameworkMessage(MessageSeverity.Error, string.Format(ValidationMessages.MustBeLessThanOrEqualTo, $"{name}.InstanceContributionToHitGroupIndex", ValidationConstants.MaxHitGroupIndex));
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