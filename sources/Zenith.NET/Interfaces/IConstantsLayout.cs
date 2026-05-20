namespace Zenith.NET;

public interface IConstantsLayout<T> where T : unmanaged, IConstantsLayout<T>
{
    static abstract uint DirectX12SizeInBytes { get; }

    static abstract uint MetalSizeInBytes { get; }

    static abstract uint VulkanSizeInBytes { get; }

    static abstract void WriteDirectX12(T data, Buffer dst);

    static abstract void WriteMetal(T data, Buffer dst);

    static abstract void WriteVulkan(T data, Buffer dst);
}
