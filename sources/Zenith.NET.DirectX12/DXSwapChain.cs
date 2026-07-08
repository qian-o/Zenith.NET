using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Zenith.NET.DirectX12;

internal unsafe class DXSwapChain : SwapChain
{
    private readonly DXTexture[] textures = new DXTexture[3];

    public ComPtr<IDXGISwapChain3> SwapChain;

    private uint index;

    public DXSwapChain(DXGraphicsContext context, SwapChainDesc desc) : base(context, desc)
    {
        CreateSwapChain();
        CreateTextures();
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public override Texture Drawable => textures[index];

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    public override void Present()
    {
        SwapChain.Present(0, DXGI.PresentAllowTearing).Success();

        index = SwapChain.GetCurrentBackBufferIndex();
    }

    protected override void ResizeImpl()
    {
        DestroyTextures();

        SwapChain.ResizeBuffers((uint)textures.Length, Desc.Surface.Width, Desc.Surface.Height, DXFormats.DirectX12(Desc.Format), (uint)SwapChainFlag.AllowTearing).Success();

        CreateTextures();
    }

    protected override void RefreshImpl()
    {
        DestroyTextures();
        DestroySwapChain();

        CreateSwapChain();
        CreateTextures();
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        base.Destroy();

        DestroyTextures();
        DestroySwapChain();
    }

    private void CreateSwapChain()
    {
        SwapChainDesc1 swapChainDesc = new()
        {
            Width = Desc.Surface.Width,
            Height = Desc.Surface.Height,
            Format = DXFormats.DirectX12(Desc.Format),
            SampleDesc = new() { Count = 1 },
            BufferUsage = DXGI.UsageRenderTargetOutput,
            BufferCount = (uint)textures.Length,
            SwapEffect = SwapEffect.FlipDiscard,
            AlphaMode = AlphaMode.Ignore,
            Flags = (uint)SwapChainFlag.AllowTearing
        };

        Context.Factory.CreateSwapChainForHwnd((IUnknown*)Context.GraphicsQueue.DirectX12().CommandQueue.Handle,
                                               Desc.Surface.Handles[0],
                                               &swapChainDesc,
                                               default(SwapChainFullscreenDesc*),
                                               default(IDXGIOutput*),
                                               (IDXGISwapChain1**)SwapChain.GetAddressOf()).Success();
    }

    private void DestroySwapChain()
    {
        SwapChain.Dispose();

        index = 0;
    }

    private void CreateTextures()
    {
        TextureDesc desc = new()
        {
            Type = TextureType.Texture2D,
            Format = Desc.Format,
            Width = Desc.Surface.Width,
            Height = Desc.Surface.Height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Usages = TextureUsages.ColorAttachment | TextureUsages.TransferDst
        };

        for (int i = 0; i < textures.Length; i++)
        {
            ComPtr<ID3D12Resource> resource = new();
            SwapChain.GetBuffer((uint)i, SilkMarshal.GuidPtrOf<ID3D12Resource>(), (void**)resource.GetAddressOf()).Success();

            textures[i] = new(Context, desc, resource);
        }

        index = SwapChain.GetCurrentBackBufferIndex();
    }

    private void DestroyTextures()
    {
        for (int i = 0; i < textures.Length; i++)
        {
            textures[i].Dispose();
        }
    }
}
