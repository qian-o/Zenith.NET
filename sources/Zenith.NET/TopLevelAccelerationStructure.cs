namespace Zenith.NET;

public abstract class TopLevelAccelerationStructure(GraphicsContext context, TopLevelAccelerationStructureDesc desc) : GraphicsResource(context)
{
    public TopLevelAccelerationStructureDesc Desc { get; } = desc;
}
