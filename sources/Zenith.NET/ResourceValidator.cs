namespace Zenith.NET;

internal class ResourceValidator(GraphicsContext context)
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

    public void ValidateSwapChainDesc(SwapChainDesc desc)
    {
        ValidateSurface(desc.Surface);

        if (!swapChainFormats.Contains(desc.ColorTargetFormat))
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid color target format: {desc.ColorTargetFormat}. Supported formats are: {string.Join(", ", swapChainFormats.Select(static item => item.ToString()))}.");
        }

        if (desc.DepthStencilTargetFormat.HasValue && !depthStencilFormats.Contains(desc.DepthStencilTargetFormat.Value))
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid depth-stencil target format: {desc.DepthStencilTargetFormat.Value}. Supported formats are: {string.Join(", ", depthStencilFormats.Select(static item => item.ToString()))}.");
        }
    }

    public void ValidateBufferDesc(BufferDesc desc)
    {
        if (desc.SizeInBytes is 0)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Buffer size must be greater than zero.");
        }

        if (desc.StrideInBytes is 0)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Warning, "Buffer stride is zero. This may lead to unexpected behavior, especially when using structured buffers. Consider setting a non-zero stride.");
        }
    }

    public void ValidateBufferViewDesc(BufferViewDesc desc)
    {
        if (desc.Buffer?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Buffer view must reference a valid buffer that is not disposed.");

            return;
        }

        if (desc.OffsetInBytes >= desc.Buffer.Desc.SizeInBytes)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid offset: {desc.OffsetInBytes}. It must be less than the buffer's size ({desc.Buffer.Desc.SizeInBytes}).");
        }

        if (desc.SizeInBytes is 0)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Buffer view size must be greater than zero.");
        }
        else if (desc.OffsetInBytes + desc.SizeInBytes > desc.Buffer.Desc.SizeInBytes)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Buffer view size exceeds the buffer's size. Ensure that OffsetInBytes + SizeInBytes does not exceed the buffer's SizeInBytes ({desc.Buffer.Desc.SizeInBytes}).");
        }

        if (desc.StrideInBytes is 0)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Warning, "Buffer view stride is zero. This may lead to unexpected behavior, especially when using structured buffers. Consider setting a non-zero stride.");
        }
    }

    public void ValidateTextureDesc(TextureDesc desc)
    {
        ValidateDefinedEnum(desc.Type, "texture type");

        ValidateDefinedEnum(desc.Format, "texture format");

        if (desc.Type is TextureType.Texture1D or TextureType.Texture1DArray && desc.Width is 0)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Texture width must be greater than zero for 1D textures.");
        }
        else if (desc.Type is TextureType.Texture2D or TextureType.Texture2DArray && (desc.Width is 0 || desc.Height is 0))
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Texture width and height must be greater than zero for 2D textures.");
        }
        else if (desc.Type is TextureType.Texture3D && (desc.Width is 0 || desc.Height is 0 || desc.Depth is 0))
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Texture width, height, and depth must be greater than zero for 3D textures.");
        }
        else if (desc.Type is TextureType.TextureCube or TextureType.TextureCubeArray && (desc.Width is 0 || desc.Height is 0 || desc.Width != desc.Height))
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Texture width and height must be equal and greater than zero for cube textures.");
        }

        if (arrayTextureTypes.Contains(desc.Type))
        {
            if (desc.Layers is 0)
            {
                context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Texture layers must be greater than zero.");
            }
        }
        else if (desc.Layers is not 1)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Texture layers must be 1 for non-array textures.");
        }

        if (desc.MipLevels is 0)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Texture mip levels must be greater than zero.");
        }

        ValidateDefinedEnum(desc.SampleCount, "sample count");
    }

    public void ValidateTextureViewDesc(TextureViewDesc desc)
    {
        if (desc.Texture?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Texture view must reference a valid texture that is not disposed.");

            return;
        }

        if (desc.FirstLayer >= desc.Texture.Desc.Layers)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid first layer: {desc.FirstLayer}. It must be less than the texture's layers ({desc.Texture.Desc.Layers}).");
        }

        if (desc.LayerCount is 0)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Texture view layer count must be greater than zero.");
        }
        else if (desc.FirstLayer + desc.LayerCount > desc.Texture.Desc.Layers)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Texture view layer count exceeds the texture's layers. Ensure that FirstLayer + LayerCount does not exceed the texture's Layers ({desc.Texture.Desc.Layers}).");
        }

        if (desc.FirstMipLevel >= desc.Texture.Desc.MipLevels)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid first mip level: {desc.FirstMipLevel}. It must be less than the texture's mip levels ({desc.Texture.Desc.MipLevels}).");
        }

        if (desc.MipLevelCount is 0)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Texture view mip level count must be greater than zero.");
        }
        else if (desc.FirstMipLevel + desc.MipLevelCount > desc.Texture.Desc.MipLevels)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Texture view mip level count exceeds the texture's mip levels. Ensure that FirstMipLevel + MipLevelCount does not exceed the texture's MipLevels ({desc.Texture.Desc.MipLevels}).");
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
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid max anisotropy: {desc.MaxAnisotropy}. Supported values are: 1, 2, 4, 8, or 16.");
        }

        if (desc.MinLod < 0)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "MinLod must be greater than or equal to zero.");
        }

        if (desc.MaxLod < desc.MinLod)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "MaxLod must be greater than or equal to MinLod.");
        }

        if (desc.LodBias is < -16 or > 16)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "LodBias must be between -16 and 16.");
        }

        ValidateDefinedEnum(desc.BorderColor, "border color");
    }

    public void ValidateResourceLayoutDesc(ResourceLayoutDesc desc)
    {
        if (desc.Elements is null)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Resource layout elements cannot be null.");

            return;
        }

        if (desc.Elements.Length is 0)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Resource layout must have at least one element.");
        }

        for (int i = 0; i < desc.Elements.Length; i++)
        {
            ResourceElement element = desc.Elements[i];

            ValidateDefinedEnum(element.Type, $"resource element type at index {i}");

            if (element.Count is 0)
            {
                context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Resource element count at index {i} must be greater than zero.");
            }
        }
    }

    public void ValidateResourceSetDesc(ResourceSetDesc desc)
    {
        if (desc.Layout?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Resource set must reference a valid resource layout that is not disposed.");

            return;
        }

        if (desc.Resources is null)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Resource set resources cannot be null.");

            return;
        }

        ResourceType[] types = [.. desc.Layout.Desc.Elements.SelectMany(static item => Enumerable.Repeat(item.Type, (int)item.Count))];

        if (desc.Resources.Length != types.Length)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Resource set must have exactly {types.Length} resources to match the layout. Provided: {desc.Resources.Length}.");

            return;
        }

        for (int i = 0; i < desc.Resources.Length; i++)
        {
            IBindableResource? resource = desc.Resources[i];

            if (resource?.IsDisposed is not false)
            {
                context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Resource at index {i} is null. All resources must be valid.");

                continue;
            }

            switch (types[i])
            {
                case ResourceType.ConstantBuffer:
                case ResourceType.StructuredBuffer:
                case ResourceType.StructuredBufferReadWrite:
                    if (resource is not IBuffer)
                    {
                        context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Resource at index {i} is not a valid buffer.");
                    }
                    break;
                case ResourceType.Texture:
                case ResourceType.TextureReadWrite:
                    if (resource is not ITexture)
                    {
                        context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Resource at index {i} is not a valid texture.");
                    }
                    break;
                case ResourceType.Sampler:
                    if (resource is not Sampler)
                    {
                        context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Resource at index {i} is not a valid sampler.");
                    }
                    break;
                case ResourceType.AccelerationStructure:
                    if (resource is not TopLevelAccelerationStructure)
                    {
                        context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Resource at index {i} is not a valid acceleration structure.");
                    }
                    break;
                default:
                    context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid resource type at index {i}: {types[i]}. Supported types are: {string.Join(", ", Enum.GetNames<ResourceType>())}.");
                    break;
            }
        }
    }

    public void ValidateFrameBufferDesc(FrameBufferDesc desc)
    {
        if (desc.ColorTargets is null)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Frame buffer color targets cannot be null.");

            return;
        }

        if (desc.ColorTargets.Length is 0 && desc.DepthStencilTarget is null)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Frame buffer must have at least one color target or a depth-stencil target.");

            return;
        }

        if (desc.ColorTargets.Length > 8)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Frame buffer cannot have more than 8 color targets.");
        }

        foreach (FrameBufferAttachment frameBufferAttachment in desc.ColorTargets)
        {
            ValidateFrameBufferAttachment(frameBufferAttachment);
        }

        if (desc.DepthStencilTarget is not null)
        {
            ValidateFrameBufferAttachment(desc.DepthStencilTarget.Value);
        }
    }

    public void ValidateShaderDesc(ShaderDesc desc)
    {
        if (desc.ShaderBytes is null)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Shader bytecode cannot be null.");

            return;
        }

        if (desc.ShaderBytes.Length is 0)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Shader bytecode must not be empty.");
        }

        if (string.IsNullOrEmpty(desc.EntryPoint))
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Shader entry point must not be null or empty.");
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
                    context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Depth bias must be greater than or equal to zero.");
                }

                if (rasterizerState.DepthBiasClamp < 0)
                {
                    context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Depth bias clamp must be greater than or equal to zero.");
                }

                if (rasterizerState.SlopeScaledDepthBias < 0)
                {
                    context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Slope scaled depth bias must be greater than or equal to zero.");
                }
            }

            // DepthStencilState
            {
                DepthStencilState depthStencilState = renderStates.DepthStencilState;

                ValidateDefinedEnum(depthStencilState.DepthFunc, "depth function");

                ValidateDepthStencilStateOp(depthStencilState.FrontFace);

                ValidateDepthStencilStateOp(depthStencilState.BackFace);
            }

            // BlendState
            {
                BlendState blendState = renderStates.BlendState;

                foreach (BlendStateRenderTarget renderTarget in (BlendStateRenderTarget[])(blendState.IndependentBlendEnable ? [blendState.RenderTarget0, blendState.RenderTarget1, blendState.RenderTarget2, blendState.RenderTarget3, blendState.RenderTarget4, blendState.RenderTarget5, blendState.RenderTarget6, blendState.RenderTarget7] : [blendState.RenderTarget0]))
                {
                    ValidateBlendStateRenderTarget(renderTarget);
                }
            }
        }

        // Shaders
        {
            GraphicsShaders shaders = desc.Shaders;

            if (shaders.Vertex?.IsDisposed is not false)
            {
                context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Vertex shader must reference a valid shader that is not disposed.");
            }

            if (shaders.Hull?.IsDisposed is true)
            {
                context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Hull shader must reference a valid shader that is not disposed.");
            }

            if (shaders.Domain?.IsDisposed is true)
            {
                context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Domain shader must reference a valid shader that is not disposed.");
            }

            if (shaders.Geometry?.IsDisposed is true)
            {
                context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Geometry shader must reference a valid shader that is not disposed.");
            }

            if (shaders.Pixel?.IsDisposed is not false)
            {
                context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Pixel shader must reference a valid shader that is not disposed.");
            }
        }

        if (desc.InputLayouts is null || desc.InputLayouts.Length is 0)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Graphics pipeline must have at least one input layout.");
        }

        if (desc.ResourceLayouts is null)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Graphics pipeline resource layouts cannot be null.");
        }

        ValidateOutput(desc.Outputs);
    }

    public void ValidateComputePipelineDesc(ComputePipelineDesc desc)
    {
        throw new NotImplementedException();
    }

    public void ValidateRayTracingPipelineDesc(RayTracingPipelineDesc desc)
    {
        throw new NotImplementedException();
    }

    private void ValidateSurface(Surface surface)
    {
        ValidateDefinedEnum(surface.Type, "surface type");

        if (surface.Handles is null)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Surface handles cannot be null.");

            return;
        }

        if (surface.Type is SurfaceType.Win32 && surface.Handles.Length is not 1)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Win32 surface must have exactly one handle (HWND).");
        }
        else if (surface.Type is SurfaceType.Wayland && surface.Handles.Length is not 2)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Wayland surface must have exactly two handles (display and surface).");
        }
        else if (surface.Type is SurfaceType.Xlib && surface.Handles.Length is not 2)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Xlib surface must have exactly two handles (display and window).");
        }
        else if (surface.Type is SurfaceType.Android && surface.Handles.Length is not 1)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Android surface must have exactly one handle (native window).");
        }
        else if (surface.Type is SurfaceType.IOS && surface.Handles.Length is not 1)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "iOS surface must have exactly one handle (view).");
        }
        else if (surface.Type is SurfaceType.MacOS && surface.Handles.Length is not 1)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "MacOS surface must have exactly one handle (view).");
        }

        if (surface.Handles.Any(static item => item is 0))
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Surface handles cannot contain zero values.");
        }

        if (surface.Width is 0 || surface.Height is 0)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Surface width and height must be greater than zero.");
        }
    }

    private void ValidateFrameBufferAttachment(FrameBufferAttachment attachment)
    {
        if (attachment.Target?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Frame buffer attachment must reference a valid texture that is not disposed.");

            return;
        }

        ObtainTextureValues(attachment.Target, out TextureType type, out uint layers, out uint mipLevels);

        if (type is not TextureType.Texture2D or TextureType.Texture2DArray)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid texture type for frame buffer attachment: {type}. Only Texture2D and Texture2DArray are supported.");
        }

        ValidateTextureSlice(type, layers, mipLevels, attachment.Slice);
    }

    private void ValidateDepthStencilStateOp(DepthStencilStateOp stateOp)
    {
        ValidateDefinedEnum(stateOp.StencilFailOp, "stencil fail operation");

        ValidateDefinedEnum(stateOp.StencilDepthFailOp, "stencil depth fail operation");

        ValidateDefinedEnum(stateOp.StencilPassOp, "stencil pass operation");

        ValidateDefinedEnum(stateOp.StencilFunc, "stencil function");
    }

    private void ValidateBlendStateRenderTarget(BlendStateRenderTarget renderTarget)
    {
        ValidateDefinedEnum(renderTarget.SrcBlend, "source blend");

        ValidateDefinedEnum(renderTarget.DestBlend, "destination blend");

        ValidateDefinedEnum(renderTarget.BlendOp, "blend operation");

        ValidateDefinedEnum(renderTarget.SrcBlendAlpha, "source blend alpha");

        ValidateDefinedEnum(renderTarget.DestBlendAlpha, "destination blend alpha");

        ValidateDefinedEnum(renderTarget.BlendOpAlpha, "blend operation alpha");
    }

    private void ValidateOutput(Output output)
    {
        if (output.ColorAttachments is null)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Output color attachments cannot be null.");

            return;
        }

        if (output.ColorAttachments.Length is 0 && output.DepthStencilAttachment is null)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Output must have at least one color attachment or a depth-stencil attachment.");

            return;
        }

        if (output.ColorAttachments.Length > 8)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Output cannot have more than 8 color attachments.");
        }

        if (output.DepthStencilAttachment is not null && !depthStencilFormats.Contains(output.DepthStencilAttachment.Value))
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid depth-stencil attachment format: {output.DepthStencilAttachment.Value}. Supported formats are: {string.Join(", ", depthStencilFormats.Select(static item => item.ToString()))}.");
        }
    }

    #region Universal Validation
    private void ValidateDefinedEnum<TEnum>(TEnum value, string name) where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid {name}: {value}. Supported values are: {string.Join(", ", Enum.GetNames<TEnum>())}.");
        }
    }
    #endregion

    #region Universal Texture Validation
    private void ObtainTextureValues(ITexture iTexture, out TextureType type, out uint layers, out uint mipLevels)
    {
        if (iTexture is Texture texture)
        {
            type = texture.Desc.Type;
            layers = texture.Desc.Layers;
            mipLevels = texture.Desc.MipLevels;
        }
        else if (iTexture is TextureView textureView)
        {
            type = textureView.Desc.Texture.Desc.Type;
            layers = textureView.Desc.LayerCount;
            mipLevels = textureView.Desc.MipLevelCount;
        }
        else
        {
            type = default;
            layers = default;
            mipLevels = default;

            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Invalid texture type for slice validation. Expected Texture or TextureView.");
        }
    }

    private void ValidateTextureSlice(TextureType type, uint layers, uint mipLevels, TextureSlice slice)
    {
        if (type is TextureType.TextureCube or TextureType.TextureCubeArray)
        {
            if (slice.Face >= 6)
            {
                context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid face index: {slice.Face}. It must be between 0 and 5 for cube textures.");
            }
        }
        else if (slice.Face is not 0)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid face index: {slice.Face}. It must be 0 for non-cube textures.");
        }

        if (arrayTextureTypes.Contains(type))
        {
            if (slice.Layer >= layers)
            {
                context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid layer index: {slice.Layer}. It must be less than the number of layers ({layers}) for array textures.");
            }
        }
        else if (slice.Layer is not 0)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid layer index: {slice.Layer}. It must be 0 for non-array textures.");
        }

        if (slice.MipLevel >= mipLevels)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid mip level: {slice.MipLevel}. It must be less than the number of mip levels ({mipLevels}).");
        }
    }
    #endregion
}
