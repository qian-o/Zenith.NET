namespace Zenith.NET;

/// <summary>
/// Describes a resource layout for a pipeline, consisting of an array of resource binding elements.
/// </summary>
public record struct ResourceLayoutDesc : IDesc
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceLayoutDesc"/> struct with default values.
    /// </summary>
    public ResourceLayoutDesc()
    {
        Elements = [];
    }

    /// <summary>
    /// Array of resource binding elements that define the layout of resources in a pipeline.
    /// </summary>
    public ResourceElementDesc[] Elements { get; set; }

    /// <summary>
    /// Validates the current <see cref="ResourceLayoutDesc"/> instance.
    /// </summary>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
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