using Zenith.NET;

namespace SponzaScene.Renderer.Passes;

internal abstract class RenderPass : DisposableObject
{
    private readonly QueryHeap queryHeap;

    protected RenderPass(string name)
    {
        Name = name;

        queryHeap = App.Context.CreateQueryHeap(new()
        {
            Type = QueryType.Timestamp,
            Count = 2
        });
    }

    public string Name { get; }

    public double GpuTime
    {
        get
        {
            ulong[] timestamps = new ulong[2];
            queryHeap.GetResults(timestamps, 0);

            return (timestamps[1] - timestamps[0]) / 1000000.0;
        }
    }

    public void Execute(CommandBuffer commandBuffer, RenderContext context)
    {
        commandBuffer.WriteTimestamp(queryHeap, 0);

        commandBuffer.BeginDebugEvent(Name);

        ExecuteImpl(commandBuffer, context);

        commandBuffer.EndDebugEvent();

        commandBuffer.WriteTimestamp(queryHeap, 1);
    }

    public void DebugUI(RenderContext context)
    {
        DebugUIImpl(context);
    }

    public abstract void Resize(uint width, uint height);

    protected abstract void ExecuteImpl(CommandBuffer commandBuffer, RenderContext context);

    protected abstract void DebugUIImpl(RenderContext context);

    protected override void Destroy()
    {
        queryHeap.Dispose();
    }

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
