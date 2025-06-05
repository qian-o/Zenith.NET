namespace Zenith.NET;

/// <summary>
/// Represents a texture resource, providing methods for data upload and access to its description.
/// </summary>
public abstract class Texture(GraphicsContext context, TextureDesc desc) : GraphicsResource(context)
{
    /// <summary>
    /// Gets the texture description.
    /// </summary>
    public TextureDesc Desc { get; } = desc;

    /// <summary>
    /// Uploads data to the texture immediately, blocking until the operation is complete.
    /// </summary>
    /// <typeparam name="T">The type of data to upload. Must be unmanaged.</typeparam>
    /// <param name="data">The data to upload.</param>
    /// <param name="position">The position within the texture to start uploading.</param>
    /// <param name="width">The width of the region to upload.</param>
    /// <param name="height">The height of the region to upload.</param>
    /// <param name="depth">The depth of the region to upload.</param>
    /// <exception cref="ArgumentException">Thrown if data is empty or dimensions are invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the upload region exceeds texture bounds.</exception>
    public void Upload<T>(ReadOnlySpan<T> data,
                          TexturePosition position,
                          uint width,
                          uint height,
                          uint depth) where T : unmanaged
    {
        if (data.IsEmpty)
        {
            throw new ArgumentException("Data cannot be empty.", nameof(data));
        }

        uint requiredWidth = position.X + width;
        uint requiredHeight = position.Y + height;
        uint requiredDepth = position.Z + depth;

        if (Desc.Type is TextureType.Texture1D or TextureType.Texture1DArray)
        {
            if (width is 0)
            {
                throw new ArgumentException("Width must be greater than zero for 1D textures.", nameof(width));
            }

            if (requiredWidth > Desc.Width)
            {
                throw new ArgumentOutOfRangeException($"{nameof(position)}, {nameof(width)}", "Width exceeds texture width.");
            }
        }
        else if (Desc.Type is TextureType.Texture2D or TextureType.Texture2DArray)
        {
            if (width is 0 || height is 0)
            {
                throw new ArgumentException("Width and height must be greater than zero for 2D textures.", $"{nameof(width)}, {nameof(height)}");
            }

            if (requiredWidth > Desc.Width || requiredHeight > Desc.Height)
            {
                throw new ArgumentOutOfRangeException($"{nameof(position)}, {nameof(width)}, {nameof(height)}", "Width or height exceeds texture dimensions.");
            }
        }
        else if (Desc.Type is TextureType.Texture3D)
        {
            if (width is 0 || height is 0 || depth is 0)
            {
                throw new ArgumentException("Width, height, and depth must be greater than zero for 3D textures.", $"{nameof(width)}, {nameof(height)}, {nameof(depth)}");
            }

            if (requiredWidth > Desc.Width || requiredHeight > Desc.Height || requiredDepth > Desc.Depth)
            {
                throw new ArgumentOutOfRangeException($"{nameof(position)}, {nameof(width)}, {nameof(height)}, {nameof(depth)}", "Width, height, or depth exceeds texture dimensions.");
            }
        }
        else if (Desc.Type is TextureType.TextureCube or TextureType.TextureCubeArray)
        {
            if (width is 0 || height is 0)
            {
                throw new ArgumentException("Width and height must be greater than zero for cube textures.", $"{nameof(width)}, {nameof(height)}");
            }

            if (requiredWidth > Desc.Width || requiredHeight > Desc.Height)
            {
                throw new ArgumentOutOfRangeException($"{nameof(position)}, {nameof(width)}, {nameof(height)}", "Width or height exceeds texture dimensions.");
            }

            if (position.FaceIndex >= 6)
            {
                throw new ArgumentOutOfRangeException(nameof(position), "Face index must be between 0 and 5 for cube textures.");
            }
        }

        if (Desc.Type is TextureType.Texture1DArray or TextureType.Texture2DArray or TextureType.TextureCubeArray
            && position.ArrayLayer >= Desc.ArrayLayers)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Array layer exceeds texture array layers.");
        }

        if (position.MipLevel >= Desc.MipLevels)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Mip level exceeds texture mip levels.");
        }

        // TODO: Use the internal Copy queue to upload data and wait for completion.
    }
}
