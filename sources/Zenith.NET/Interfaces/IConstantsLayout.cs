namespace Zenith.NET;

public interface IConstantsLayout<T> where T : unmanaged, IConstantsLayout<T>
{
    static abstract uint SizeInBytesOnDirectX12 { get; }

    static abstract uint SizeInBytesOnMetal { get; }

    static abstract uint SizeInBytesOnVulkan { get; }

    static abstract void DirectX12(T data, Buffer dst);

    static abstract void Metal(T data, Buffer dst);

    static abstract void Vulkan(T data, Buffer dst);
}
