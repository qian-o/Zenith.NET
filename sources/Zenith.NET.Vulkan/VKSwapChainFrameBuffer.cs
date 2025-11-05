namespace Zenith.NET;

internal unsafe class VKSwapChainFrameBuffer(VKGraphicsContext context, VKSwapChain swapChain) : GraphicsResource(context)
{
    public VKFrameBuffer this[uint index] => throw new NotImplementedException();

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
    }
}
