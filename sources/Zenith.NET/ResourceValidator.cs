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

        if (desc.SizeInBytes % desc.Buffer.Desc.StrideInBytes is not 0)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Warning, "Buffer view size is not a multiple of the buffer's stride. This may lead to unexpected behavior, especially when using structured buffers.");
        }
    }

    public void ValidateTextureDesc(TextureDesc desc)
    {
        if (!Enum.IsDefined(desc.Type))
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid texture type: {desc.Type}. Supported types are: {string.Join(", ", Enum.GetNames<TextureType>())}.");

            return;
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

        if (desc.Layers is 0)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Texture layers must be greater than zero.");
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

        if (desc.MipLevel >= desc.Texture.Desc.MipLevels)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid mip level: {desc.MipLevel}. It must be less than the texture's mip levels ({desc.Texture.Desc.MipLevels}).");
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
    }

    private void ValidateSurface(Surface surface)
    {
        if (!Enum.IsDefined(surface.Type))
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid surface type: {surface.Type}. Supported types are: {string.Join(", ", Enum.GetNames<SurfaceType>())}.");

            return;
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
}
