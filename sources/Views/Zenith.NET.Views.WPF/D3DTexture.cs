using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.Direct3D9;
using Silk.NET.DXGI;
using Format = Silk.NET.Direct3D9.Format;
using PresentParameters = Silk.NET.Direct3D9.PresentParameters;

namespace Zenith.NET.Views.WPF;

internal unsafe class D3DTexture : DisposableObject
{
    public static class GPU
    {
        public static ComPtr<IDirect3D9Ex> D3D9Ex;

        public static ComPtr<IDirect3DDevice9Ex> D3D9DeviceEx;

        public static ComPtr<ID3D11Device> D3D11Device;

        public static ComPtr<ID3D11DeviceContext> D3D11DeviceContext;

        static GPU()
        {
            D3D9 = D3D9.GetApi(null);
            D3D11 = D3D11.GetApi(null);

            D3D9.Direct3DCreate9Ex(D3D9.SdkVersion, ref D3D9Ex);

            PresentParameters present = new()
            {
                BackBufferWidth = 1,
                BackBufferHeight = 1,
                BackBufferFormat = Format.X8R8G8B8,
                BackBufferCount = 1,
                SwapEffect = Swapeffect.Discard,
                Windowed = 1
            };

            D3D9Ex.CreateDeviceEx(0,
                                  Devtype.Hal,
                                  0,
                                  D3D9.CreateHardwareVertexprocessing | D3D9.CreateMultithreaded | D3D9.CreatePuredevice | D3D9.CreateFpuPreserve,
                                  &present,
                                  (Displaymodeex*)null,
                                  ref D3D9DeviceEx);

            D3D11.CreateDevice(default(ComPtr<IDXGIAdapter>),
                               D3DDriverType.Hardware,
                               0,
                               (uint)CreateDeviceFlag.BgraSupport,
                               null,
                               0,
                               D3D11.SdkVersion,
                               ref D3D11Device,
                               null,
                               ref D3D11DeviceContext);
        }

        public static D3D9 D3D9 { get; }

        public static D3D11 D3D11 { get; }
    }

    public ComPtr<IDirect3DTexture9> D3D9Texture;

    public ComPtr<IDirect3DSurface9> D3D9Surface;

    public ComPtr<ID3D11Texture2D> D3D11Texture;

    public nint Handle;

    public nint SharedHandle;

    public D3DTexture(uint width, uint height)
    {
        void* d3d9ShareHandle = null;
        GPU.D3D9DeviceEx.CreateTexture(width,
                                       height,
                                       1,
                                       D3D9.UsageRendertarget,
                                       Silk.NET.Direct3D9.Format.X8R8G8B8,
                                       Pool.Default,
                                       ref D3D9Texture,
                                       &d3d9ShareHandle);

        D3D9Texture.GetSurfaceLevel(0, ref D3D9Surface);

        GPU.D3D11Device.OpenSharedResource(d3d9ShareHandle, out D3D11Texture);

        void* d3d11SharedHandle = null;
        using ComPtr<IDXGIResource> resource = D3D11Texture.QueryInterface<IDXGIResource>();
        resource.GetSharedHandle(&d3d11SharedHandle);

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