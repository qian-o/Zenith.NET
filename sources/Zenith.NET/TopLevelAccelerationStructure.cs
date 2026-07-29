namespace Zenith.NET;

public abstract class TopLevelAccelerationStructure(GraphicsContext context, TopLevelAccelerationStructureDesc desc) : GraphicsResource(context)
{
    private TopLevelAccelerationStructureDesc desc = desc;

    public ref readonly TopLevelAccelerationStructureDesc Desc => ref desc;

    public abstract ResourceHandle Handle { get; }

    internal void Refresh(TopLevelAccelerationStructureDesc newDesc)
    {
        desc = newDesc;
    }
}
