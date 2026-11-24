namespace CornellBox.Models;

internal readonly struct DrawBatch(uint firstIndex, uint indexCount, uint baseInstance, uint instanceCount)
{
    public readonly uint FirstIndex = firstIndex;

    public readonly uint IndexCount = indexCount;

    public readonly uint BaseInstance = baseInstance;

    public readonly uint InstanceCount = instanceCount;
}
