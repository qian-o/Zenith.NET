namespace Zenith.NET;

/// <summary>
/// Describes a shader module, including its bytecode, entry point, and stage flags.
/// </summary>
public record struct ShaderDesc : IDesc
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShaderDesc"/> struct with default values.
    /// </summary>
    public ShaderDesc()
    {
        ShaderBytes = [];
        EntryPoint = string.Empty;
        Flags = ShaderStageFlags.None;
    }

    /// <summary>
    /// The raw shader bytecode.
    /// </summary>
    public byte[] ShaderBytes { get; set; }

    /// <summary>
    /// The name of the entry point function in the shader module.
    /// </summary>
    public string EntryPoint { get; set; }

    /// <summary>
    /// The shader stage(s) this module is intended for.
    /// </summary>
    public ShaderStageFlags Flags { get; set; }

    /// <summary>
    /// Validates the current <see cref="ShaderDesc"/> instance.
    /// </summary>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
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

        if (!Enum.IsDefined(Flags))
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
