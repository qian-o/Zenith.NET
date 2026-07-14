# Compute

Compute pipelines run outside render passes and are typically recorded on `GraphicsContext.ComputeQueue`.

## Compile a Compute Shader

Compile Slang entry points through `ZenithCompiler` and create a `Shader`:

```csharp
using Shader computeShader = context.CreateShader(ZenithCompiler.CompileFromFile(context.GraphicsApi, "Assets/Shaders/PathTracing.slang", "CSMain"));
```

In Slang, the entry point uses compute stage terminology:

```slang
[shader("compute")]
[numthreads(16, 16, 1)]
void CSMain(uint3 dispatchThreadID : SV_DispatchThreadID)
{
}
```

## ComputePipelineDesc

`ComputePipelineDesc` currently has one field:

| Field | Type | Description |
|-------|------|-------------|
| `ComputeShader` | `Shader` | Compute shader entry point |

```csharp
ComputePipeline pipeline = context.CreateComputePipeline(new() { ComputeShader = computeShader });
```

`ThreadGroupSize` exists as a general struct (`X`, `Y`, `Z`) and is commonly mirrored in app code, but dispatch dimensions are provided to `Dispatch(...)`, not stored in `ComputePipelineDesc`.

## Bindless Storage Resources

Compute resource binding is bindless. Store `ResourceHandle` values in an explicitly laid-out C# constant struct, then resolve them in Slang with `DescriptorHandle<T>`.

```csharp
[StructLayout(LayoutKind.Explicit, Size = 32)]
file struct ComputeConstants
{
    [FieldOffset(0)]
    public uint Width;

    [FieldOffset(4)]
    public uint Height;

    [FieldOffset(8)]
    public ResourceHandle Input;

    [FieldOffset(16)]
    public ResourceHandle Output;
}
```

```slang
struct ComputeConstants
{
    uint Width;
    uint Height;
    DescriptorHandle<StructuredBuffer<float4>> Input;
    DescriptorHandle<RWTexture2D<float4>> Output;
};

uniform ComputeConstants constants;
```

For the full bindless model and handle mapping, see [Bindless Resources](../concepts/resource-binding.md).

## Dispatch and Group Counts

Use `Dispatch(groupCountX, groupCountY, groupCountZ)` after setting pipeline and constant buffer:

```csharp
commandBuffer.SetPipeline(pipeline);
commandBuffer.SetConstantBuffer(constantBuffer, 0);
commandBuffer.Dispatch(groupCountX, groupCountY, groupCountZ);
```

Group counts should cover your workload size based on `[numthreads(x, y, z)]`:

```csharp
const uint groupSizeX = 16;
const uint groupSizeY = 16;

uint groupCountX = (width + groupSizeX - 1) / groupSizeX;
uint groupCountY = (height + groupSizeY - 1) / groupSizeY;

commandBuffer.Dispatch(groupCountX, groupCountY, 1);
```

Then guard bounds in shader with `SV_DispatchThreadID`.

## Indirect Dispatch

For GPU-driven compute counts, write `IndirectDispatchArgs` into a buffer and call:

```csharp
commandBuffer.DispatchIndirect(indirectBuffer, 0);
```

`IndirectDispatchArgs` contains:

- `GroupCountX`
- `GroupCountY`
- `GroupCountZ`

## Transitions and Barriers

Use explicit transitions for texture layout changes and barriers for producer/consumer ordering when layout does not change.

```csharp
commandBuffer.Transition(outputTexture, default, TextureLayout.Storage);

commandBuffer.SetPipeline(producerPipeline);
commandBuffer.SetConstantBuffer(producerConstants, 0);
commandBuffer.Dispatch(producerX, producerY, 1);

commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

commandBuffer.SetPipeline(consumerPipeline);
commandBuffer.SetConstantBuffer(consumerConstants, 0);
commandBuffer.Dispatch(consumerX, consumerY, 1);

commandBuffer.Transition(outputTexture, default, TextureLayout.Sampled);
```

`Barrier` stage masks should match producer and consumer domains (for example, compute-to-vertex for indirect draw argument generation).

See [Synchronization and Barriers](../concepts/synchronization.md) for stage guidance and queue-to-queue timeline waits.
