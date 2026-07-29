# Shaders

Zenith.NET compiles Slang entry points for the active `GraphicsApi`, then creates `Shader` objects from the resulting descriptions. Pipeline creation combines those shader objects with workload-specific state.

## Compile from a File

Pass the active graphics API, the source file, and the entry-point name to `ZenithCompiler.CompileFromFile`:

```csharp
ShaderDesc vertexDesc = ZenithCompiler.CompileFromFile(
    context.GraphicsApi,
    "Shaders/Basic.slang",
    "VSMain");

Shader vertexShader = context.CreateShader(vertexDesc);
```

The `name` argument identifies the Slang entry point. Compile each entry point required by the pipeline, then assign the resulting shaders to its description.

## Compile from Source

Use `CompileFromSource` when the Slang source is already available as a string:

```csharp
const string source = """
    [shader("compute")]
    [numthreads(8, 8, 1)]
    void CSMain(uint3 dispatchThreadID : SV_DispatchThreadID)
    {
    }
    """;

ShaderDesc computeDesc = ZenithCompiler.CompileFromSource(
    context.GraphicsApi,
    source,
    "CSMain");

Shader computeShader = context.CreateShader(computeDesc);
```

Provide additional lookup directories through `searchPaths` when the shader imports other source files:

```csharp
ShaderDesc desc = ZenithCompiler.CompileFromFile(
    context.GraphicsApi,
    "Shaders/Lighting.slang",
    "PSMain",
    ["Shaders", "Shaders/Shared"]);
```

## Create a Pipeline

Assign compiled shaders to the pipeline description that matches the workload:

```csharp
ComputePipeline pipeline = context.CreateComputePipeline(new()
{
    ComputeShader = computeShader
});
```

Graphics pipelines use vertex and fragment shaders, while mesh shading pipelines use mesh and fragment shaders and may also use a task shader. For both pipeline types, the attachment formats and render state must match the render pass where the pipeline is used.

## Use Thread-Group Size

Compute shader reflection stores the entry point's thread-group dimensions in `ShaderDesc.ThreadGroupSize`. Use those values when converting a workload size into dispatch groups:

```csharp
ThreadGroupSize threadGroupSize = computeShader.Desc.ThreadGroupSize;

uint groupCountX = (width + threadGroupSize.X - 1) / threadGroupSize.X;
uint groupCountY = (height + threadGroupSize.Y - 1) / threadGroupSize.Y;

commandBuffer.SetPipeline(pipeline);
commandBuffer.Dispatch(groupCountX, groupCountY, 1);
```

Round each dimension up to a complete group and handle threads outside the workload in the shader.

## Manage Shader Lifetime

Shader objects are required while creating a pipeline and can be disposed after pipeline creation returns. Keep the pipeline alive until every submitted command that uses it has completed.

Use [Rasterization](../workloads/rasterization.md), [Compute](../workloads/compute.md), [Ray Tracing](../workloads/ray-tracing.md), and [Mesh Shading](../workloads/mesh-shading.md) for workload-specific pipeline descriptions and commands.