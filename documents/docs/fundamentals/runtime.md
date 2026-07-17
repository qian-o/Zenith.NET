# Runtime

`GraphicsContext` is the root of a Zenith.NET application. It identifies the selected graphics API, reports device capabilities, exposes command queues, and creates resources.

Create resources that work together from the same context.

## Create a Context

Add the core package and at least one graphics API package:

| Package | Platform support |
|---------|------------------|
| `Zenith.NET.DirectX12` | Windows |
| `Zenith.NET.Metal` | Apple platforms |
| `Zenith.NET.Vulkan` | Windows, Apple platforms, Android, and Linux |

Each package adds a context factory:

```csharp
using Zenith.NET;
using Zenith.NET.DirectX12;
using Zenith.NET.Metal;
using Zenith.NET.Vulkan;

GraphicsContext context;

if (OperatingSystem.IsWindows())
{
    context = GraphicsContext.CreateDirectX12(useValidationLayer: true);
}
else if (OperatingSystem.IsMacOS())
{
    context = GraphicsContext.CreateMetal(useValidationLayer: true);
}
else
{
    context = GraphicsContext.CreateVulkan(useValidationLayer: true);
}
```

Reference only the packages your application can select. The selected API is available through `context.GraphicsApi`.

## Check Capabilities

Inspect optional features before creating the resources that require them:

```csharp
Console.WriteLine(context.Capabilities.DeviceName);

if (context.Capabilities.RayTracingSupported)
{
    // Acceleration structures and inline RayQuery can be used.
}

if (context.Capabilities.MeshShadingSupported)
{
    // Mesh shading pipelines can be created.
}
```

Use capabilities to select an application feature or a compatible fallback.

## Choose a Queue

Every context exposes three command queues:

| Property | Intended work |
|----------|---------------|
| `GraphicsQueue` | Rendering, compute, copies, and presentation |
| `ComputeQueue` | Compute and acceleration-structure work |
| `TransferQueue` | Buffer transfers and copy commands |

Request command buffers from the queue that will execute the work:

```csharp
CommandBuffer commandBuffer = context.GraphicsQueue.CommandBuffer();
```

See [Commands](commands.md) for recording and submission.

## Enable Validation

Enable validation during development and subscribe before creating resources:

```csharp
using GraphicsContext context = GraphicsContext.CreateVulkan(useValidationLayer: true);

context.ValidationMessage += static (_, args) => Console.WriteLine($"[{args.Severity}] {args.Message}");
```

Set resource names when they help identify objects in diagnostics:

```csharp
using Zenith.NET.Buffer vertexBuffer = context.CreateBuffer(BufferDesc.Vertex(sizeInBytes));
vertexBuffer.Name = "Scene vertices";
```

Validation messages have `Error`, `Warning`, or `Info` severity.

## Dispose Objects

Zenith.NET resources implement `IDisposable`. Wait for submitted work before disposing objects used by that work, then dispose the context last:

```csharp
submission.Wait();

pipeline.Dispose();
constantBuffer.Dispose();
output.Dispose();
swapChain.Dispose();
context.Dispose();
```

Record and submit objects returned by `CommandQueue.CommandBuffer()`, but do not dispose or retain them for later recording.

