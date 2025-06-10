using Zenith.NET.Helpers;

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
        return Validation.IsValidDescs(Elements);
    }
}