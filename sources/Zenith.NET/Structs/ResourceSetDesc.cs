namespace Zenith.NET;

public record struct ResourceSetDesc : IDesc
{
    public ResourceLayout Layout { get; set; }

    public GraphicsResource[] Resources { get; set; }

    public readonly bool Validate()
    {
        if (Layout is null)
        {
            return false;
        }

        if (Resources is null)
        {
            return false;
        }

        uint index = 0;
        foreach (ResourceElementDesc element in Layout.Desc.Elements)
        {
            Func<GraphicsResource, bool> validateFunc = element.Type switch
            {
                ResourceType.ConstantBuffer
                or ResourceType.StructuredBuffer
                or ResourceType.StructuredBufferReadWrite => static resource => resource is Buffer,

                ResourceType.Texture
                or ResourceType.TextureReadWrite => static resource => resource is Texture,

                ResourceType.Sampler => static resource => resource is Sampler,

                ResourceType.AccelerationStructure => static resource => resource is TopLevelAccelerationStructure,

                _ => _ => false
            };

            for (uint i = 0; i < element.Count; i++)
            {
                if (index >= Resources.Length)
                {
                    return false;
                }

                if (!validateFunc(Resources[index]))
                {
                    return false;
                }

                index++;
            }
        }

        return true;
    }
}
