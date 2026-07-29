using System.Diagnostics;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;

namespace Zenith.NET.Views.Maui.Platforms.Windows;

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