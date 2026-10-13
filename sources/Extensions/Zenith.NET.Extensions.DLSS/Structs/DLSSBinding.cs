using NGX.NET;

namespace Zenith.NET.Extensions.DLSS;

public readonly struct DLSSBinding(nint directX12, NGXResourceVK vulkan, NGXDimensions size)
{
    internal readonly nint DirectX12 = directX12;

    internal readonly NGXResourceVK Vulkan = vulkan;

    internal readonly NGXDimensions Size = size;
}
