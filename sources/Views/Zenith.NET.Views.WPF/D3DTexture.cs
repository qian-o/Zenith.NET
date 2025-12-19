using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.Direct3D9;
using Silk.NET.DXGI;
using D3D9Format = Silk.NET.Direct3D9.Format;
using DXGIFormat = Silk.NET.DXGI.Format;

namespace Zenith.NET.Views.WPF;

internal unsafe class D3DTexture : DisposableObject
{
    public ComPtr<IDirect3DTexture9> D3D9RenderTarget;

    public ComPtr<IDirect3DSurface9> D3D9RenderSurface;

    public ComPtr<ID3D11Texture2D> D3D9SharedTexture;

    public ComPtr<ID3D11Texture2D> D3D11RenderTarget;

    public ComPtr<IDXGIKeyedMutex> D3D11Mutex;

    public nint Handle;

    public nint SharedHandle;

    private ulong mutexKey;

    public D3DTexture(uint width, uint height)
    {
        void* sharedHandle = null;
        D3D.Success(D3D.D3D9DeviceEx.CreateTexture(width,
                                                   height,
                                                   1,
                                                   D3D9.UsageRendertarget,
                                                   D3D9Format.X8R8G8B8,
                                                   Pool.Default,
                                                   ref D3D9RenderTarget,
                                                   &sharedHandle));

        D3D.Success(D3D9RenderTarget.GetSurfaceLevel(0, ref D3D9RenderSurface));
        D3D.Success(D3D.D3D11Device.OpenSharedResource(sharedHandle, out D3D9SharedTexture));

        Texture2DDesc desc = new()
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = DXGIFormat.FormatB8G8R8X8Unorm,
            SampleDesc = new SampleDesc { Count = 1, Quality = 0 },
            Usage = Usage.Default,
            MiscFlags = (uint)(ResourceMiscFlag.SharedNthandle | ResourceMiscFlag.SharedKeyedmutex)
        };

        D3D.Success(D3D.D3D11Device.CreateTexture2D(&desc, null, ref D3D11RenderTarget));

        D3D.Success(D3D11RenderTarget.QueryInterface(out D3D11Mutex));

        using ComPtr<IDXGIResource1> resource = D3D11RenderTarget.QueryInterface<IDXGIResource1>();

        sharedHandle = null;
        D3D.Success(resource.CreateSharedHandle((SecurityAttributes*)null, DXGI.SharedResourceRead | DXGI.SharedResourceWrite, (char*)null, &sharedHandle));

        Handle = (nint)D3D9RenderSurface.Handle;
        SharedHandle = (nint)sharedHandle;

        Width = width;
        Height = height;
    }

    public uint Width { get; }

    public uint Height { get; }

    public void AcquireMutex()
    {
        D3D.Success(D3D11Mutex.AcquireSync(mutexKey++, uint.MaxValue));
    }

    public void ReleaseMutex()
    {
        D3D.Success(D3D11Mutex.ReleaseSync(mutexKey));
    }

    public void Present()
    {
        AcquireMutex();

        D3D.D3D11DeviceContext.CopyResource((ID3D11Resource*)D3D9SharedTexture.Handle, (ID3D11Resource*)D3D11RenderTarget.Handle);
        D3D.D3D11DeviceContext.Flush();

        ReleaseMutex();
    }

    protected override void Destroy()
    {
        D3D11Mutex.Dispose();
        D3D11RenderTarget.Dispose();
        D3D9SharedTexture.Dispose();
        D3D9RenderSurface.Dispose();
        D3D9RenderTarget.Dispose();
    }
}