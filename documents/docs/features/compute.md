# Compute

Compute pipelines run general-purpose GPU computations using compute shaders. They operate outside of render passes and work on buffers and textures through resource bindings.

## Compute Pipeline

```csharp
ComputePipeline pipeline = context.CreateComputePipeline(new ComputePipelineDesc
{
    Compute = computeShader,
    ResourceBindings = resourceBindings,
    ThreadGroupSizeX = 16,
    ThreadGroupSizeY = 16,
    ThreadGroupSizeZ = 1
});
```

### ComputePipelineDesc

| Field | Type | Description |
|-------|------|-------------|
| `Compute` | `Shader` | The compute shader |
| `ResourceBindings` | `ResourceBinding[]` | Shader resource binding declarations |
| `ThreadGroupSizeX` | `uint` | Thread group size in X (must match `[numthreads]`) |
| `ThreadGroupSizeY` | `uint` | Thread group size in Y |
| `ThreadGroupSizeZ` | `uint` | Thread group size in Z |

The thread group size must match the `[numthreads]` attribute in the shader.

## Shader

Compute shaders use `[numthreads(X, Y, Z)]` to define thread group dimensions:

```hlsl
Texture2D inputTexture;
RWTexture2D outputTexture;

[numthreads(16, 16, 1)]
void CSMain(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint width, height;
    outputTexture.GetDimensions(width, height);

    if (dispatchThreadID.x >= width || dispatchThreadID.y >= height)
        return;

    float4 color = inputTexture[dispatchThreadID.xy];
    outputTexture[dispatchThreadID.xy] = color;
}
```

Compile with `ShaderStageFlags.Compute`:

```csharp
Shader computeShader = context.LoadShaderFromSource(source, "CSMain", ShaderStageFlags.Compute);
```

## Dispatching

Compute dispatches do not use render passes:

```csharp
commandBuffer.SetPipeline(pipeline);
commandBuffer.SetResourceTable(resourceTable);
commandBuffer.Dispatch(groupCountX, groupCountY, groupCountZ);
```

Calculate dispatch group counts:

```csharp
uint groupCountX = (width + threadGroupSize - 1) / threadGroupSize;
uint groupCountY = (height + threadGroupSize - 1) / threadGroupSize;
commandBuffer.Dispatch(groupCountX, groupCountY, 1);
```

### Indirect Dispatch

For GPU-driven dispatch counts, store `IndirectDispatchArgs` in a buffer:

```csharp
commandBuffer.DispatchIndirect(indirectBuffer, offsetInBytes: 0);
```

## Read-Write Resources

Compute shaders can both read and write textures and buffers:

| Shader Type | Resource Type | Description |
|------------|---------------|-------------|
| `Texture2D` | `ResourceType.Texture` | Read-only texture |
| `RWTexture2D` | `ResourceType.TextureReadWrite` | Read-write texture |
| `StructuredBuffer<T>` | `ResourceType.StructuredBuffer` | Read-only buffer |
| `RWStructuredBuffer<T>` | `ResourceType.StructuredBufferReadWrite` | Read-write buffer |

Read-write textures require `TextureUsageFlags.UnorderedAccess`. Read-write buffers require `BufferUsageFlags.UnorderedAccess`.

## Resource Sharing

Compute results can be consumed by graphics pipelines and vice versa. For example, run a compute pass to process an image, then copy or display the result:

```csharp
// Compute pass
commandBuffer.SetPipeline(computePipeline);
commandBuffer.SetResourceTable(computeTable);
commandBuffer.Dispatch(groupCountX, groupCountY, 1);

// Copy result to frame buffer
commandBuffer.CopyTexture(outputTexture, default, default,
                          colorTarget, default, default,
                          new() { Width = width, Height = height, Depth = 1 });
```
