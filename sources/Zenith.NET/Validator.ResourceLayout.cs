namespace Zenith.NET;

internal partial class Validator
{
    public void SwapChainDesc(SwapChainDesc desc)
    {
        Surface(desc.Surface);

        if (!swapChainFormats.Contains(desc.ColorTargetFormat))
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Color target format '{desc.ColorTargetFormat}' is not supported. Valid formats: {string.Join(", ", swapChainFormats.Select(static item => item.ToString()))}.");
        }

        if (desc.DepthStencilTargetFormat.HasValue && !depthStencilFormats.Contains(desc.DepthStencilTargetFormat.Value))
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Depth-stencil target format '{desc.DepthStencilTargetFormat.Value}' is not supported. Valid formats: {string.Join(", ", depthStencilFormats.Select(static item => item.ToString()))}.");
        }
    }

    public void BufferDesc(BufferDesc desc)
    {
        if (desc.SizeInBytes is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Buffer size must be greater than 0 bytes.");
        }

        if (desc.StrideInBytes is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Warning,
                                         "Buffer stride is 0 bytes. This may cause issues with structured buffers.");
        }
    }

    public void BufferViewDesc(BufferViewDesc desc)
    {
        if (desc.Buffer?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Buffer view must reference a valid buffer.");

            return;
        }

        if (desc.OffsetInBytes >= desc.Buffer.Desc.SizeInBytes)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Buffer view offset ({desc.OffsetInBytes} bytes) exceeds buffer size ({desc.Buffer.Desc.SizeInBytes} bytes).");
        }

        if (desc.SizeInBytes is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Buffer view size must be greater than 0 bytes.");
        }
        else if (desc.OffsetInBytes + desc.SizeInBytes > desc.Buffer.Desc.SizeInBytes)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Buffer view range [{desc.OffsetInBytes}, {desc.OffsetInBytes + desc.SizeInBytes}) exceeds buffer bounds [0, {desc.Buffer.Desc.SizeInBytes}).");
        }

        if (desc.StrideInBytes is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Warning,
                                         "Buffer view stride is 0 bytes. This may cause issues with structured buffers.");
        }
    }

    public void TextureDesc(TextureDesc desc)
    {
        DefinedEnum(desc.Type, "texture type");

        DefinedEnum(desc.Format, "texture format");

        if (desc.Type is TextureType.Texture1D or TextureType.Texture1DArray)
        {
            if (desc.Width is 0)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             "1D texture width must be greater than 0.");
            }

            if (desc.Height is not 1 || desc.Depth is not 1)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             "1D texture must have height and depth of 1.");
            }
        }
        else if (desc.Type is TextureType.Texture2D or TextureType.Texture2DArray)
        {
            if (desc.Width is 0 || desc.Height is 0)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             "2D texture dimensions must be greater than 0.");
            }

            if (desc.Depth is not 1)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             "2D texture must have depth of 1.");
            }
        }
        else if (desc.Type is TextureType.Texture3D)
        {
            if (desc.Width is 0 || desc.Height is 0 || desc.Depth is 0)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             "3D texture dimensions must be greater than 0.");
            }
        }
        else if (desc.Type is TextureType.TextureCube or TextureType.TextureCubeArray)
        {
            if (desc.Width is 0 || desc.Height is 0 || desc.Width != desc.Height)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             "Cube texture must have equal width and height greater than 0.");
            }

            if (desc.Depth is not 1)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             "Cube texture must have depth of 1.");
            }
        }

        if (arrayTextureTypes.Contains(desc.Type))
        {
            if (desc.Layers is 0)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             "Array texture must have at least 1 layer.");
            }
        }
        else if (desc.Layers is not 1)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Non-array texture must have exactly 1 layer.");
        }

        if (desc.MipLevels is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Texture must have at least 1 mip level.");
        }

        DefinedEnum(desc.SampleCount, "sample count");
    }

    public void TextureViewDesc(TextureViewDesc desc)
    {
        if (desc.Texture?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Texture view must reference a valid texture.");

            return;
        }

        if (desc.FirstLayer >= desc.Texture.Desc.Layers)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Texture view first layer ({desc.FirstLayer}) exceeds texture layer count ({desc.Texture.Desc.Layers}).");
        }

        if (desc.LayerCount is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Texture view must include at least 1 layer.");
        }
        else if (desc.FirstLayer + desc.LayerCount > desc.Texture.Desc.Layers)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Texture view layer range [{desc.FirstLayer}, {desc.FirstLayer + desc.LayerCount}) exceeds texture layer bounds [0, {desc.Texture.Desc.Layers}).");
        }

        if (desc.FirstMipLevel >= desc.Texture.Desc.MipLevels)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Texture view first mip level ({desc.FirstMipLevel}) exceeds texture mip level count ({desc.Texture.Desc.MipLevels}).");
        }

        if (desc.MipLevelCount is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Texture view must include at least 1 mip level.");
        }
        else if (desc.FirstMipLevel + desc.MipLevelCount > desc.Texture.Desc.MipLevels)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Texture view mip level range [{desc.FirstMipLevel}, {desc.FirstMipLevel + desc.MipLevelCount}) exceeds texture mip level bounds [0, {desc.Texture.Desc.MipLevels}).");
        }
    }

    public void SamplerDesc(SamplerDesc desc)
    {
        DefinedEnum(desc.U, "U address mode");

        DefinedEnum(desc.V, "V address mode");

        DefinedEnum(desc.W, "W address mode");

        DefinedEnum(desc.Filter, "filter");

        DefinedEnum(desc.ComparisonFunc, "comparison function");

        if (desc.MaxAnisotropy is not 1 and not 2 and not 4 and not 8 and not 16)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Max anisotropy must be 1, 2, 4, 8, or 16. Got: {desc.MaxAnisotropy}.");
        }

        if (desc.MinLod < 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Min LOD must be non-negative.");
        }

        if (desc.MaxLod < desc.MinLod)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Max LOD ({desc.MaxLod}) must be greater than or equal to Min LOD ({desc.MinLod}).");
        }

        if (desc.LodBias is < -16 or > 16)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"LOD bias must be in range [-16, 16]. Got: {desc.LodBias}.");
        }

        DefinedEnum(desc.BorderColor, "border color");
    }

    public void ResourceLayoutDesc(ResourceLayoutDesc desc)
    {
        if (desc.Elements is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Resource layout elements cannot be null.");

            return;
        }

        if (desc.Elements.Length is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Resource layout must contain at least 1 element.");
        }

        for (int i = 0; i < desc.Elements.Length; i++)
        {
            ResourceElement element = desc.Elements[i];

            DefinedEnum(element.Type, $"resource element type at index {i}");

            if (element.Count is 0)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             $"Resource element at index {i} must have count greater than 0.");
            }
        }
    }

    public void ResourceSetDesc(ResourceSetDesc desc)
    {
        if (desc.Layout?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Resource set must reference a valid resource layout.");

            return;
        }

        if (desc.Resources is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Resource set resources cannot be null.");

            return;
        }

        ResourceType[] types = [.. desc.Layout.Desc.Elements.SelectMany(static item => Enumerable.Repeat(item.Type, (int)item.Count))];

        if (desc.Resources.Length != types.Length)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Resource set requires exactly {types.Length} resources to match layout. Got: {desc.Resources.Length}.");

            return;
        }

        for (int i = 0; i < desc.Resources.Length; i++)
        {
            IBindableResource? resource = desc.Resources[i];

            if (resource?.IsDisposed is not false)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             $"Resource at index {i} must reference a valid, non-disposed resource of type '{types[i]}'.");

                continue;
            }

            switch (types[i])
            {
                case ResourceType.ConstantBuffer:
                case ResourceType.StructuredBuffer:
                case ResourceType.StructuredBufferReadWrite:
                    if (resource is not Buffer or BufferView)
                    {
                        context.PublishDebugCallback(MessageCategory.System,
                                                     MessageSeverity.Error,
                                                     $"Resource at index {i} must be a buffer or buffer view for resource type '{types[i]}'.");
                    }
                    else
                    {
                        ObtainBufferValues((IBuffer)resource,
                                           out _,
                                           out _,
                                           out BufferUsageFlags flags,
                                           $"resource at index {i}");

                        BufferUsageFlags requestedFlag = types[i] switch
                        {
                            ResourceType.ConstantBuffer => BufferUsageFlags.Constant,
                            ResourceType.StructuredBuffer => BufferUsageFlags.ShaderResource,
                            ResourceType.StructuredBufferReadWrite => BufferUsageFlags.UnorderedAccess,
                            _ => BufferUsageFlags.None
                        };

                        if (!flags.HasFlag(requestedFlag))
                        {
                            context.PublishDebugCallback(MessageCategory.System,
                                                         MessageSeverity.Warning,
                                                         $"Resource at index {i} must have usage flag '{requestedFlag}' for resource type '{types[i]}'. Got: {flags}.");
                        }
                    }
                    break;
                case ResourceType.Texture:
                case ResourceType.TextureReadWrite:
                    if (resource is not Texture or TextureView)
                    {
                        context.PublishDebugCallback(MessageCategory.System,
                                                     MessageSeverity.Error,
                                                     $"Resource at index {i} must be a texture or texture view for resource type '{types[i]}'.");
                    }
                    else
                    {
                        ObtainTextureValues((ITexture)resource,
                                            out _,
                                            out _,
                                            out _,
                                            out _,
                                            out _,
                                            out _,
                                            out _,
                                            out _,
                                            out TextureUsageFlags flags,
                                            $"resource at index {i}");

                        TextureUsageFlags requestedFlag = types[i] switch
                        {
                            ResourceType.Texture => TextureUsageFlags.ShaderResource,
                            ResourceType.TextureReadWrite => TextureUsageFlags.UnorderedAccess,
                            _ => TextureUsageFlags.None
                        };

                        if (!flags.HasFlag(requestedFlag))
                        {
                            context.PublishDebugCallback(MessageCategory.System,
                                                         MessageSeverity.Warning,
                                                         $"Resource at index {i} must have usage flag '{requestedFlag}' for resource type '{types[i]}'. Got: {flags}.");
                        }
                    }
                    break;
                case ResourceType.Sampler:
                    if (resource is not Sampler)
                    {
                        context.PublishDebugCallback(MessageCategory.System,
                                                     MessageSeverity.Error,
                                                     $"Resource at index {i} must be a sampler for resource type '{types[i]}'.");
                    }
                    break;
                case ResourceType.AccelerationStructure:
                    if (resource is not TopLevelAccelerationStructure)
                    {
                        context.PublishDebugCallback(MessageCategory.System,
                                                     MessageSeverity.Error,
                                                     $"Resource at index {i} must be a top-level acceleration structure for resource type '{types[i]}'.");
                    }
                    break;
                default:
                    context.PublishDebugCallback(MessageCategory.System,
                                                 MessageSeverity.Error,
                                                 $"Resource type '{types[i]}' at index {i} is not recognized. Valid types: {string.Join(", ", Enum.GetNames<ResourceType>())}.");
                    break;
            }
        }
    }

    public void FrameBufferDesc(FrameBufferDesc desc)
    {
        if (desc.ColorTargets is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Frame buffer color targets cannot be null.");

            return;
        }

        if (desc.ColorTargets.Length is 0 && desc.DepthStencilTarget is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Frame buffer must have at least 1 color target or a depth-stencil target.");

            return;
        }

        if (desc.ColorTargets.Length > 8)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Frame buffer supports up to 8 color targets. Got: {desc.ColorTargets.Length}.");
        }

        uint? targetWidth = null;
        uint? targetHeight = null;
        SampleCount? sampleCount = null;

        for (int i = 0; i < desc.ColorTargets.Length; i++)
        {
            FrameBufferAttachment(desc.ColorTargets[i],
                                          null,
                                          ref targetWidth,
                                          ref targetHeight,
                                          ref sampleCount,
                                          TextureUsageFlags.RenderTarget,
                                          $"color target at index {i}");
        }

        if (desc.DepthStencilTarget is not null)
        {
            FrameBufferAttachment(desc.DepthStencilTarget.Value,
                                          depthStencilFormats,
                                          ref targetWidth,
                                          ref targetHeight,
                                          ref sampleCount,
                                          TextureUsageFlags.DepthStencil,
                                          "depth-stencil target");
        }
    }

    public void ShaderDesc(ShaderDesc desc)
    {
        if (desc.ShaderBytes is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Shader bytecode cannot be null.");

            return;
        }

        if (desc.ShaderBytes.Length is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Shader bytecode cannot be empty.");
        }

        if (string.IsNullOrEmpty(desc.EntryPoint))
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Shader entry point must be specified.");
        }

        DefinedEnum(desc.Stage, "shader stage");
    }

    public void GraphicsPipelineDesc(GraphicsPipelineDesc desc)
    {
        // RenderStates
        {
            RenderStates renderStates = desc.RenderStates;

            // RasterizerState
            {
                RasterizerState rasterizerState = renderStates.RasterizerState;

                DefinedEnum(rasterizerState.CullMode, "cull mode");

                DefinedEnum(rasterizerState.FillMode, "fill mode");

                DefinedEnum(rasterizerState.FrontFace, "front face");

                if (rasterizerState.DepthBias < 0)
                {
                    context.PublishDebugCallback(MessageCategory.System,
                                                 MessageSeverity.Warning,
                                                 "Depth bias is negative. This may cause rendering artifacts.");
                }

                if (rasterizerState.DepthBiasClamp < 0)
                {
                    context.PublishDebugCallback(MessageCategory.System,
                                                 MessageSeverity.Warning,
                                                 "Depth bias clamp is negative. This may cause rendering artifacts.");
                }

                if (rasterizerState.SlopeScaledDepthBias < 0)
                {
                    context.PublishDebugCallback(MessageCategory.System,
                                                 MessageSeverity.Warning,
                                                 "Slope scaled depth bias is negative. This may cause rendering artifacts.");
                }
            }

            // DepthStencilState
            {
                DepthStencilState depthStencilState = renderStates.DepthStencilState;

                DefinedEnum(depthStencilState.DepthFunc, "depth function");

                DepthStencilStateOp(depthStencilState.FrontFace, "front face");

                DepthStencilStateOp(depthStencilState.BackFace, "back face");
            }

            // BlendState
            {
                BlendState blendState = renderStates.BlendState;

                BlendStateRenderTarget[] renderTargets = blendState.IndependentBlendEnable ? [blendState.RenderTarget0, blendState.RenderTarget1, blendState.RenderTarget2, blendState.RenderTarget3, blendState.RenderTarget4, blendState.RenderTarget5, blendState.RenderTarget6, blendState.RenderTarget7] : [blendState.RenderTarget0];

                for (int i = 0; i < renderTargets.Length; i++)
                {
                    BlendStateRenderTarget(renderTargets[i], $"render target at index {i}");
                }
            }
        }

        // Shaders
        {
            GraphicsShaders shaders = desc.Shaders;

            Object(shaders.Vertex, false, "Vertex shader");

            Object(shaders.Hull, true, "Hull shader");

            Object(shaders.Domain, true, "Domain shader");

            Object(shaders.Geometry, true, "Geometry shader");

            Object(shaders.Pixel, false, "Pixel shader");
        }

        // InputLayouts
        {
            if (desc.InputLayouts is null)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             "Input layouts cannot be null.");

                return;
            }

            if (desc.InputLayouts.Length is 0)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             "Graphics pipeline must have at least 1 input layout.");
            }

            for (int i = 0; i < desc.InputLayouts.Length; i++)
            {
                InputLayout(desc.InputLayouts[i], $"input layout at index {i}");
            }
        }

        Objects(desc.ResourceLayouts, "resource layouts");

        Output(desc.Outputs);
    }

    public void ComputePipelineDesc(ComputePipelineDesc desc)
    {
        Object(desc.Shader, false, "Compute shader");

        Objects(desc.ResourceLayouts, "resource layouts");
    }

    public void RayTracingPipelineDesc(RayTracingPipelineDesc desc)
    {
        // Shaders
        {
            RayTracingShaders shaders = desc.Shaders;

            Object(shaders.RayGeneration, false, "Ray generation shader");

            Objects(shaders.Miss, "Miss shaders");

            Objects(shaders.AnyHit, "Any-hit shaders");

            Objects(shaders.Intersection, "Intersection shaders");

            Objects(shaders.ClosestHit, "Closest-hit shaders");
        }

        if (desc.HitGroups is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Ray tracing pipeline hit groups cannot be null.");
        }
        else
        {
            for (int i = 0; i < desc.HitGroups.Length; i++)
            {
                HitGroup hitGroup = desc.HitGroups[i];

                DefinedEnum(hitGroup.Type, $"hit group type at index {i}");

                if (string.IsNullOrEmpty(hitGroup.Name))
                {
                    context.PublishDebugCallback(MessageCategory.System,
                                                 MessageSeverity.Error,
                                                 $"Hit group at index {i} must have a name.");
                }
            }

            string[] hitGroupNames = [.. desc.HitGroups.Select(static item => item.Name)];

            if (hitGroupNames.Distinct().Count() != hitGroupNames.Length)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             "Hit group names must be unique.");
            }
        }

        Objects(desc.ResourceLayouts, "resource layouts");

        if (desc.MaxTraceRecursionDepth > 31)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Max trace recursion depth must not exceed 31. Got: {desc.MaxTraceRecursionDepth}.");
        }

        if (desc.MaxPayloadSizeInBytes % 4 is not 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Max payload size must be a multiple of 4 bytes. Got: {desc.MaxPayloadSizeInBytes}.");
        }

        if (desc.MaxAttributeSizeInBytes % 4 is not 0 or > 32)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Max attribute size must be a multiple of 4 bytes and not exceed 32 bytes. Got: {desc.MaxAttributeSizeInBytes}.");
        }
    }

    private void Surface(Surface surface)
    {
        DefinedEnum(surface.Type, "surface type");

        if (surface.Handles is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Surface handles cannot be null.");

            return;
        }

        if (surface.Type is SurfaceType.Win32 && surface.Handles.Length is not 1)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Win32 surface requires exactly 1 handle (HWND).");
        }
        else if (surface.Type is SurfaceType.Wayland && surface.Handles.Length is not 2)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Wayland surface requires exactly 2 handles (display and surface).");
        }
        else if (surface.Type is SurfaceType.Xlib && surface.Handles.Length is not 2)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Xlib surface requires exactly 2 handles (display and window).");
        }
        else if (surface.Type is SurfaceType.Android && surface.Handles.Length is not 1)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Android surface requires exactly 1 handle (native window).");
        }
        else if (surface.Type is SurfaceType.IOS && surface.Handles.Length is not 1)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "iOS surface requires exactly 1 handle (view).");
        }
        else if (surface.Type is SurfaceType.MacOS && surface.Handles.Length is not 1)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "MacOS surface requires exactly 1 handle (view).");
        }

        if (surface.Handles.Any(static item => item is 0))
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Surface handles cannot contain null pointers.");
        }

        if (surface.Width is 0 || surface.Height is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Surface dimensions must be greater than 0.");
        }
    }

    private void FrameBufferAttachment(FrameBufferAttachment attachment,
                                       PixelFormat[]? targetFormats,
                                       ref uint? targetWidth,
                                       ref uint? targetHeight,
                                       ref SampleCount? targetSampleCount,
                                       TextureUsageFlags targetFlag,
                                       string name)
    {
        if (attachment.Target?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Frame buffer {name} must reference a valid texture.");

            return;
        }

        ObtainTextureValues(attachment.Target,
                            out TextureType type,
                            out PixelFormat format,
                            out uint width,
                            out uint height,
                            out _,
                            out uint layers,
                            out uint mipLevels,
                            out SampleCount sampleCount,
                            out TextureUsageFlags flags,
                            name);

        if (type is not TextureType.Texture2D or TextureType.Texture2DArray)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Frame buffer {name} must use Texture2D or Texture2DArray. Got: {type}.");
        }

        if (targetFormats?.Contains(format) is false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Frame buffer {name} texture format '{format}' is not supported. Valid formats: {string.Join(", ", targetFormats.Select(static item => item.ToString()))}.");
        }

        if (targetWidth.HasValue && targetWidth.Value != width)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Frame buffer {name} texture width ({width}) does not match expected width ({targetWidth.Value}).");
        }
        else
        {
            targetWidth = width;
        }

        if (targetHeight.HasValue && targetHeight.Value != height)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Frame buffer {name} texture height ({height}) does not match expected height ({targetHeight.Value}).");
        }
        else
        {
            targetHeight = height;
        }

        if (targetSampleCount.HasValue && targetSampleCount.Value != sampleCount)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Frame buffer {name} texture sample count ({sampleCount}) does not match expected sample count ({targetSampleCount.Value}).");
        }
        else
        {
            targetSampleCount = sampleCount;
        }

        if (!flags.HasFlag(targetFlag))
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Frame buffer {name} texture must have usage flag '{targetFlag}'. Got: {flags}.");
        }

        TextureSlice(type, layers, mipLevels, attachment.Slice, name);
    }

    private void DepthStencilStateOp(DepthStencilStateOp stateOp, string name)
    {
        DefinedEnum(stateOp.StencilFailOp, $"{name} stencil fail operation");

        DefinedEnum(stateOp.StencilDepthFailOp, $"{name} stencil depth fail operation");

        DefinedEnum(stateOp.StencilPassOp, $"{name} stencil pass operation");

        DefinedEnum(stateOp.StencilFunc, $"{name} stencil function");
    }

    private void BlendStateRenderTarget(BlendStateRenderTarget renderTarget, string name)
    {
        DefinedEnum(renderTarget.SrcBlend, $"{name} source blend");

        DefinedEnum(renderTarget.DestBlend, $"{name} destination blend");

        DefinedEnum(renderTarget.BlendOp, $"{name} blend operation");

        DefinedEnum(renderTarget.SrcBlendAlpha, $"{name} source alpha blend");

        DefinedEnum(renderTarget.DestBlendAlpha, $"{name} destination alpha blend");

        DefinedEnum(renderTarget.BlendOpAlpha, $"{name} alpha blend operation");
    }

    private void InputLayout(InputLayout inputLayout, string name)
    {
        if (inputLayout.Elements is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name} elements cannot be null.");

            return;
        }

        if (inputLayout.Elements.Length is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name} must contain at least 1 element.");
        }

        for (int i = 0; i < inputLayout.Elements.Length; i++)
        {
            InputElement inputElement = inputLayout.Elements[i];

            DefinedEnum(inputElement.Format, $"input element format at index {i}");

            DefinedEnum(inputElement.Semantic, $"input element semantic at index {i}");
        }

        if (inputLayout.StrideInBytes is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name} stride must be greater than 0.");
        }
    }

    private void Output(Output output)
    {
        if (output.ColorAttachments is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Output color attachments cannot be null.");

            return;
        }

        if (output.ColorAttachments.Length is 0 && output.DepthStencilAttachment is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Output must have at least 1 color attachment or a depth-stencil attachment.");

            return;
        }

        if (output.ColorAttachments.Length > 8)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Output supports up to 8 color attachments. Got: {output.ColorAttachments.Length}.");
        }

        if (output.DepthStencilAttachment is not null && !depthStencilFormats.Contains(output.DepthStencilAttachment.Value))
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Depth-stencil attachment format '{output.DepthStencilAttachment.Value}' is not supported. Valid formats: {string.Join(", ", depthStencilFormats.Select(static item => item.ToString()))}.");
        }
    }
}
