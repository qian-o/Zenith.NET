using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Zenith.NET.DirectX12;

internal unsafe class DXSwapChain : SwapChain
{
    public ComPtr<IDXGISwapChain3> SwapChain;

    private DXTexture[] textures = [];
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

    protected override void AcquireImpl()
    {
        if (SwapChain.Handle is not null)
        {
            index = SwapChain.GetCurrentBackBufferIndex();
        }
    }

    protected override void PresentImpl()
    {
        if (SwapChain.Handle is not null)
        {
            SwapChain.Present(0, DXGI.PresentAllowTearing).Success();
        }
    }

    protected override void ResizeImpl()
    {
        DestroyTextures();

        if (SwapChain.Handle is not null)
        {
            SwapChain.ResizeBuffers((uint)textures.Length, Desc.Surface.Width, Desc.Surface.Height, DXFormats.DirectX12(Desc.Format), (uint)SwapChainFlag.AllowTearing).Success();
        }

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
        DestroyTextures();
        DestroySwapChain();
    }

    private void CreateSwapChain()
    {
        if (Desc.Surface.Type is not SurfaceType.D3D11Interop)
        {
            const uint BufferCount = 3;

            SwapChainDesc1 swapChainDesc = new()
            {
                Width = Desc.Surface.Width,
                Height = Desc.Surface.Height,
                Format = DXFormats.DirectX12(Desc.Format),
                SampleDesc = new()
                {
                    Count = 1
                },
                BufferUsage = DXGI.UsageRenderTargetOutput,
                BufferCount = BufferCount,
                SwapEffect = SwapEffect.FlipDiscard,
                AlphaMode = AlphaMode.Ignore,
                Flags = (uint)SwapChainFlag.AllowTearing
            };

            Context.Factory.CreateSwapChainForHwnd((IUnknown*)Context.GraphicsQueue.DirectX12().CommandQueue.Handle,
                                                   Desc.Surface.NativeHandles[0],
                                                   &swapChainDesc,
                                                   default(SwapChainFullscreenDesc*),
                                                   default(IDXGIOutput*),
                                                   (IDXGISwapChain1**)SwapChain.GetAddressOf()).Success();

            textures = new DXTexture[BufferCount];
        }
        else
        {
            textures = new DXTexture[1];
        }
    }

    private void DestroySwapChain()
    {
        SwapChain.Dispose();
        SwapChain = default;

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
            Usages = TextureUsages.ColorAttachment
        };

        if (SwapChain.Handle is not null)
        {
            for (int i = 0; i < textures.Length; i++)
            {
                ComPtr<ID3D12Resource> resource = new();
                SwapChain.GetBuffer((uint)i, SilkMarshal.GuidPtrOf<ID3D12Resource>(), (void**)resource.GetAddressOf());

                textures[i] = new(Context, desc, resource);
            }
        }
        else
        {
            Context.Device.OpenSharedHandle((void*)Desc.Surface.NativeHandles[0], out ComPtr<ID3D12Resource> resource).Success();

            textures[0] = new(Context, desc, resource);
        }
    }

    private void DestroyTextures()
    {
        for (int i = 0; i < textures.Length; i++)
        {
            textures[i].Dispose();
        }
    }
}
