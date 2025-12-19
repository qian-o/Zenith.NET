namespace Zenith.NET.DirectX12;

internal class DXSwapChain : SwapChain
{
    public DXSwapChain(DXGraphicsContext context, SwapChainDesc desc) : base(context, desc)
    {
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public override FrameBuffer FrameBuffer { get; }

    public override void Present()
    {
        throw new NotImplementedException();
    }

    protected override void ResizeImpl()
    {
        throw new NotImplementedException();
    }

    protected override void RefreshImpl()
    {
        throw new NotImplementedException();
    }

    protected override void SetResourceName(string name)
    {
        throw new NotImplementedException();
    }

    protected override void Destroy()
    {
        throw new NotImplementedException();
    }
}
