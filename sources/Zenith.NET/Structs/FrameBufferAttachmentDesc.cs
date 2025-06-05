namespace Zenith.NET;

/// <summary>
/// Describes a single attachment of a framebuffer, including the target texture and its subresource selection.
/// </summary>
public record struct FrameBufferAttachmentDesc : IDesc
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FrameBufferAttachmentDesc"/> struct with default values.
    /// </summary>
    public FrameBufferAttachmentDesc()
    {
        Target = null!;
        FaceIndex = 0;
        ArrayLayer = 0;
        MipLevel = 0;
    }

    /// <summary>
    /// The target texture to render into.
    /// </summary>
    public Texture Target { get; set; }

    /// <summary>
    /// The face index to render to. Only applicable for cube or cube array textures.
    /// </summary>
    public uint FaceIndex { get; set; }

    /// <summary>
    /// The array layer to render to. Only applicable for array textures.
    /// </summary>
    public uint ArrayLayer { get; set; }

    /// <summary>
    /// The mip level to render to.
    /// </summary>
    public uint MipLevel { get; set; }

    /// <summary>
    /// Validates the current <see cref="FrameBufferAttachmentDesc"/> instance.
    /// </summary>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
    public readonly bool Validate()
    {
        if (Target is null)
        {
            return false;
        }

        if (Target.Desc.Type is TextureType.TextureCube or TextureType.TextureCubeArray && FaceIndex >= 6)
        {
            return false;
        }

        if (Target.Desc.Type is TextureType.Texture1DArray or TextureType.Texture2DArray or TextureType.TextureCubeArray
            && ArrayLayer >= Target.Desc.ArrayLayers)
        {
            return false;
        }

        if (MipLevel >= Target.Desc.MipLevels)
        {
            return false;
        }

        return true;
    }
}
