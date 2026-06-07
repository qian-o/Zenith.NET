using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.Direct3D9;
using Silk.NET.DXGI;
using D3D9Format = Silk.NET.Direct3D9.Format;
using DXGIFormat = Silk.NET.DXGI.Format;

namespace Zenith.NET.Views.WPF;

internal unsafe partial class D3DTexture : DisposableObject
{
    [LibraryImport("kernel32")]
    private static partial int CloseHandle(nint hObject);

    public ComPtr<IDirect3DTexture9> D3D9RenderTarget = new();

    public ComPtr<IDirect3DSurface9> D3D9RenderSurface = new();

    public ComPtr<ID3D11Texture2D> D3D9SharedTexture = new();

    public ComPtr<ID3D11Texture2D> D3D11RenderTarget = new();

    public ComPtr<IDXGIKeyedMutex> D3D11Mutex = new();

    public nint SharedHandle;

    private ulong key;

    public D3DTexture(uint width, uint height, D3DImage image)
    {
        void* sharedHandle = null;
        D3D.Success(D3D.D3D9DeviceEx.CreateTexture(width,
                                                   height,
                                                   1,
                                                   D3D9.UsageRendertarget,
                                                   D3D9Format.A8R8G8B8,
                                                   Pool.Default,
                                                   D3D9RenderTarget.GetAddressOf(),
                                                   &sharedHandle));

        D3D.Success(D3D9RenderTarget.GetSurfaceLevel(0, D3D9RenderSurface.GetAddressOf()));
        D3D.Success(D3D.D3D11Device.OpenSharedResource(sharedHandle, SilkMarshal.GuidPtrOf<ID3D11Texture2D>(), (void**)D3D9SharedTexture.GetAddressOf()));

        Texture2DDesc desc = new()
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = ColorFormat(),
            SampleDesc = new(1),
            BindFlags = (uint)BindFlag.RenderTarget,
            MiscFlags = (uint)(ResourceMiscFlag.SharedKeyedmutex | ResourceMiscFlag.SharedNthandle)
        };

        D3D.Success(D3D.D3D11Device.CreateTexture2D(&desc, default(SubresourceData*), D3D11RenderTarget.GetAddressOf()));

        D3D.Success(D3D11RenderTarget.QueryInterface(SilkMarshal.GuidPtrOf<IDXGIKeyedMutex>(), (void**)D3D11Mutex.GetAddressOf()));

        using ComPtr<IDXGIResource1> resource = new();
        D3D.Success(D3D11RenderTarget.QueryInterface(SilkMarshal.GuidPtrOf<IDXGIResource1>(), (void**)resource.GetAddressOf()));

        sharedHandle = null;
        D3D.Success(resource.CreateSharedHandle(default(SecurityAttributes*), DXGI.SharedResourceRead | DXGI.SharedResourceWrite, default(char*), &sharedHandle));

        SharedHandle = (nint)sharedHandle;

        Width = width;
        Height = height;
        Image = image;
    }

    public uint Width { get; }

    public uint Height { get; }

    public D3DImage Image { get; }

    public void AcquireSync()
    {
        D3D.Success(D3D11Mutex.AcquireSync(key++, uint.MaxValue));
    }

    public void ReleaseSync()
    {
        D3D.Success(D3D11Mutex.ReleaseSync(key));
    }

    public void Present()
    {
        Image.Lock();
        Image.SetBackBuffer(D3DResourceType.IDirect3DSurface9, (nint)D3D9RenderSurface.Handle);

        AcquireSync();

        D3D.D3D11DeviceContext.CopyResource((ID3D11Resource*)D3D9SharedTexture.Handle, (ID3D11Resource*)D3D11RenderTarget.Handle);
        D3D.D3D11DeviceContext.Flush();

        ReleaseSync();

        Image.AddDirtyRect(new(0, 0, (int)Width, (int)Height));
        Image.Unlock();
    }

    protected override void Destroy()
    {
        if (CloseHandle(SharedHandle) is 0)
        {
            Debug.WriteLine("Failed to close shared handle.");
        }

        D3D11Mutex.Dispose();
        D3D11RenderTarget.Dispose();
        D3D9SharedTexture.Dispose();
        D3D9RenderSurface.Dispose();
        D3D9RenderTarget.Dispose();
    }

    private static DXGIFormat ColorFormat()
    {
        return ZenithViewHelper.Format switch
        {
            PixelFormat.R8G8B8A8UNorm => DXGIFormat.FormatR8G8B8A8Unorm,
            PixelFormat.B8G8R8A8UNorm => DXGIFormat.FormatB8G8R8A8Unorm,
            _ => throw new NotSupportedException($"Pixel format {ZenithViewHelper.Format} is not supported.")
        };
    }
}