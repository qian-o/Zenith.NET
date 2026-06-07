#if WINDOWS
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using WinRT;

namespace Zenith.NET.Views.WinUI;

public unsafe partial class ZenithView
{
    private D3DTexture? texture;
    private SwapChain? swapChain;

    void IZenithView.EnsureResources()
    {
        if (GraphicsContext is null)
        {
            return;
        }

        uint width = Math.Clamp((uint)Math.Ceiling(ActualWidth), 1, uint.MaxValue);
        uint height = Math.Clamp((uint)Math.Ceiling(ActualHeight), 1, uint.MaxValue);

        if (texture is null || texture.Width != width || texture.Height != height || swapChain is null)
        {
            ((IZenithView)this).ReleaseResources();

            texture = new(width, height);

            swapChain = GraphicsContext.CreateSwapChain(new()
            {
                Surface = Surface.D3D11Interop(texture.SharedHandle, width, height),
                Format = ZenithViewHelper.Format
            });

            this.As<ISwapChainPanelNative>().SetSwapChain(texture.SwapChain);
        }
    }

    void IZenithView.Tick()
    {
        if (texture is null || swapChain is null)
        {
            return;
        }

        texture.AcquireSync();

        UpdateRequested?.Invoke(this, new(scheduler.UpdateSeconds, scheduler.TotalSeconds));
        RenderRequested?.Invoke(this, new(scheduler.RenderSeconds, scheduler.TotalSeconds, swapChain.Drawable));

        texture.ReleaseSync();
    }

    void IZenithView.Present()
    {
        swapChain?.Present();
        texture?.Present();
    }

    void IZenithView.ReleaseResources()
    {
        swapChain?.Dispose();
        swapChain = null;

        texture?.Dispose();
        texture = null;
    }
}

internal static unsafe class D3D
{
    public static ComPtr<IDXGIFactory2> Factory = new();

    public static ComPtr<ID3D11Device> Device = new();

    public static ComPtr<ID3D11DeviceContext> DeviceContext = new();

    static D3D()
    {
        DXGI = DXGI.GetApi(null);
        D3D11 = D3D11.GetApi(null);

        Success(DXGI.CreateDXGIFactory2(0, SilkMarshal.GuidPtrOf<IDXGIFactory2>(), (void**)Factory.GetAddressOf()));

        Success(D3D11.CreateDevice(default,
                                   D3DDriverType.Hardware,
                                   0,
                                   (uint)CreateDeviceFlag.BgraSupport,
                                   default,
                                   0,
                                   D3D11.SdkVersion,
                                   Device.GetAddressOf(),
                                   default,
                                   DeviceContext.GetAddressOf()));
    }

    public static DXGI DXGI { get; }

    public static D3D11 D3D11 { get; }

    public static void Success(int result)
    {
        if (result is not 0)
        {
            Debug.WriteLine($"Direct3D operation failed. HRESULT: 0x{result:X8}");
        }
    }
}

internal unsafe partial class D3DTexture : DisposableObject
{
    [LibraryImport("kernel32")]
    private static partial int CloseHandle(nint hObject);

    public ComPtr<IDXGISwapChain1> SwapChain = new();

    public ComPtr<ID3D11Texture2D> Texture = new();

    public ComPtr<IDXGIKeyedMutex> Mutex = new();

    public nint Handle;

    public nint SharedHandle;

    private ulong key;

    public D3DTexture(uint width, uint height)
    {
        SwapChainDesc1 swapChainDesc = new()
        {
            Width = width,
            Height = height,
            Format = ColorFormat(),
            SampleDesc = new(1),
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
            Format = ColorFormat(),
            SampleDesc = new(1),
            BindFlags = (uint)BindFlag.RenderTarget,
            MiscFlags = (uint)(ResourceMiscFlag.SharedKeyedmutex | ResourceMiscFlag.SharedNthandle)
        };

        D3D.Success(D3D.Device.CreateTexture2D(&texture2DDesc, default(SubresourceData*), Texture.GetAddressOf()));

        D3D.Success(Texture.QueryInterface(SilkMarshal.GuidPtrOf<IDXGIKeyedMutex>(), (void**)Mutex.GetAddressOf()));

        using ComPtr<IDXGIResource1> resource = new();
        D3D.Success(Texture.QueryInterface(SilkMarshal.GuidPtrOf<IDXGIResource1>(), (void**)resource.GetAddressOf()));

        void* sharedHandle = null;
        D3D.Success(resource.CreateSharedHandle(default(SecurityAttributes*), DXGI.SharedResourceRead | DXGI.SharedResourceWrite, default(char*), &sharedHandle));

        Handle = (nint)Texture.Handle;
        SharedHandle = (nint)sharedHandle;

        Width = width;
        Height = height;
    }

    public uint Width { get; }

    public uint Height { get; }

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
        if (CloseHandle(SharedHandle) is 0)
        {
            Debug.WriteLine("Failed to close shared handle.");
        }

        Mutex.Dispose();
        Texture.Dispose();
        SwapChain.Dispose();
    }

    private static Format ColorFormat()
    {
        return ZenithViewHelper.Format switch
        {
            PixelFormat.R8G8B8A8UNorm => Format.FormatR8G8B8A8Unorm,
            PixelFormat.B8G8R8A8UNorm => Format.FormatB8G8R8A8Unorm,
            _ => throw new NotSupportedException($"Pixel format {ZenithViewHelper.Format} is not supported.")
        };
    }
}

[GeneratedComInterface]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("63aad0b8-7c24-40ff-85a8-640d944cc325")]
internal unsafe partial interface ISwapChainPanelNative
{
    void SetSwapChain(IDXGISwapChain1* swapChain);

    ulong Release();
}
#endif