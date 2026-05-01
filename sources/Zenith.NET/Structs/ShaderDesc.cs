namespace Zenith.NET;

public record struct ShaderDesc
{
    public byte[] Bytecode;

    public string EntryPoint;

    public ShaderStages Stage;
}
