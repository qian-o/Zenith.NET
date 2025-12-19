namespace Zenith.NET.DirectX12;

internal class DXSwapChainFrameBuffer(DXGraphicsContext context, DXSwapChain swapChain) : GraphicsResource(context)
{
    public DXFrameBuffer this[uint index] => throw new NotImplementedException();

    public void CreateFrameBuffers(uint width, uint height, nint[] handles)
    {
        throw new NotImplementedException();
    }

    public void DestroyFrameBuffers()
    {
        throw new NotImplementedException();
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        throw new NotImplementedException();
    }
}
