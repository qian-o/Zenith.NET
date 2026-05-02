namespace Zenith.NET;

public record struct InputLayout
{
    public InputElement[] InputElements;

    public uint StrideInBytes;

    public void Add(InputElement element)
    {
        element.OffsetInBytes = StrideInBytes;

        if (InputElements is null)
        {
            InputElements = [element];
        }
        else
        {
            Array.Resize(ref InputElements, InputElements.Length + 1);

            InputElements[^1] = element;
        }

        StrideInBytes += ZenithHelper.SizeInBytes(element.Format);
    }
}
