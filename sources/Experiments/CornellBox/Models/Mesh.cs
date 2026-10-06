namespace CornellBox.Models;

internal readonly struct Mesh(uint firstIndex, uint indexCount)
{
    public readonly uint FirstIndex = firstIndex;

    public readonly uint IndexCount = indexCount;
}
