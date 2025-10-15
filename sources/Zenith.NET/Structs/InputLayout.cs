namespace Zenith.NET;

public record struct InputLayout
{
    public InputElement[] Elements;

    public uint StrideInBytes;

    public InputLayout Add(InputElement element)
    {
        element.OffsetInBytes = StrideInBytes;

        if (Elements is null)
        {
            Elements = [element];
        }
        else
        {
            Array.Resize(ref Elements, Elements.Length + 1);

            Elements[^1] = element;
        }

        StrideInBytes += ZenithHelpers.SizeInBytes(element.Format);

        return this;
    }
}
