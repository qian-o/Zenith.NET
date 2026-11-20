using System.Diagnostics;
using System.Runtime.CompilerServices;
using FSR.NET;

namespace Zenith.NET.Extensions.Upscaling.FSR;

internal static unsafe class Extensions
{
    extension(CommandBuffer commandBuffer)
    {
        public void* FSR()
        {
            return (void*)(commandBuffer.Context.GraphicsApi switch
            {
                GraphicsApi.DirectX12 => commandBuffer.GetNativeObject(NativeObjectType.D3D12GraphicsCommandList),
                GraphicsApi.Vulkan => commandBuffer.GetNativeObject(NativeObjectType.VulkanCommandBuffer),
                _ => default
            });
        }
    }

    extension(FSRApiReturnCodes result)
    {
        public void Success()
        {
            if (result is not FSRApiReturnCodes.Ok)
            {
                Debug.WriteLine($"FSR call failed with error: {result}");
            }
        }
    }

    extension(GraphicsContext context)
    {
        public nint DirectX12()
        {
            return context.GetNativeObject(NativeObjectType.D3D12Device);
        }

        public FSRVkDeviceContext Vulkan()
        {
            return new()
            {
                VkDevice = context.GetNativeObject(NativeObjectType.VulkanDevice),
                VkPhysicalDevice = context.GetNativeObject(NativeObjectType.VulkanPhysicalDevice),
                VkDeviceProcAddr = (delegate* unmanaged[Stdcall]<nint, byte*, delegate* unmanaged[Stdcall]<void>>)context.GetNativeObject(NativeObjectType.VulkanGetDeviceProcAddr)
            };
        }
    }

    extension<TChain>(ref TChain chain) where TChain : unmanaged
    {
        public void AddNext<TNext>(out TNext next) where TNext : new()
        {
            next = new();

            FSRApiHeader* current = (FSRApiHeader*)Unsafe.AsPointer(ref chain);
            while (current->Next is not null)
            {
                current = current->Next;
            }

            current->Next = (FSRApiHeader*)Unsafe.AsPointer(ref next);
        }
    }

    extension(Texture texture)
    {
        public FSRApiResource FSR()
        {
            FSRApiResourceUsage usage = FSRFormats.FSR(texture.Desc.Usages);

            if (ZenithHelper.HasDepth(texture.Desc.Format))
            {
                usage |= FSRApiResourceUsage.DepthTarget;
            }

            if (ZenithHelper.HasStencil(texture.Desc.Format))
            {
                usage |= FSRApiResourceUsage.StencilTarget;
            }

            return new()
            {
                Resource = (void*)(texture.Context.GraphicsApi switch
                {
                    GraphicsApi.DirectX12 => texture.GetNativeObject(NativeObjectType.D3D12Resource),
                    GraphicsApi.Vulkan => texture.GetNativeObject(NativeObjectType.VulkanImage),
                    _ => default
                }),
                Description = new()
                {
                    Type = (uint)FSRFormats.FSR(texture.Desc.Type),
                    Format = (uint)FSRFormats.FSR(texture.Desc.Format).Format,
                    Width = texture.Desc.Width,
                    Height = texture.Desc.Height,
                    Depth = texture.Desc.Type is TextureType.Texture3D ? texture.Desc.Depth : texture.Desc.ArrayLayers,
                    MipCount = texture.Desc.MipLevels,
                    Usage = (uint)usage
                },
                State = (uint)FSRApiResourceState.Common
            };
        }
    }
}
