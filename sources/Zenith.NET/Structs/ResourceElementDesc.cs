namespace Zenith.NET;

public record struct ResourceElementDesc : IDesc
{
    public ResourceElementDesc()
    {
        Type = ResourceType.ConstantBuffer;
        Index = 0;
        Count = 1;
        StageFlags = ShaderStageFlags.None;
    }

    public ResourceType Type { get; set; }

    public uint Index { get; set; }

    public uint Count { get; set; }

    public ShaderStageFlags StageFlags { get; set; }

    public readonly bool Validate()
    {
        if (!Enum.IsDefined(Type))
        {
            return false;
        }

        if (Count is 0)
        {
            return false;
        }

        if (StageFlags is ShaderStageFlags.None)
        {
            return false;
        }

        return true;
    }
}
