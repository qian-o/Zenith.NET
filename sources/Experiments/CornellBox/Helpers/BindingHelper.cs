using Zenith.NET;

namespace CornellBox.Helpers;

internal static class BindingHelper
{
    public static ResourceBinding[] Bindings(params ResourceBinding[] bindings)
    {
        switch (App.Context.Backend)
        {
            case Backend.DirectX12:
                {
                    uint cbvIndex = 0;
                    uint srvIndex = 0;
                    uint uavIndex = 0;
                    uint samplerIndex = 0;

                    for (int i = 0; i < bindings.Length; i++)
                    {
                        ref ResourceBinding binding = ref bindings[i];

                        binding = binding with
                        {
                            Index = binding.Type switch
                            {
                                ResourceType.ConstantBuffer => cbvIndex++,

                                ResourceType.StructuredBuffer or
                                ResourceType.Texture or
                                ResourceType.AccelerationStructure => srvIndex++,

                                ResourceType.StructuredBufferReadWrite or
                                ResourceType.TextureReadWrite => uavIndex++,

                                ResourceType.Sampler => samplerIndex++,

                                _ => binding.Index
                            }
                        };
                    }
                }
                break;

            case Backend.Vulkan:
                {
                    for (int i = 0; i < bindings.Length; i++)
                    {
                        ref ResourceBinding binding = ref bindings[i];

                        binding = binding with { Index = (uint)i };
                    }
                }
                break;

            case Backend.Metal:
                {
                    uint bufferIndex = 0;
                    uint textureIndex = 0;
                    uint samplerIndex = 0;

                    for (int i = 0; i < bindings.Length; i++)
                    {
                        ref ResourceBinding binding = ref bindings[i];

                        binding = binding with
                        {
                            Index = binding.Type switch
                            {
                                ResourceType.ConstantBuffer or
                                ResourceType.StructuredBuffer or
                                ResourceType.StructuredBufferReadWrite => bufferIndex++,

                                ResourceType.Texture or
                                ResourceType.TextureReadWrite => textureIndex++,

                                ResourceType.Sampler => samplerIndex++,

                                _ => binding.Index
                            }
                        };
                    }
                }
                break;
        }

        return bindings;
    }
}
