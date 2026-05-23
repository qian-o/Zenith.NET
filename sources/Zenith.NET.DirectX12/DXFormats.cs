using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal static class DXFormats
{
    public static ResourceFlags DirectX12(BufferUsages bufferUsages)
    {
        ResourceFlags result = ResourceFlags.None;

        if (bufferUsages.HasFlag(BufferUsages.StorageReadWrite))
        {
            result |= ResourceFlags.AllowUnorderedAccess;
        }

        if (bufferUsages.HasFlag(BufferUsages.AccelerationStructure))
        {
            result |= ResourceFlags.RaytracingAccelerationStructure;
        }

        return result;
    }

    public static DxHeapType DirectX12(BufferAccess bufferAccess)
    {
        return bufferAccess switch
        {
            BufferAccess.GpuOnly => DxHeapType.Default,
            BufferAccess.CpuReadOnly => DxHeapType.Readback,
            BufferAccess.CpuWriteOnly => DxHeapType.Upload,
            _ => DxHeapType.Default
        };
    }
}
