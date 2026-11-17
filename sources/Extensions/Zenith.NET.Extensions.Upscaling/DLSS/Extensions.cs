using System.Diagnostics;
using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal static class Extensions
{
    extension(CommandBuffer commandBuffer)
    {
        public nint DirectX12()
        {
            return commandBuffer.GetNativeObject(NativeObjectType.D3D12GraphicsCommandList);
        }

        public nint Vulkan()
        {
            return commandBuffer.GetNativeObject(NativeObjectType.VulkanCommandBuffer);
        }
    }

    extension(GraphicsContext context)
    {
        public nint DirectX12()
        {
            return context.GetNativeObject(NativeObjectType.D3D12Device);
        }

        public (nint Instance, nint PhysicalDevice, nint Device, nint GetInstanceProcAddr, nint GetDeviceProcAddr) Vulkan()
        {
            return (context.GetNativeObject(NativeObjectType.VulkanInstance), context.GetNativeObject(NativeObjectType.VulkanPhysicalDevice), context.GetNativeObject(NativeObjectType.VulkanDevice), context.GetNativeObject(NativeObjectType.VulkanGetInstanceProcAddr), context.GetNativeObject(NativeObjectType.VulkanGetDeviceProcAddr));
        }
    }

    extension(int result)
    {
        public void Success()
        {
            if (result is not 0)
            {
                Debug.WriteLine($"Vulkan call failed with error code: {result}");
            }
        }
    }

    extension(NGXResult result)
    {
        public void Success()
        {
            if (Ngx.NVSDK_NGX_FAILED(result))
            {
                Debug.WriteLine($"NGX call failed with error: {result}");
            }
        }
    }

    extension(Texture texture)
    {
        public nint DirectX12()
        {
            return texture.GetNativeObject(NativeObjectType.D3D12Resource);
        }

        public nint Vulkan()
        {
            return texture.GetNativeObject(NativeObjectType.VulkanImage);
        }
    }
}
