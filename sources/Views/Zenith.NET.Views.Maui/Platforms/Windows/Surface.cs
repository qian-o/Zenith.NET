using System.Diagnostics;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;

namespace Zenith.NET.Views.Maui.Platforms.Windows;

internal unsafe partial class Surface : DisposableObject
{
    [LibraryImport("kernel32")]
    private static partial int CloseHandle(nint hObject);

    public ComPtr<IDXGISwapChain1> SwapChain = new();

    public ComPtr<ID3D11Texture2D> Texture = new();

    public ComPtr<IDXGIKeyedMutex> Mutex = new();

    public nint SharedHandle;

    private ulong key;

    public Surface(GraphicsContext graphicsContext, uint width, uint height)
    {
        SwapChainDesc1 swapChainDesc = new()
        {
            Width = width,
            Height = height,
            Format = DrawableFormat(),
            SampleDesc = new() { Count = 1 },
            BufferUsage = DXGI.UsageRenderTargetOutput,
            BufferCount = 3,
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipSequential
        };

        D3D.Success(D3D.Factory.CreateSwapChainForComposition((IUnknown*)D3D.Device.Handle, &swapChainDesc, default(IDXGIOutput*), SwapChain.GetAddressOf()));

        Texture2DDesc texture2DDesc = new()
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = DrawableFormat(),
            SampleDesc = new() { Count = 1 },
            BindFlags = (uint)BindFlag.RenderTarget,
            MiscFlags = (uint)(ResourceMiscFlag.SharedKeyedmutex | ResourceMiscFlag.SharedNthandle)
        };

        D3D.Success(D3D.Device.CreateTexture2D(&texture2DDesc, default(SubresourceData*), Texture.GetAddressOf()));

        D3D.Success(Texture.QueryInterface(SilkMarshal.GuidPtrOf<IDXGIKeyedMutex>(), (void**)Mutex.GetAddressOf()));

        using ComPtr<IDXGIResource1> resource = new();
        D3D.Success(Texture.QueryInterface(SilkMarshal.GuidPtrOf<IDXGIResource1>(), (void**)resource.GetAddressOf()));

        void* sharedHandle = null;
        D3D.Success(resource.CreateSharedHandle(default(SecurityAttributes*), DXGI.SharedResourceRead | DXGI.SharedResourceWrite, default(char*), &sharedHandle));

        Drawable = graphicsContext.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = ZenithViewHelper.DrawableFormat,
            Width = Width = width,
            Height = Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Usages = TextureUsages.ColorAttachment | TextureUsages.CopyDst
        }, NativeTextureType.D3D11TextureNtHandle, SharedHandle = (nint)sharedHandle);
    }

    public uint Width { get; }

    public uint Height { get; }

    public Texture Drawable { get; }

    public void AcquireSync()
    {
        D3D.Success(Mutex.AcquireSync(key++, uint.MaxValue));
    }

    public void ReleaseSync()
    {
        D3D.Success(Mutex.ReleaseSync(key));
    }

    public void Present()
    {
        using ComPtr<ID3D11Texture2D> backBuffer = new();
        D3D.Success(SwapChain.GetBuffer(0, SilkMarshal.GuidPtrOf<ID3D11Texture2D>(), (void**)backBuffer.GetAddressOf()));

        AcquireSync();

        D3D.DeviceContext.CopyResource((ID3D11Resource*)backBuffer.Handle, (ID3D11Resource*)Texture.Handle);
        D3D.DeviceContext.Flush();

        ReleaseSync();

        D3D.Success(SwapChain.Present(1, 0));
    }

    protected override void Destroy()
    {
        Drawable.Dispose();

        if (CloseHandle(SharedHandle) is 0)
        {
            Debug.WriteLine("Failed to close shared handle.");
        }

        Mutex.Dispose();
        Texture.Dispose();
        SwapChain.Dispose();
    }

    private static Format DrawableFormat()
    {
        return ZenithViewHelper.DrawableFormat switch
        {
            PixelFormat.R8G8B8A8UNorm => Format.FormatR8G8B8A8Unorm,
            PixelFormat.B8G8R8A8UNorm => Format.FormatB8G8R8A8Unorm,
            _ => default
        };
    }
}