namespace Zenith.NET;

public record struct ShaderDesc
{
    public byte[] ShaderBytes;

    public string EntryPoint;

    public ShaderStageFlags Stage;
}
