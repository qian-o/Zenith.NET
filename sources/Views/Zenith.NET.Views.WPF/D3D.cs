using System.Diagnostics;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.Direct3D9;
using Format = Silk.NET.Direct3D9.Format;
using PresentParameters = Silk.NET.Direct3D9.PresentParameters;

namespace Zenith.NET.Views.WPF;

internal static unsafe class D3D
{
    public static ComPtr<IDirect3D9Ex> D3D9Ex = new();

    public static ComPtr<IDirect3DDevice9Ex> D3D9DeviceEx = new();

    public static ComPtr<ID3D11Device> D3D11Device = new();

    public static ComPtr<ID3D11DeviceContext> D3D11DeviceContext = new();

    static D3D()
    {
        D3D9 = D3D9.GetApi(null);
        D3D11 = D3D11.GetApi(null);

        Success(D3D9.Direct3DCreate9Ex(D3D9.SdkVersion, D3D9Ex.GetAddressOf()));

        PresentParameters present = new()
        {
            BackBufferWidth = 1,
            BackBufferHeight = 1,
            BackBufferFormat = Format.X8R8G8B8,
            BackBufferCount = 1,
            SwapEffect = Swapeffect.Discard,
            Windowed = 1
        };

        Success(D3D9Ex.CreateDeviceEx(0,
                                      Devtype.Hal,
                                      0,
                                      D3D9.CreateHardwareVertexprocessing | D3D9.CreateMultithreaded | D3D9.CreatePuredevice | D3D9.CreateFpuPreserve,
                                      &present,
                                      default(Displaymodeex*),
                                      D3D9DeviceEx.GetAddressOf()));

        Success(D3D11.CreateDevice(default,
                                   D3DDriverType.Hardware,
                                   0,
                                   (uint)CreateDeviceFlag.BgraSupport,
                                   default,
                                   0,
                                   D3D11.SdkVersion,
                                   D3D11Device.GetAddressOf(),
                                   default,
                                   D3D11DeviceContext.GetAddressOf()));
    }

    public static D3D9 D3D9 { get; }

    public static D3D11 D3D11 { get; }

    public static void Success(int result)
    {
        if (result is not 0)
        {
            Debug.WriteLine($"Direct3D operation failed. HRESULT: 0x{result:X8}");
        }
    }
}
