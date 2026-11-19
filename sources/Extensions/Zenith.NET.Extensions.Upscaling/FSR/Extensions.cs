using System.Diagnostics;
using System.Runtime.CompilerServices;
using FSR.NET;

namespace Zenith.NET.Extensions.Upscaling.FSR;

internal static unsafe class Extensions
{
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

    extension(Texture texture)
    {
        public FSRApiResource FSR()
        {
            TextureDesc desc = texture.Desc;

            FSRApiResourceUsage usage = FSRFormats.FSR(desc.Usages);

            if (ZenithHelper.HasDepth(desc.Format))
            {
                usage |= FSRApiResourceUsage.DepthTarget;
            }

            if (ZenithHelper.HasStencil(desc.Format))
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
                    Type = (uint)FSRFormats.FSR(desc.Type),
                    Format = (uint)FSRFormats.FSR(desc.Format),
                    Width = desc.Width,
                    Height = desc.Height,
                    Depth = desc.Type is TextureType.Texture3D ? desc.Depth : desc.ArrayLayers,
                    MipCount = desc.MipLevels,
                    Usage = (uint)usage
                },
                State = (uint)FSRApiResourceState.Common
            };
        }
    }
}
