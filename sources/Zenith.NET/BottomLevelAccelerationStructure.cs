namespace Zenith.NET;

public abstract class BottomLevelAccelerationStructure(GraphicsContext context, BottomLevelAccelerationStructureDesc desc) : GraphicsResource(context)
{
    public BottomLevelAccelerationStructureDesc Desc { get; } = desc;
}
