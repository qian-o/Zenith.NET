namespace Zenith.NET;

public record struct ResourceBinding
{
    public ResourceType Type;

    public uint Index;

    public uint Count;

    public ShaderStageFlags StageFlags;
}
