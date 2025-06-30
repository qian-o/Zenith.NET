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

    public void ValidateSwapChainDesc(SwapChainDesc desc)
    {
        ValidateSurface(desc.Surface);

        if (!swapChainFormats.Contains(desc.ColorTargetFormat))
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid color target format: {desc.ColorTargetFormat}. Supported formats are: {string.Join(", ", swapChainFormats.Select(f => f.ToString()))}.");
        }

        if (desc.DepthStencilTargetFormat.HasValue && !depthStencilFormats.Contains(desc.DepthStencilTargetFormat.Value))
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid depth-stencil target format: {desc.DepthStencilTargetFormat.Value}. Supported formats are: {string.Join(", ", depthStencilFormats.Select(f => f.ToString()))}.");
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
        if (!Enum.IsDefined(desc.Type))
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid texture type: {desc.Type}. Supported types are: {string.Join(", ", Enum.GetNames<TextureType>())}.");
        }

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

        if (desc.Type is TextureType.Texture1DArray or TextureType.Texture2DArray or TextureType.TextureCubeArray)
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

        if (!Enum.IsDefined(desc.SampleCount))
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid sample count: {desc.SampleCount}. Supported sample counts are: {string.Join(", ", Enum.GetNames<SampleCount>())}.");
        }
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
        if (!Enum.IsDefined(desc.U))
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid U address mode: {desc.U}. Supported modes are: {string.Join(", ", Enum.GetNames<AddressMode>())}.");
        }

        if (!Enum.IsDefined(desc.V))
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid V address mode: {desc.V}. Supported modes are: {string.Join(", ", Enum.GetNames<AddressMode>())}.");
        }

        if (!Enum.IsDefined(desc.W))
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid W address mode: {desc.W}. Supported modes are: {string.Join(", ", Enum.GetNames<AddressMode>())}.");
        }

        if (!Enum.IsDefined(desc.Filter))
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid filter: {desc.Filter}. Supported filters are: {string.Join(", ", Enum.GetNames<Filter>())}.");
        }

        if (!Enum.IsDefined(desc.ComparisonFunc))
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid comparison function: {desc.ComparisonFunc}. Supported functions are: {string.Join(", ", Enum.GetNames<ComparisonFunc>())}.");
        }

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

        if (!Enum.IsDefined(desc.BorderColor))
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid border color: {desc.BorderColor}. Supported colors are: {string.Join(", ", Enum.GetNames<BorderColor>())}.");
        }
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

            if (!Enum.IsDefined(element.Type))
            {
                context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid resource element type at index {i}: {element.Type}. Supported types are: {string.Join(", ", Enum.GetNames<ResourceType>())}.");
            }

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

    private void ValidateSurface(Surface surface)
    {
        if (!Enum.IsDefined(surface.Type))
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid surface type: {surface.Type}. Supported types are: {string.Join(", ", Enum.GetNames<SurfaceType>())}.");
        }

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

        if (type is TextureType.Texture1DArray or TextureType.Texture2DArray or TextureType.TextureCubeArray)
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
}
