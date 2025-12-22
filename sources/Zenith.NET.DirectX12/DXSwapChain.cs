using Silk.NET.Core.Native;
using Silk.NET.DXGI;

namespace Zenith.NET.DirectX12;

internal unsafe class DXSwapChain : SwapChain
{
    private readonly DXFence fence;
    private readonly DXSwapChainFrameBuffer swapChainFrameBuffer;

    public ComPtr<IDXGISwapChain3> SwapChain3;

    public uint BufferIndex;

    public DXSwapChain(DXGraphicsContext context, SwapChainDesc desc) : base(context, desc)
    {
        fence = new(context);
        swapChainFrameBuffer = new(context, this);

        CreateSwapChain();
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public override FrameBuffer FrameBuffer => swapChainFrameBuffer[BufferIndex];

    public override void Present()
    {
        if (SwapChain3.Handle is not null)
        {
            SwapChain3.Present(0, DXGI.PresentAllowTearing).Success();

            fence.Wait(Context.GraphicsQueue);

            BufferIndex = SwapChain3.GetCurrentBackBufferIndex();
        }
    }

    protected override void ResizeImpl()
    {
        if (SwapChain3.Handle is not null)
        {
            fence.Wait(Context.GraphicsQueue);

            swapChainFrameBuffer.DestroyFrameBuffers();

            SwapChain3.ResizeBuffers(DXGraphicsContext.SwapChainBufferCount,
                                     Desc.Surface.Width,
                                     Desc.Surface.Height,
                                     DXFormats.DirectX12(Desc.ColorTargetFormat),
                                     (uint)SwapChainFlag.AllowTearing).Success();

            swapChainFrameBuffer.CreateFrameBuffers(Desc.Surface.Width, Desc.Surface.Height, []);

            BufferIndex = SwapChain3.GetCurrentBackBufferIndex();
        }
    }

    protected override void RefreshImpl()
    {
        CreateSwapChain();
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        DestroySwapChain();

        swapChainFrameBuffer.Dispose();
        fence.Dispose();
    }

    private void CreateSwapChain()
    {
        DestroySwapChain();

        if (Desc.Surface.Type is not SurfaceType.D3D11Interop)
        {
            SwapChainDesc1 swapChainDesc = new()
            {
                Width = Desc.Surface.Width,
                Height = Desc.Surface.Height,
                Format = DXFormats.DirectX12(Desc.ColorTargetFormat),
                SampleDesc = new(1, 0),
                BufferUsage = DXGI.UsageRenderTargetOutput,
                BufferCount = DXGraphicsContext.SwapChainBufferCount,
                SwapEffect = SwapEffect.FlipDiscard,
                AlphaMode = AlphaMode.Ignore,
                Flags = (uint)SwapChainFlag.AllowTearing
            };

            Context.Factory7.CreateSwapChainForHwnd(Context.GraphicsQueue,
                                                    Desc.Surface.Handles[0],
                                                    &swapChainDesc,
                                                    null,
                                                    (ComPtr<IDXGIOutput>)null,
                                                    ref SwapChain3).Success();

            swapChainFrameBuffer.CreateFrameBuffers(Desc.Surface.Width, Desc.Surface.Height, []);

            BufferIndex = SwapChain3.GetCurrentBackBufferIndex();
        }
        else
        {
            swapChainFrameBuffer.CreateFrameBuffers(Desc.Surface.Width, Desc.Surface.Height, Desc.Surface.Handles);
        }
    }

    private void DestroySwapChain()
    {
        swapChainFrameBuffer.DestroyFrameBuffers();

        SwapChain3.Dispose();
        SwapChain3 = default;

        BufferIndex = 0;
    }
}
