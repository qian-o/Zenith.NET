using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Silk.NET.DXGI;

namespace Zenith.NET.Views.Maui.Platforms.Windows;

[GeneratedComInterface]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("63aad0b8-7c24-40ff-85a8-640d944cc325")]
internal unsafe partial interface ISwapChainPanelNative
{
    void SetSwapChain(IDXGISwapChain1* swapChain);

    ulong Release();
}