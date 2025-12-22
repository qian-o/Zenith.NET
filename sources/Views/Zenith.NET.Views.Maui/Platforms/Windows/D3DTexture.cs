using System.Diagnostics;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;

namespace Zenith.NET.Views.Maui.Platforms.Windows;

internal unsafe partial class D3DTexture : DisposableObject
{
    [LibraryImport("kernel32")]
    private static partial int CloseHandle(nint hObject);

    public ComPtr<IDXGISwapChain1> SwapChain;

    public ComPtr<ID3D11Texture2D> Texture;

    public ComPtr<IDXGIKeyedMutex> Mutex;

    public nint Handle;

    public nint SharedHandle;

    private ulong key;

    public D3DTexture(uint width, uint height)
    {
        SwapChainDesc1 swapChainDesc = new()
        {
            Width = width,
            Height = height,
            Format = Format.FormatB8G8R8A8Unorm,
            SampleDesc = new SampleDesc { Count = 1, Quality = 0 },
            BufferUsage = DXGI.UsageRenderTargetOutput,
            BufferCount = 3,
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipSequential
        };

        D3D.Success(D3D.Factory.CreateSwapChainForComposition((IUnknown*)D3D.Device.Handle, &swapChainDesc, (IDXGIOutput*)null, SwapChain.GetAddressOf()));

        Texture2DDesc texture2DDesc = new()
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.FormatB8G8R8A8Unorm,
            SampleDesc = new SampleDesc { Count = 1, Quality = 0 },
            BindFlags = (uint)BindFlag.RenderTarget,
            MiscFlags = (uint)(ResourceMiscFlag.SharedNthandle | ResourceMiscFlag.SharedKeyedmutex)
        };

        D3D.Success(D3D.Device.CreateTexture2D(&texture2DDesc, null, ref Texture));

        D3D.Success(Texture.QueryInterface(out Mutex));

        using ComPtr<IDXGIResource1> resource = Texture.QueryInterface<IDXGIResource1>();

        void* sharedHandle = null;
        D3D.Success(resource.CreateSharedHandle((SecurityAttributes*)null, DXGI.SharedResourceRead | DXGI.SharedResourceWrite, (char*)null, &sharedHandle));

        Handle = (nint)Texture.Handle;
        SharedHandle = (nint)sharedHandle;

        Width = width;
        Height = height;
    }

    public uint Width { get; }

    public uint Height { get; }

    public void AcquireForUpdate()
    {
        D3D.Success(Mutex.AcquireSync(key++, uint.MaxValue));
    }

    public void PresentAndRelease()
    {
        D3D.Success(SwapChain.GetBuffer(0, out ComPtr<ID3D11Texture2D> backBuffer));

        D3D.DeviceContext.CopyResource((ID3D11Resource*)backBuffer.Handle, (ID3D11Resource*)Texture.Handle);
        D3D.DeviceContext.Flush();

        D3D.Success(SwapChain.Present(1, 0));

        backBuffer.Dispose();

        D3D.Success(Mutex.ReleaseSync(key));
    }

    protected override void Destroy()
    {
        if (CloseHandle(SharedHandle) is 0)
        {
            Debug.WriteLine("Failed to close shared handle.");
        }

        Mutex.Dispose();
        Texture.Dispose();
        SwapChain.Dispose();
    }
}