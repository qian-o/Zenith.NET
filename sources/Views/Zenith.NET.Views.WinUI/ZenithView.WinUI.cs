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
                ColorTargetFormat = PixelFormat.B8G8R8A8UNorm,
                DepthStencilTargetFormat = PixelFormat.D24UNormS8UInt
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
        RenderRequested?.Invoke(this, new(scheduler.RenderSeconds, scheduler.TotalSeconds, swapChain.FrameBuffer));

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
    public static ComPtr<IDXGIFactory2> Factory;

    public static ComPtr<ID3D11Device> Device;

    public static ComPtr<ID3D11DeviceContext> DeviceContext;

    static D3D()
    {
        DXGI = DXGI.GetApi(null);
        D3D11 = D3D11.GetApi(null);

        Success(DXGI.CreateDXGIFactory2(0, out Factory));

        Success(D3D11.CreateDevice(default(ComPtr<IDXGIAdapter>),
                                   D3DDriverType.Hardware,
                                   0,
                                   (uint)CreateDeviceFlag.BgraSupport,
                                   null,
                                   0,
                                   D3D11.SdkVersion,
                                   ref Device,
                                   null,
                                   ref DeviceContext));
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
            SampleDesc = new() { Count = 1, Quality = 0 },
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
            SampleDesc = new() { Count = 1, Quality = 0 },
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
        AcquireSync();

        D3D.Success(SwapChain.GetBuffer(0, out ComPtr<ID3D11Texture2D> backBuffer));

        D3D.DeviceContext.CopyResource((ID3D11Resource*)backBuffer.Handle, (ID3D11Resource*)Texture.Handle);
        D3D.DeviceContext.Flush();

        D3D.Success(SwapChain.Present(1, 0));

        backBuffer.Dispose();

        ReleaseSync();
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

[GeneratedComInterface]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("63aad0b8-7c24-40ff-85a8-640d944cc325")]
internal unsafe partial interface ISwapChainPanelNative
{
    void SetSwapChain(IDXGISwapChain1* swapChain);

    ulong Release();
}
#endif