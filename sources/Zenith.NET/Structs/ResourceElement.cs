namespace Zenith.NET;

public record struct ResourceElement
{
    public ResourceType Type;

    public uint Index;

    public uint Count;

    public ShaderStageFlags StageFlags;
}
