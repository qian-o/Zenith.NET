using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.Direct3D9;
using Silk.NET.DXGI;
using D3D9Format = Silk.NET.Direct3D9.Format;
using DXGIFormat = Silk.NET.DXGI.Format;

namespace Zenith.NET.Views.WPF;

internal unsafe class D3DTexture : DisposableObject
{
    public ComPtr<IDirect3DTexture9> D3D9Texture;

    public ComPtr<IDirect3DSurface9> D3D9Surface;

    public ComPtr<ID3D11Texture2D> D3D11Texture;

    public ComPtr<ID3D11Texture2D> Texture;

    public ComPtr<IDXGIKeyedMutex> Mutex;

    public nint Handle;

    public nint SharedHandle;

    private ulong key;

    public D3DTexture(uint width, uint height)
    {
        void* d3d9SharedHandle = null;
        D3D.Success(D3D.D3D9DeviceEx.CreateTexture(width,
                                                   height,
                                                   1,
                                                   D3D9.UsageRendertarget,
                                                   D3D9Format.X8R8G8B8,
                                                   Pool.Default,
                                                   ref D3D9Texture,
                                                   &d3d9SharedHandle));

        D3D.Success(D3D9Texture.GetSurfaceLevel(0, ref D3D9Surface));
        D3D.Success(D3D.D3D11Device.OpenSharedResource(d3d9SharedHandle, out D3D11Texture));

        Texture2DDesc texture2DDesc = new()
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = DXGIFormat.FormatB8G8R8A8Unorm,
            SampleDesc = new SampleDesc { Count = 1, Quality = 0 },
            Usage = Usage.Default,
            MiscFlags = (uint)(ResourceMiscFlag.SharedNthandle | ResourceMiscFlag.SharedKeyedmutex)
        };

        D3D.Success(D3D.D3D11Device.CreateTexture2D(&texture2DDesc, null, ref Texture));

        D3D.Success(Texture.QueryInterface(out Mutex));

        using ComPtr<IDXGIResource1> resource = Texture.QueryInterface<IDXGIResource1>();

        void* d3d11SharedHandle = null;
        D3D.Success(resource.CreateSharedHandle((SecurityAttributes*)null, DXGI.SharedResourceRead | DXGI.SharedResourceWrite, (char*)null, &d3d11SharedHandle));

        Handle = (nint)D3D9Surface.Handle;
        SharedHandle = (nint)d3d11SharedHandle;

        Width = width;
        Height = height;
    }

    public uint Width { get; }

    public uint Height { get; }

    public void AcquireMutex()
    {
        D3D.Success(Mutex.AcquireSync(key++, uint.MaxValue));
    }

    public void ReleaseMutex()
    {
        D3D.Success(Mutex.ReleaseSync(key));
    }

    public void Present()
    {
        AcquireMutex();

        D3D.D3D11DeviceContext.CopyResource((ID3D11Resource*)D3D11Texture.Handle, (ID3D11Resource*)Texture.Handle);
        D3D.D3D11DeviceContext.Flush();

        ReleaseMutex();
    }

    protected override void Destroy()
    {
        Mutex.Dispose();
        Texture.Dispose();
        D3D11Texture.Dispose();
        D3D9Surface.Dispose();
        D3D9Texture.Dispose();
    }
}