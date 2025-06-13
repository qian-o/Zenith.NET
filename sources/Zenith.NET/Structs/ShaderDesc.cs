using Zenith.NET.Helpers;

namespace Zenith.NET;

public record struct ShaderDesc : IDesc
{
    public byte[] ShaderBytes { get; set; }

    public string EntryPoint { get; set; }

    public ShaderStageFlags Stage { get; set; }

    public readonly bool Validate()
    {
        if (Validation.IsNullOrEmpty(ShaderBytes))
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
