namespace Zenith.NET;

public record struct FrameBufferAttachmentDesc : IDesc
{
    public FrameBufferAttachmentDesc()
    {
        Target = null!;
        FaceIndex = 0;
        ArrayLayer = 0;
        MipLevel = 0;
    }

    public Texture Target { get; set; }

    public uint FaceIndex { get; set; }

    public uint ArrayLayer { get; set; }

    public uint MipLevel { get; set; }

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
