namespace Zenith.NET;

internal partial class Validator(GraphicsContext context)
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

    private void ValidateObjects<TObject>(TObject[]? objects, string name) where TObject : IDisposableObject
    {
        if (objects is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name} cannot be null.");

            return;
        }

        for (int i = 0; i < objects.Length; i++)
        {
            ValidateObject(objects[i], false, $"{name} at index {i}");
        }
    }

    private void ValidateObject<TObject>(TObject? @object, bool canBeNull, string name) where TObject : IDisposableObject
    {
        if (@object is null)
        {
            if (!canBeNull)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             $"{name} cannot be null.");
            }
        }
        else if (@object.IsDisposed)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name} must be a valid, non-disposed object.");
        }
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
}