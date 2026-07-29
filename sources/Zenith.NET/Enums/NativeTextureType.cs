namespace Zenith.NET;

public enum NativeTextureType
{
    D3D11TextureNtHandle,

    D3D12ResourceNtHandle,

    MTLSharedTextureHandle,

    IOSurfaceRef,

    VulkanOpaqueNtHandle,

    VulkanOpaquePosixFileDescriptor,

    VulkanAndroidHardwareBuffer
}
