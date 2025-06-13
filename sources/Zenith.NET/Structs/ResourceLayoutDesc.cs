using Zenith.NET.Helpers;

namespace Zenith.NET;

public record struct ResourceLayoutDesc : IDesc
{
    public ResourceElementDesc[] Elements { get; set; }

    public readonly bool Validate()
    {
        return Validation.IsValidDescs(Elements);
    }
}