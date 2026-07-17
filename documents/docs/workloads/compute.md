# Compute

Compute pipelines process data outside a render pass. Record compute work on the compute or graphics queue according to the surrounding workload.

## Create a Compute Pipeline

Define a Slang compute entry point and its thread-group size:

```slang
[shader("compute")]
[numthreads(16, 16, 1)]
void CSMain(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    // Process one element or pixel.
}
```

Compile the entry point and create the pipeline:

```csharp
using Shader computeShader = context.CreateShader(
    ZenithCompiler.CompileFromFile(
        context.GraphicsApi,
        "Assets/Shaders/ImageProcessing.slang",
        "CSMain"));

using ComputePipeline pipeline = context.CreateComputePipeline(new() { ComputeShader = computeShader });
```

## Dispatch Work

Calculate group counts from the workload dimensions and the shader's `[numthreads]` values:

```csharp
const uint groupSizeX = 16;
const uint groupSizeY = 16;

uint groupCountX = (width + groupSizeX - 1) / groupSizeX;
uint groupCountY = (height + groupSizeY - 1) / groupSizeY;
```

Bind the pipeline and constants, then dispatch:

```csharp
CommandBuffer commandBuffer = context.ComputeQueue.CommandBuffer();

commandBuffer.SetPipeline(pipeline);
commandBuffer.SetConstantBuffer(constantBuffer, 0);
commandBuffer.Dispatch(groupCountX, groupCountY, 1);

commandBuffer.Submit();
```

Guard out-of-range threads in the shader when group counts round up the workload dimensions.

## Dispatch Indirectly

`DispatchIndirect` reads `GroupCountX`, `GroupCountY`, and `GroupCountZ` from an `IndirectDispatchArgs` record:

```csharp
commandBuffer.DispatchIndirect(indirectBuffer, offsetInBytes);
```

Create the argument buffer with `BufferUsages.Indirect`. If earlier GPU work writes the arguments, also add the appropriate storage usage and record a barrier before dispatch.

See [Bindless Resources](../fundamentals/bindless-resources.md) for passing buffers and textures to the shader. See [Synchronization](../fundamentals/synchronization.md) when a dispatch consumes or produces data for other GPU work.
