namespace Zenith.NET.Extensions.Reconstruction;

public abstract class FrameGenerator : DisposableObject
{
    private readonly FrameGeneratorDesc desc;

    internal FrameGenerator(FrameGeneratorDesc desc)
    {
        this.desc = desc;
    }

    public ref readonly FrameGeneratorDesc Desc => ref desc;

    public void Dispatch(CommandBuffer commandBuffer, FrameGeneratorArgs args)
    {
        commandBuffer.BeginExternalCommands();
        commandBuffer.BeginDebugEvent("Frame Generator");

        DispatchImpl(commandBuffer, args);

        commandBuffer.EndDebugEvent();
        commandBuffer.EndExternalCommands();
    }

    protected abstract void DispatchImpl(CommandBuffer commandBuffer, FrameGeneratorArgs args);
}
