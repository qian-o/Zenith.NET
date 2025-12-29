using Zenith.NET;

namespace SponzaScene.Renderer;

internal abstract class RenderPass(string name) : DisposableObject
{
    public string Name { get; } = name;

    public bool Enabled { get; set; } = true;

    public abstract void Execute(CommandBuffer commandBuffer, RenderContext context);

    public abstract void DebugUI(RenderContext context);

    public abstract void Resize(uint width, uint height);

    protected static string GetShaderPath(string shaderName)
    {
        return Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", $"{shaderName}.slang");
    }

    protected static ResourceBinding[] Bindings(params ResourceBinding[] bindings)
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

                        switch (binding.Type)
                        {
                            case ResourceType.ConstantBuffer:
                                binding = binding with { Index = cbvIndex++ };
                                break;

                            case ResourceType.StructuredBuffer:
                            case ResourceType.Texture:
                            case ResourceType.AccelerationStructure:
                                binding = binding with { Index = srvIndex++ };
                                break;

                            case ResourceType.StructuredBufferReadWrite:
                            case ResourceType.TextureReadWrite:
                                binding = binding with { Index = uavIndex++ };
                                break;

                            case ResourceType.Sampler:
                                binding = binding with { Index = samplerIndex++ };
                                break;
                        }
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
        }

        return bindings;
    }
}
