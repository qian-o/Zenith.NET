namespace Zenith.NET;

public record struct ShaderDesc : IDesc
{
    public ShaderDesc()
    {
        ShaderBytes = [];
        EntryPoint = string.Empty;
        Stage = ShaderStageFlags.None;
    }

    public byte[] ShaderBytes { get; set; }

    public string EntryPoint { get; set; }

    public ShaderStageFlags Stage { get; set; }

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

        if (Stage is ShaderStageFlags.None)
        {
            return false;
        }

        if (!Enum.IsDefined(Stage))
        {
            return false;
        }

        return true;
    }
}
