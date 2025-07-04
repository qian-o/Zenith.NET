using System.Numerics;
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
        if (!ValidateObject(desc.Buffer, "buffer"))
        {
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
        if (!ValidateObject(desc.Texture, "texture"))
        {
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
        if (!ValidateObject(desc.Layout, "resource layout"))
        {
            return;
        }

        if (!ValidateObjects(desc.Resources, "resource set resources"))
        {
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
            IBindableResource resource = desc.Resources[i];

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

            ValidateObject(shaders.Vertex, "Vertex shader");

            if (shaders.Hull is not null)
            {
                ValidateObject(shaders.Hull, "Hull shader");
            }

            if (shaders.Domain is not null)
            {
                ValidateObject(shaders.Domain, "Domain shader");
            }

            if (shaders.Geometry is not null)
            {
                ValidateObject(shaders.Geometry, "Geometry shader");
            }

            ValidateObject(shaders.Pixel, "Pixel shader");
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

        ValidateObjects(desc.ResourceLayouts, "resource layouts");

        ValidateOutput(desc.Outputs);
    }

    public void ValidateComputePipelineDesc(ComputePipelineDesc desc)
    {
        ValidateObject(desc.Shader, "Compute shader");

        ValidateObjects(desc.ResourceLayouts, "resource layouts");
    }

    public void ValidateRayTracingPipelineDesc(RayTracingPipelineDesc desc)
    {
        // Shaders
        {
            RayTracingShaders shaders = desc.Shaders;

            ValidateObject(shaders.RayGeneration, "Ray generation shader");

            ValidateObjects(shaders.Miss, "Miss shaders");

            ValidateObjects(shaders.AnyHit, "Any-hit shaders");

            ValidateObjects(shaders.Intersection, "Intersection shaders");

            ValidateObjects(shaders.ClosestHit, "Closest-hit shaders");
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

        ValidateObjects(desc.ResourceLayouts, "resource layouts");

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
        if (!ValidateObject(attachment.Target, name))
        {
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
                                         $"Command buffer can only be started when in Idle state. Current state: {commandBuffer.State}.");
        }
    }

    public void ValidateEnd(CommandBuffer commandBuffer)
    {
        if (commandBuffer.State is not CommandBufferState.Recording)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Command buffer can only be ended when in Recording state. Current state: {commandBuffer.State}.");
        }
    }

    public void ValidateSubmit(CommandBuffer commandBuffer)
    {
        if (commandBuffer.State is not CommandBufferState.Completed)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Command buffer can only be submitted when in Completed state. Current state: {commandBuffer.State}.");
        }
    }

    public void ValidateUploadBuffer<T>(CommandBuffer commandBuffer,
                                        IBuffer buffer,
                                        uint offsetInBytes,
                                        ReadOnlySpan<T> data)
    {
        ValidateRecordingState(commandBuffer, "UploadBuffer");

        if (!ValidateObject(buffer, "buffer for upload"))
        {
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

        if (!ValidateObject(src, "buffer for copy source") || !ValidateObject(dest, "buffer for copy destination"))
        {
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

        if (!ValidateObject(texture, "texture for upload"))
        {
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

        if (!ValidateObject(src, "source buffer for copy") || !ValidateObject(dest, "destination texture for copy"))
        {
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

        if (!ValidateObject(src, "source texture for copy") || !ValidateObject(dest, "destination texture for copy"))
        {
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

    public void ValidateResolveTexture(CommandBuffer commandBuffer,
                                       ITexture src,
                                       TextureSlice srcSlice,
                                       ITexture dest,
                                       TextureSlice destSlice)
    {
        ValidateDirectQueue(commandBuffer, "ResolveTexture");

        ValidateRecordingState(commandBuffer, "ResolveTexture");

        if (!ValidateObject(src, "source texture for resolve") || !ValidateObject(dest, "destination texture for resolve"))
        {
            return;
        }

        ObtainTextureValues(src,
                            out TextureType srcType,
                            out _,
                            out _,
                            out _,
                            out _,
                            out uint srcLayers,
                            out uint srcMipLevels,
                            out SampleCount srcSampleCount,
                            out _,
                            "source texture for resolve");

        ObtainTextureValues(dest,
                            out TextureType destType,
                            out _,
                            out _,
                            out _,
                            out _,
                            out uint destLayers,
                            out uint destMipLevels,
                            out SampleCount destSampleCount,
                            out _,
                            "destination texture for resolve");

        ValidateTextureSlice(srcType, srcLayers, srcMipLevels, srcSlice, "source texture slice for resolve");

        ValidateTextureSlice(destType, destLayers, destMipLevels, destSlice, "destination texture slice for resolve");

        if (srcSampleCount is SampleCount.Count1)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Source texture for resolve must have a sample count greater than 1.");
        }

        if (destSampleCount is not SampleCount.Count1)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Destination texture for resolve must have a sample count of 1.");
        }
    }

    public void ValidateBuildBottomLevelAccelerationStructure(CommandBuffer commandBuffer,
                                                              BottomLevelAccelerationStructureDesc desc)
    {
        ValidateNotCopyQueue(commandBuffer, "BuildBottomLevelAccelerationStructure");

        ValidateRecordingState(commandBuffer, "BuildBottomLevelAccelerationStructure");

        if (desc.Geometries is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Bottom-level acceleration structure geometries cannot be null.");

            return;
        }

        if (desc.Geometries.Length is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Bottom-level acceleration structure must have at least 1 geometry.");
        }

        for (int i = 0; i < desc.Geometries.Length; i++)
        {
            ValidateRayTracingGeometry(desc.Geometries[i], $"geometry at index {i}");
        }
    }

    public void ValidateBuildTopLevelAccelerationStructure(CommandBuffer commandBuffer,
                                                           TopLevelAccelerationStructureDesc desc)
    {
        ValidateNotCopyQueue(commandBuffer, "BuildTopLevelAccelerationStructure");

        ValidateRecordingState(commandBuffer, "BuildTopLevelAccelerationStructure");

        if (desc.Instances is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Top-level acceleration structure instances cannot be null.");

            return;
        }

        if (desc.Instances.Length is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Top-level acceleration structure must have at least 1 instance.");
        }

        for (int i = 0; i < desc.Instances.Length; i++)
        {
            ValidateRayTracingInstance(desc.Instances[i], $"instance at index {i}");
        }
    }

    public void ValidateUpdateTopLevelAccelerationStructure(CommandBuffer commandBuffer,
                                                            TopLevelAccelerationStructure accelerationStructure,
                                                            TopLevelAccelerationStructureDesc newDesc)
    {
        ValidateDirectQueue(commandBuffer, "UpdateTopLevelAccelerationStructure");

        ValidateRecordingState(commandBuffer, "UpdateTopLevelAccelerationStructure");

        if (newDesc.Instances is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "New top-level acceleration structure instances cannot be null.");

            return;
        }

        if (newDesc.Instances.Length != accelerationStructure.Desc.Instances.Length)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"New top-level acceleration structure must have the same number of instances as the existing one. Existing: {accelerationStructure.Desc.Instances.Length}, New: {newDesc.Instances.Length}.");

            return;
        }

        for (int i = 0; i < newDesc.Instances.Length; i++)
        {
            ValidateRayTracingInstance(newDesc.Instances[i], $"instance at index {i}");
        }
    }

    public void ValidateBeginRendering(CommandBuffer commandBuffer, FrameBuffer frameBuffer, ClearValue clearValue)
    {
        ValidateDirectQueue(commandBuffer, "BeginRendering");

        ValidateRecordingState(commandBuffer, "BeginRendering");

        if (!ValidateObject(frameBuffer, "frame buffer"))
        {
            return;
        }

        if (clearValue.ColorValues is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Clear value color values cannot be null.");
        }
    }

    public void ValidateEndRendering(CommandBuffer commandBuffer)
    {
        ValidateDirectQueue(commandBuffer, "EndRendering");

        ValidateRecordingState(commandBuffer, "EndRendering");
    }

    public void ValidateSetScissors(CommandBuffer commandBuffer, Scissor[] scissors)
    {
        ValidateDirectQueue(commandBuffer, "SetScissors");

        ValidateRecordingState(commandBuffer, "SetScissors");

        ValidateCurrentFrameBuffer(commandBuffer, "SetScissors");

        if (scissors is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Scissors cannot be null.");
        }
    }

    public void ValidateSetViewports(CommandBuffer commandBuffer, Viewport[] viewports)
    {
        ValidateDirectQueue(commandBuffer, "SetViewports");

        ValidateRecordingState(commandBuffer, "SetViewports");

        ValidateCurrentFrameBuffer(commandBuffer, "SetViewports");

        if (viewports is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Viewports cannot be null.");
        }
    }

    private void ValidateDirectQueue(CommandBuffer commandBuffer, string name)
    {
        if (commandBuffer.Queue.Type is not CommandQueueType.Direct)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name} can only be performed on the direct command queue. Current queue type: {commandBuffer.Queue.Type}.");
        }
    }

    private void ValidateNotCopyQueue(CommandBuffer commandBuffer, string name)
    {
        if (commandBuffer.Queue.Type is CommandQueueType.Copy)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name} cannot be performed on the copy command queue. Current queue type: {commandBuffer.Queue.Type}.");
        }
    }

    private void ValidateRecordingState(CommandBuffer commandBuffer, string name)
    {
        if (commandBuffer.State is not CommandBufferState.Recording)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name} can only be performed when the command buffer is in the recording state. Current state: {commandBuffer.State}.");
        }
    }

    private void ValidateCurrentFrameBuffer(CommandBuffer commandBuffer, string name)
    {
        if (commandBuffer.CurrentFrameBuffer is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name} requires a current frame buffer to be set. Use CommandBuffer.BeginRendering() to set it.");
        }
    }

    private void ValidateRayTracingGeometry(RayTracingGeometry geometry, string name)
    {
        ValidateDefinedEnum(geometry.Type, $"{name} type");

        if (geometry.Type is RayTracingGeometryType.Triangles)
        {
            RayTracingTriangles triangles = geometry.Triangles;

            if (!ValidateObject(triangles.VertexBuffer, $"{name} vertex buffer"))
            {
                return;
            }

            ObtainBufferValues(triangles.VertexBuffer,
                               out _,
                               out _,
                               out BufferUsageFlags vertexFlags,
                               $"{name} vertex buffer");

            if (!vertexFlags.HasFlag(BufferUsageFlags.AccelerationStructure))
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Warning,
                                             $"{name} vertex buffer should have BufferUsageFlags.AccelerationStructure. Current flags: {vertexFlags}.");
            }

            ValidateDefinedEnum(triangles.VertexFormat, $"{name} vertex format");

            if (triangles.VertexCount is 0)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             $"{name} must have at least 1 vertex.");
            }

            if (triangles.VertexStrideInBytes is 0)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             $"{name} vertex stride must be greater than 0.");
            }

            if (triangles.IndexBuffer is not null)
            {
                if (!ValidateObject(triangles.IndexBuffer, $"{name} index buffer"))
                {
                    return;
                }

                ObtainBufferValues(triangles.IndexBuffer,
                                   out _,
                                   out _,
                                   out BufferUsageFlags indexFlags,
                                   $"{name} index buffer");

                if (!indexFlags.HasFlag(BufferUsageFlags.AccelerationStructure))
                {
                    context.PublishDebugCallback(MessageCategory.System,
                                                 MessageSeverity.Warning,
                                                 $"{name} index buffer should have BufferUsageFlags.AccelerationStructure. Current flags: {indexFlags}.");
                }

                ValidateDefinedEnum(triangles.IndexFormat, $"{name} index format");

                if (triangles.IndexCount is 0)
                {
                    context.PublishDebugCallback(MessageCategory.System,
                                                 MessageSeverity.Error,
                                                 $"{name} must have at least 1 index.");
                }

                ValidateTransform(triangles.Transform, $"{name} transform");
            }
        }
        else if (geometry.Type is RayTracingGeometryType.AABBs)
        {
            RayTracingAABBs aabbs = geometry.AABBs;

            if (!ValidateObject(aabbs.Buffer, $"{name} AABB buffer"))
            {
                return;
            }

            ObtainBufferValues(aabbs.Buffer,
                               out _,
                               out _,
                               out BufferUsageFlags aabbFlags,
                               $"{name} AABB buffer");

            if (!aabbFlags.HasFlag(BufferUsageFlags.AccelerationStructure))
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Warning,
                                             $"{name} AABB buffer should have BufferUsageFlags.AccelerationStructure. Current flags: {aabbFlags}.");
            }

            if (aabbs.Count is 0)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             $"{name} must have at least 1 AABB.");
            }

            if (aabbs.StrideInBytes is 0)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             $"{name} AABB stride must be greater than 0.");
            }
        }
    }

    private void ValidateRayTracingInstance(RayTracingInstance instance, string name)
    {
        if (!ValidateObject(instance.AccelerationStructure, "acceleration structure"))
        {
            return;
        }

        ValidateTransform(instance.Transform, $"{name} transform");
    }

    private void ValidateTransform(Matrix4x4 matrix, string name)
    {
        if (matrix.M11 is 0 && matrix.M22 is 0 && matrix.M33 is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Warning,
                                         $"{name} has a zero scale transform. This will make the instance invisible in the scene.");
        }
    }
    #endregion

    #region Universal Validation
    private bool ValidateObjects<TObject>(TObject[]? objects, string name) where TObject : IDisposableObject
    {
        if (objects is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name} cannot be null.");

            return false;
        }

        for (int i = 0; i < objects.Length; i++)
        {
            if (!ValidateObject(objects[i], $"{name} at index {i}"))
            {
                return false;
            }
        }

        return true;
    }

    private bool ValidateObject<TObject>(TObject? @object, string name) where TObject : IDisposableObject
    {
        if (@object?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name} cannot be null or disposed.");

            return false;
        }

        return true;
    }

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