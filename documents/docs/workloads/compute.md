# Compute

Compute pipelines run outside render passes and are typically recorded on `GraphicsContext.ComputeQueue`.

## Pipeline

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

```csharp
ComputePipeline pipeline = context.CreateComputePipeline(new() { ComputeShader = computeShader });
```

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

For shader-visible resources, see [Bindless Resources](../fundamentals/bindless-resources.md). Use [Synchronization](../fundamentals/synchronization.md) when dispatches produce data consumed by later GPU work.
