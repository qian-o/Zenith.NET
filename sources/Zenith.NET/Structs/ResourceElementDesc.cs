namespace Zenith.NET;

public record struct ResourceElementDesc
{
    public ResourceType Type { get; set; }

    public uint Index { get; set; }

    public uint Count { get; set; }

    public ShaderStageFlags StageFlags { get; set; }
}
