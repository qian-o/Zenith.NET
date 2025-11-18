using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.Direct3D9;
using Silk.NET.DXGI;

namespace Zenith.NET.Views.WPF;

internal unsafe class D3DTexture : DisposableObject
{
    public ComPtr<IDirect3DTexture9> D3D9Texture;

    public ComPtr<IDirect3DSurface9> D3D9Surface;

    public ComPtr<ID3D11Texture2D> D3D11Texture;

    public nint Handle;

    public nint SharedHandle;

    public D3DTexture(uint width, uint height)
    {
        void* d3d9ShareHandle = null;
        D3D.Success(D3D.D3D9DeviceEx.CreateTexture(width,
                                                   height,
                                                   1,
                                                   D3D9.UsageRendertarget,
                                                   Silk.NET.Direct3D9.Format.X8R8G8B8,
                                                   Pool.Default,
                                                   ref D3D9Texture,
                                                   &d3d9ShareHandle));

        D3D.Success(D3D9Texture.GetSurfaceLevel(0, ref D3D9Surface));
        D3D.Success(D3D.D3D11Device.OpenSharedResource(d3d9ShareHandle, out D3D11Texture));

        using ComPtr<IDXGIResource> resource = D3D11Texture.QueryInterface<IDXGIResource>();

        void* d3d11SharedHandle = null;
        D3D.Success(resource.GetSharedHandle(&d3d11SharedHandle));

        Handle = (nint)D3D9Surface.Handle;
        SharedHandle = (nint)d3d11SharedHandle;

        Width = width;
        Height = height;
    }

    public uint Width { get; }

    public uint Height { get; }

    protected override void Destroy()
    {
        D3D11Texture.Dispose();
        D3D9Surface.Dispose();
        D3D9Texture.Dispose();
    }
}