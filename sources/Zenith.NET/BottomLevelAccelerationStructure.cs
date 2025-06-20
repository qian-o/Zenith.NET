namespace Zenith.NET;

public abstract class BottomLevelAccelerationStructure(GraphicsContext context, BottomLevelAccelerationStructureDesc desc) : GraphicsResource(context)
{
    private BottomLevelAccelerationStructureDesc desc = desc;

    public ref readonly BottomLevelAccelerationStructureDesc Desc => ref desc;
}
