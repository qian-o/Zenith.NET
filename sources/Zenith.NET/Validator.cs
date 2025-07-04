using System.Runtime.CompilerServices;

namespace Zenith.NET;

internal class Validator(GraphicsContext context)
{
    private static readonly PixelFormat[] swapChainFormats =
    [
        PixelFormat.R8G8B8A8UNorm,
        PixelFormat.R8G8B8A8UNormSRgb,
        PixelFormat.R16G16B16A16Float,
        PixelFormat.B8G8R8A8UNorm,
        PixelFormat.B8G8R8A8UNormSRgb
    ];

    private static readonly PixelFormat[] depthStencilFormats =
    [
        PixelFormat.D24UNormS8UInt,
        PixelFormat.D32FloatS8UInt
    ];

    private static readonly TextureType[] arrayTextureTypes =
    [
        TextureType.Texture1DArray,
        TextureType.Texture2DArray,
        TextureType.TextureCubeArray
    ];

    #region ResourceFactory
    public void ValidateSwapChainDesc(SwapChainDesc desc)
    {
        ValidateSurface(desc.Surface);

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

    public void ValidateBufferDesc(BufferDesc desc)
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

    public void ValidateBufferViewDesc(BufferViewDesc desc)
    {
        if (desc.Buffer?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Buffer view requires a valid, non-disposed buffer.");

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

    public void ValidateTextureDesc(TextureDesc desc)
    {
        ValidateDefinedEnum(desc.Type, "texture type");

        ValidateDefinedEnum(desc.Format, "texture format");

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

        ValidateDefinedEnum(desc.SampleCount, "sample count");
    }

    public void ValidateTextureViewDesc(TextureViewDesc desc)
    {
        if (desc.Texture?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Texture view requires a valid, non-disposed texture.");

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

    public void ValidateSamplerDesc(SamplerDesc desc)
    {
        ValidateDefinedEnum(desc.U, "U address mode");

        ValidateDefinedEnum(desc.V, "V address mode");

        ValidateDefinedEnum(desc.W, "W address mode");

        ValidateDefinedEnum(desc.Filter, "filter");

        ValidateDefinedEnum(desc.ComparisonFunc, "comparison function");

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

        ValidateDefinedEnum(desc.BorderColor, "border color");
    }

    public void ValidateResourceLayoutDesc(ResourceLayoutDesc desc)
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

            ValidateDefinedEnum(element.Type, $"resource element type at index {i}");

            if (element.Count is 0)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             $"Resource element at index {i} must have count greater than 0.");
            }
        }
    }

    public void ValidateResourceSetDesc(ResourceSetDesc desc)
    {
        if (desc.Layout?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Resource set requires a valid, non-disposed resource layout.");

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
                                             $"Resource at index {i} must be valid and non-disposed.");

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

    public void ValidateFrameBufferDesc(FrameBufferDesc desc)
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
            ValidateFrameBufferAttachment(desc.ColorTargets[i],
                                          null,
                                          ref targetWidth,
                                          ref targetHeight,
                                          ref sampleCount,
                                          TextureUsageFlags.RenderTarget,
                                          $"color target at index {i}");
        }

        if (desc.DepthStencilTarget is not null)
        {
            ValidateFrameBufferAttachment(desc.DepthStencilTarget.Value,
                                          depthStencilFormats,
                                          ref targetWidth,
                                          ref targetHeight,
                                          ref sampleCount,
                                          TextureUsageFlags.DepthStencil,
                                          "depth-stencil target");
        }
    }

    public void ValidateShaderDesc(ShaderDesc desc)
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

