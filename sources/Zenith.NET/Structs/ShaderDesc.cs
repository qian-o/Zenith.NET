namespace Zenith.NET;

public record struct ShaderDesc : IDesc
{
    public ShaderDesc()
    {
        ShaderBytes = [];
        EntryPoint = string.Empty;
        Flags = ShaderStageFlags.None;
    }

    /// <summary>
    /// An array containing the raw shader bytes.
    /// </summary>
    public byte[] ShaderBytes { get; set; }

    /// <summary>
    /// The name of the entry point function in the shader module to be used in this stage.
    /// </summary>
    public string EntryPoint { get; set; }

    /// <summary>
    /// The shader stage this instance describes.
    /// </summary>
    public ShaderStageFlags Flags { get; set; }

    public readonly bool Validate()
    {
        if (ShaderBytes is null || ShaderBytes.Length is 0)
        {
            return false;
        }

        if (string.IsNullOrEmpty(EntryPoint))
        {
            return false;
        }

        if (Flags is ShaderStageFlags.None)
        {
            return false;
        }

        return true;
    }
}
