namespace Zenith.NET;

public struct InputLayout
{
    public InputElement[] Elements;

    public uint StrideInBytes;

    public void Add(InputElement element)
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

        StrideInBytes += ZenithHelper.SizeInBytes(element.Format);
    }
}