        ValidateDefinedEnum(desc.Stage, "shader stage");
    }

    public void ValidateGraphicsPipelineDesc(GraphicsPipelineDesc desc)
    {
        // RenderStates
        {
            RenderStates renderStates = desc.RenderStates;

            // RasterizerState
            {
                RasterizerState rasterizerState = renderStates.RasterizerState;

                ValidateDefinedEnum(rasterizerState.CullMode, "cull mode");

                ValidateDefinedEnum(rasterizerState.FillMode, "fill mode");

                ValidateDefinedEnum(rasterizerState.FrontFace, "front face");

                if (rasterizerState.DepthBias < 0)
                {
                    context.PublishDebugCallback(MessageCategory.System,
                                                 MessageSeverity.Error,
                                                 "Depth bias must be non-negative.");
                }

                if (rasterizerState.DepthBiasClamp < 0)
                {
                    context.PublishDebugCallback(MessageCategory.System,
                                                 MessageSeverity.Error,
                                                 "Depth bias clamp must be non-negative.");
                }

                if (rasterizerState.SlopeScaledDepthBias < 0)
                {
                    context.PublishDebugCallback(MessageCategory.System,
                                                 MessageSeverity.Error,
                                                 "Slope scaled depth bias must be non-negative.");
                }
            }

            // DepthStencilState
            {
                DepthStencilState depthStencilState = renderStates.DepthStencilState;

                ValidateDefinedEnum(depthStencilState.DepthFunc, "depth function");

                ValidateDepthStencilStateOp(depthStencilState.FrontFace, "front face");

                ValidateDepthStencilStateOp(depthStencilState.BackFace, "back face");
            }

            // BlendState
            {
                BlendState blendState = renderStates.BlendState;

                BlendStateRenderTarget[] renderTargets = blendState.IndependentBlendEnable ? [blendState.RenderTarget0, blendState.RenderTarget1, blendState.RenderTarget2, blendState.RenderTarget3, blendState.RenderTarget4, blendState.RenderTarget5, blendState.RenderTarget6, blendState.RenderTarget7] : [blendState.RenderTarget0];

                for (int i = 0; i < renderTargets.Length; i++)
                {
                    ValidateBlendStateRenderTarget(renderTargets[i], $"render target at index {i}");
                }
            }
        }

        // Shaders
        {
            GraphicsShaders shaders = desc.Shaders;

            ValidateShader(shaders.Vertex, "Vertex shader");

            if (shaders.Hull is not null)
            {
                ValidateShader(shaders.Hull, "Hull shader");
            }

            if (shaders.Domain is not null)
            {
                ValidateShader(shaders.Domain, "Domain shader");
            }

            if (shaders.Geometry is not null)
            {
                ValidateShader(shaders.Geometry, "Geometry shader");
            }

            ValidateShader(shaders.Pixel, "Pixel shader");
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
                ValidateInputLayout(desc.InputLayouts[i], $"input layout at index {i}");
            }
        }

        ValidateResourceLayouts(desc.ResourceLayouts);

        ValidateOutput(desc.Outputs);
    }

    public void ValidateComputePipelineDesc(ComputePipelineDesc desc)
    {
        ValidateShader(desc.Shader, "Compute shader");

        ValidateResourceLayouts(desc.ResourceLayouts);
    }

    public void ValidateRayTracingPipelineDesc(RayTracingPipelineDesc desc)
    {
        // Shaders
        {
            RayTracingShaders shaders = desc.Shaders;

            ValidateShader(shaders.RayGeneration, "Ray generation shader");

            ValidateShaders(shaders.Miss, "Miss shaders");

            ValidateShaders(shaders.AnyHit, "Any-hit shaders");

            ValidateShaders(shaders.Intersection, "Intersection shaders");

            ValidateShaders(shaders.ClosestHit, "Closest-hit shaders");
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

                ValidateDefinedEnum(hitGroup.Type, $"hit group type at index {i}");

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

        ValidateResourceLayouts(desc.ResourceLayouts);

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

    private void ValidateSurface(Surface surface)
    {
        ValidateDefinedEnum(surface.Type, "surface type");

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

    private void ValidateFrameBufferAttachment(FrameBufferAttachment attachment,
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
                                         $"Frame buffer {name} requires a valid, non-disposed texture.");

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

        ValidateTextureSlice(type, layers, mipLevels, attachment.Slice, name);
    }

    private void ValidateDepthStencilStateOp(DepthStencilStateOp stateOp, string name)
    {
        ValidateDefinedEnum(stateOp.StencilFailOp, $"{name} stencil fail operation");

        ValidateDefinedEnum(stateOp.StencilDepthFailOp, $"{name} stencil depth fail operation");

        ValidateDefinedEnum(stateOp.StencilPassOp, $"{name} stencil pass operation");

        ValidateDefinedEnum(stateOp.StencilFunc, $"{name} stencil function");
    }

    private void ValidateBlendStateRenderTarget(BlendStateRenderTarget renderTarget, string name)
    {
        ValidateDefinedEnum(renderTarget.SrcBlend, $"{name} source blend");

        ValidateDefinedEnum(renderTarget.DestBlend, $"{name} destination blend");

        ValidateDefinedEnum(renderTarget.BlendOp, $"{name} blend operation");

        ValidateDefinedEnum(renderTarget.SrcBlendAlpha, $"{name} source alpha blend");

        ValidateDefinedEnum(renderTarget.DestBlendAlpha, $"{name} destination alpha blend");

        ValidateDefinedEnum(renderTarget.BlendOpAlpha, $"{name} alpha blend operation");
    }

    private void ValidateShaders(Shader[]? shaders, string name)
    {
        if (shaders is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name} cannot be null.");

            return;
        }

        for (int i = 0; i < shaders.Length; i++)
        {
            ValidateShader(shaders[i], $"{name} at index {i}");
        }
    }

    private void ValidateShader(Shader? shader, string name)
    {
        if (shader?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name} must be valid and non-disposed.");
        }
    }

    private void ValidateInputLayout(InputLayout inputLayout, string name)
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

            ValidateDefinedEnum(inputElement.Format, $"input element format at index {i}");

            ValidateDefinedEnum(inputElement.Semantic, $"input element semantic at index {i}");
        }

        if (inputLayout.StrideInBytes is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name} stride must be greater than 0.");
        }
    }

    private void ValidateResourceLayouts(ResourceLayout[]? resourceLayouts)
    {
        if (resourceLayouts is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Resource layouts cannot be null.");

            return;
        }

        for (int i = 0; i < resourceLayouts.Length; i++)
        {
            if (resourceLayouts[i]?.IsDisposed is not false)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             $"Resource layout at index {i} must be valid and non-disposed.");
            }
        }
    }

    private void ValidateOutput(Output output)
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
    #endregion

    #region CommandBuffer Validation
    public void ValidateBegin(CommandBuffer commandBuffer)
    {
        if (commandBuffer.State is not CommandBufferState.Idle)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Command buffer must be in Idle state to begin recording. Current state: {commandBuffer.State}.");
        }
    }

    public void ValidateEnd(CommandBuffer commandBuffer)
    {
        if (commandBuffer.State is not CommandBufferState.Recording)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Command buffer must be in Recording state to end recording. Current state: {commandBuffer.State}.");
        }
    }

    public void ValidateSubmit(CommandBuffer commandBuffer)
    {
        if (commandBuffer.State is not CommandBufferState.Completed)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Command buffer must be in Completed state to submit. Current state: {commandBuffer.State}.");
        }
    }

    public void ValidateUploadBuffer<T>(CommandBuffer commandBuffer,
                                        IBuffer buffer,
                                        uint offsetInBytes,
                                        ReadOnlySpan<T> data)
    {
        ValidateRecordingState(commandBuffer, "UploadBuffer");

        if (buffer.IsDisposed)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Cannot upload to a disposed buffer.");

            return;
        }

        if (data.IsEmpty)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Upload data cannot be empty.");

            return;
        }

        ObtainBufferValues(buffer,
                           out uint sizeInBytes,
                           out _,
                           out _,
                           "buffer for upload");

        uint requestedSize = offsetInBytes + (uint)(data.Length * Unsafe.SizeOf<T>());

        if (requestedSize > sizeInBytes)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Upload size ({requestedSize} bytes) exceeds buffer size ({sizeInBytes} bytes).");
        }
    }

    public void ValidateCopyBuffer(CommandBuffer commandBuffer,
                                   IBuffer src,
                                   uint srcOffsetInBytes,
                                   IBuffer dest,
                                   uint destOffsetInBytes,
                                   uint sizeInBytes)
    {
        ValidateRecordingState(commandBuffer, "CopyBuffer");

        if (src.IsDisposed || dest.IsDisposed)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Cannot copy to or from a disposed buffer.");

            return;
        }

        if (sizeInBytes is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Copy size must be greater than 0.");

            return;
        }

        ObtainBufferValues(src,
                           out uint srcSizeInBytes,
                           out _,
                           out _,
                           "source buffer for copy");

        ObtainBufferValues(dest,
                           out uint destSizeInBytes,
                           out _,
                           out _,
                           "destination buffer for copy");

        if (srcOffsetInBytes + sizeInBytes > srcSizeInBytes)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Source buffer copy range exceeds source buffer size. Source size: {srcSizeInBytes} bytes, requested range: {srcOffsetInBytes} + {sizeInBytes} bytes.");
        }

        if (destOffsetInBytes + sizeInBytes > destSizeInBytes)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Destination buffer copy range exceeds destination buffer size. Destination size: {destSizeInBytes} bytes, requested range: {destOffsetInBytes} + {sizeInBytes} bytes.");
        }
    }

    public void ValidateUploadTexture<T>(CommandBuffer commandBuffer,
                                         ITexture texture,
                                         TextureSlice slice,
                                         TextureOffset offset,
                                         TextureExtent extent,
                                         ReadOnlySpan<T> data)
    {
        ValidateRecordingState(commandBuffer, "UploadTexture");

        if (texture.IsDisposed)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Cannot upload to a disposed texture.");

            return;
        }

        if (extent.Width is 0 || extent.Height is 0 || extent.Depth is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Texture extent must have non-zero width, height, and depth.");

            return;
        }

        if (data.IsEmpty)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Upload data cannot be empty.");

            return;
        }

        ObtainTextureValues(texture,
                            out TextureType type,
                            out _,
                            out uint width,
                            out uint height,
                            out uint depth,
                            out uint layers,
                            out uint mipLevels,
                            out _,
                            out _,
                            "texture for upload");

        ValidateTextureSlice(type, layers, mipLevels, slice, "texture slice for upload");

        ValidateTextureRange(width, height, depth, offset, extent, "texture offset and extent for upload");
    }

    public void ValidateCopyTexture(CommandBuffer commandBuffer,
                                    IBuffer src,
                                    uint srcOffsetInBytes,
                                    uint srcSizeInBytes,
                                    ITexture dest,
                                    TextureSlice destSlice,
                                    TextureOffset destOffset,
                                    TextureExtent destExtent)
    {
        ValidateRecordingState(commandBuffer, "CopyTexture");

        if (src.IsDisposed || dest.IsDisposed)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Cannot copy to or from a disposed resource.");

            return;
        }

        if (srcSizeInBytes is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Source size must be greater than 0.");

            return;
        }

        if (destExtent.Width is 0 || destExtent.Height is 0 || destExtent.Depth is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Destination texture extent must have non-zero width, height, and depth.");

            return;
        }

        ObtainBufferValues(src,
                           out uint srcBufferSizeInBytes,
                           out _,
                           out _,
                           "source buffer for copy");

        ObtainTextureValues(dest,
                            out TextureType type,
                            out _,
                            out uint width,
                            out uint height,
                            out uint depth,
                            out uint layers,
                            out uint mipLevels,
                            out _,
                            out _,
                            "destination texture for copy");

        if (srcOffsetInBytes + srcSizeInBytes > srcBufferSizeInBytes)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Source buffer copy range exceeds source buffer size. Source size: {srcBufferSizeInBytes} bytes, requested range: {srcOffsetInBytes} + {srcSizeInBytes} bytes.");
        }

        ValidateTextureSlice(type, layers, mipLevels, destSlice, "destination texture slice for copy");

        ValidateTextureRange(width, height, depth, destOffset, destExtent, "destination texture offset and extent for copy");
    }

    public void ValidateCopyTexture(CommandBuffer commandBuffer,
                                    ITexture src,
                                    TextureSlice srcSlice,
                                    TextureOffset srcOffset,
                                    ITexture dest,
                                    TextureSlice destSlice,
                                    TextureOffset destOffset,
                                    TextureExtent extent)
    {
        ValidateRecordingState(commandBuffer, "CopyTexture");

        if (src.IsDisposed || dest.IsDisposed)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Cannot copy to or from a disposed resource.");

            return;
        }

        if (extent.Width is 0 || extent.Height is 0 || extent.Depth is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Texture extent must have non-zero width, height, and depth.");

            return;
        }

        ObtainTextureValues(src,
                            out TextureType srcType,
                            out _,
                            out uint srcWidth,
                            out uint srcHeight,
                            out uint srcDepth,
                            out uint srcLayers,
                            out uint srcMipLevels,
                            out _,
                            out _,
                            "source texture for copy");

        ObtainTextureValues(dest,
                            out TextureType destType,
                            out _,
                            out uint destWidth,
                            out uint destHeight,
                            out uint destDepth,
                            out uint destLayers,
                            out uint destMipLevels,
                            out _,
                            out _,
                            "destination texture for copy");

        ValidateTextureSlice(srcType, srcLayers, srcMipLevels, srcSlice, "source texture slice for copy");

        ValidateTextureRange(srcWidth, srcHeight, srcDepth, srcOffset, extent, "source texture offset and extent for copy");

        ValidateTextureSlice(destType, destLayers, destMipLevels, destSlice, "destination texture slice for copy");

        ValidateTextureRange(destWidth, destHeight, destDepth, destOffset, extent, "destination texture offset and extent for copy");
    }

    private void ValidateRecordingState(CommandBuffer commandBuffer, string name)
    {
        if (commandBuffer.State is not CommandBufferState.Recording)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name}: Command buffer must be in Recording state to perform this operation. Current state: {commandBuffer.State}.");
        }
    }
    #endregion

    #region Universal Validation
    private void ValidateDefinedEnum<TEnum>(TEnum value, string name) where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Invalid {name} value '{value}'. Valid values: {string.Join(", ", Enum.GetNames<TEnum>())}.");
        }
    }
    #endregion

    #region Universal Buffer Validation
    private void ObtainBufferValues(IBuffer iBuffer,
                                    out uint sizeInBytes,
                                    out uint strideInBytes,
                                    out BufferUsageFlags flags,
                                    string name)
    {
        if (iBuffer is Buffer buffer)
        {
            sizeInBytes = buffer.Desc.SizeInBytes;
            strideInBytes = buffer.Desc.StrideInBytes;
            flags = buffer.Desc.Flags;
        }
        else if (iBuffer is BufferView bufferView)
        {
            sizeInBytes = bufferView.Desc.SizeInBytes;
            strideInBytes = bufferView.Desc.StrideInBytes;
            flags = bufferView.Desc.Buffer.Desc.Flags;
        }
        else
        {
            sizeInBytes = default;
            strideInBytes = default;
            flags = default;

            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Cannot validate buffer slice for {name}: expected Buffer or BufferView.");
        }
    }
    #endregion

    #region Universal Texture Validation
    private void ObtainTextureValues(ITexture iTexture,
                                     out TextureType type,
                                     out PixelFormat format,
                                     out uint width,
                                     out uint height,
                                     out uint depth,
                                     out uint layers,
                                     out uint mipLevels,
                                     out SampleCount sampleCount,
                                     out TextureUsageFlags flags,
                                     string name)
    {
        if (iTexture is Texture texture)
        {
            type = texture.Desc.Type;
            format = texture.Desc.Format;
            width = texture.Desc.Width;
            height = texture.Desc.Height;
            depth = texture.Desc.Depth;
            layers = texture.Desc.Layers;
            mipLevels = texture.Desc.MipLevels;
            sampleCount = texture.Desc.SampleCount;
            flags = texture.Desc.Flags;
        }
        else if (iTexture is TextureView textureView)
        {
            type = textureView.Desc.Texture.Desc.Type;
            format = textureView.Desc.Texture.Desc.Format;
            width = textureView.Desc.Texture.Desc.Width;
            height = textureView.Desc.Texture.Desc.Height;
            depth = textureView.Desc.Texture.Desc.Depth;
            layers = textureView.Desc.LayerCount;
            mipLevels = textureView.Desc.MipLevelCount;
            sampleCount = textureView.Desc.Texture.Desc.SampleCount;
            flags = textureView.Desc.Texture.Desc.Flags;
        }
        else
        {
            type = default;
            format = default;
            width = default;
            height = default;
            depth = default;
            layers = default;
            mipLevels = default;
            sampleCount = default;
            flags = default;

            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Cannot validate texture slice for {name}: expected Texture or TextureView.");
        }
    }

    private void ValidateTextureSlice(TextureType type,
                                      uint layers,
                                      uint mipLevels,
                                      TextureSlice slice,
                                      string name)
    {
        if (type is TextureType.TextureCube or TextureType.TextureCubeArray)
        {
            if (slice.Face >= 6)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             $"Face index {slice.Face} is invalid for {name}. Cube textures have 6 faces (0-5).");
            }
        }
        else if (slice.Face is not 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Face index must be 0 for non-cube texture {name}.");
        }

        if (arrayTextureTypes.Contains(type))
        {
            if (slice.Layer >= layers)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             $"Layer index {slice.Layer} exceeds layer count ({layers}) for {name}.");
            }
        }
        else if (slice.Layer is not 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Layer index must be 0 for non-array texture {name}.");
        }

        if (slice.MipLevel >= mipLevels)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Mip level {slice.MipLevel} exceeds mip level count ({mipLevels}) for {name}.");
        }
    }

    private void ValidateTextureRange(uint width,
                                      uint height,
                                      uint depth,
                                      TextureOffset offset,
                                      TextureExtent extent,
                                      string name)
    {
        if (offset.X + extent.Width > width)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Offset X ({offset.X}) + extent width ({extent.Width}) exceeds texture width ({width}) for {name}.");
        }

        if (offset.Y + extent.Height > height)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Offset Y ({offset.Y}) + extent height ({extent.Height}) exceeds texture height ({height}) for {name}.");
        }

        if (offset.Z + extent.Depth > depth)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Offset Z ({offset.Z}) + extent depth ({extent.Depth}) exceeds texture depth ({depth}) for {name}.");
        }
    }
    #endregion
}