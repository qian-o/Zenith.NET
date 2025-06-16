namespace Zenith.NET;

public struct ShaderDesc
{
    public byte[] ShaderBytes { get; set; }

    public string EntryPoint { get; set; }

    public ShaderStageFlags Stage { get; set; }
}
