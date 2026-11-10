using System.Diagnostics;
using FSR.NET;

namespace Zenith.NET.Extensions.Upscaling.FSR;

internal static unsafe class Extensions
{
    extension(FSRApiReturnCodes result)
    {
        internal void Success()
        {
            if (!result.IsSuccess())
            {
                Debug.WriteLine($"FSR call failed with error: {result}");
            }
        }

        internal bool IsSuccess()
        {
            return result is FSRApiReturnCodes.Ok;
        }
    }

    extension(CommandBuffer commandBuffer)
    {
        internal void* FSR()
        {
            return (void*)commandBuffer.GetNativeObject(commandBuffer.Context.GraphicsApi is GraphicsApi.DirectX12 ? NativeObjectType.D3D12GraphicsCommandList : NativeObjectType.VulkanCommandBuffer);
        }
    }

    extension(Texture? texture)
    {
        internal FSRApiResource FSR(FSRApiResourceState state = FSRApiResourceState.Common)
        {
            if (texture is null)
            {
                return default;
            }

            ref readonly TextureDesc desc = ref texture.Desc;
            FSRApiResourceUsage usage = FSRFormats.FSR(desc.Usages);

            if (ZenithHelper.HasDepth(desc.Format))
            {
                usage |= FSRApiResourceUsage.DepthTarget;
            }

            if (ZenithHelper.HasStencil(desc.Format))
            {
                usage |= FSRApiResourceUsage.StencilTarget;
            }

            if (desc.Type is TextureType.Texture1DArray or TextureType.Texture2DArray or TextureType.TextureCubeArray)
            {
                usage |= FSRApiResourceUsage.ArrayView;
            }

            return new()
            {
                Resource = (void*)texture.GetNativeObject(texture.Context.GraphicsApi is GraphicsApi.DirectX12 ? NativeObjectType.D3D12Resource : NativeObjectType.VulkanImage),
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
                State = (uint)state
            };
        }
    }
}
