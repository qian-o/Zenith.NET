namespace Zenith.NET;

public record struct ResourceLayoutDesc : IDesc
{
    public ResourceLayoutDesc()
    {
        Elements = [];
    }

    public ResourceElementDesc[] Elements { get; set; }

    public readonly bool Validate()
    {
        if (Elements is null || Elements.Length is 0)
        {
            return false;
        }

        if (Elements.Any(static item => !item.Validate()))
        {
            return false;
        }

        return true;
    }
}