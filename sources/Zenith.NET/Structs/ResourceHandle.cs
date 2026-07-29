using System.Runtime.InteropServices;

namespace Zenith.NET;

[StructLayout(LayoutKind.Sequential)]
public readonly struct ResourceHandle(uint x, uint y)
{
    public readonly uint X = x;

    public readonly uint Y = y;
}
