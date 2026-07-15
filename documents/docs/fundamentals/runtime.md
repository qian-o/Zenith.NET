# Runtime and Devices

`GraphicsContext` is the root object of Zenith.NET. It selects one graphics API and provides capabilities, queues, resource creation, and validation.

Create resources from the context that will use them. Resources cannot be shared between contexts.

## Graphics APIs

Each graphics API is provided by a separate package and extension namespace:

```csharp
using Zenith.NET;
using Zenith.NET.DirectX12;
using Zenith.NET.Metal;
using Zenith.NET.Vulkan;
```

| Package | Graphics API | Primary platform |
|---------|--------------|------------------|
| `Zenith.NET.DirectX12` | DirectX 12 | Windows |
| `Zenith.NET.Metal` | Metal 4 | Apple platforms |
| `Zenith.NET.Vulkan` | Vulkan 1.4 | Cross-platform |

Reference the graphics API packages your application can select.

Create the context appropriate for the current platform:

```csharp
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

`GraphicsApi` identifies the selected graphics API:

```csharp
Console.WriteLine(context.GraphicsApi);
```

Its values are `DirectX12`, `Metal`, and `Vulkan`.

## Command Queues

Every context exposes three queues:

| Property | Queue type | Intended work |
|----------|------------|---------------|
| `GraphicsQueue` | `CommandQueueType.Graphics` | Rendering, compute, copies, and presentation work |
| `ComputeQueue` | `CommandQueueType.Compute` | Compute and acceleration structure work |
| `TransferQueue` | `CommandQueueType.Transfer` | Uploads, downloads, and copies |

Request command buffers directly from these queues:

```csharp
CommandBuffer commandBuffer = context.GraphicsQueue.CommandBuffer();
```

See [Queues and Commands](commands.md) for recording and [Synchronization](synchronization.md) for dependencies.

## Capabilities

`Capabilities` reports the selected device and optional RHI features:

```csharp
Console.WriteLine(context.Capabilities.DeviceName);

if (context.Capabilities.RayTracingSupported)
{
    // Acceleration structures and inline RayQuery are available.
}

if (context.Capabilities.MeshShadingSupported)
{
    // Task and mesh shader pipelines are available.
}
```

| Property | Description |
|----------|-------------|
| `DeviceName` | Name reported by the selected GPU device |
| `RayTracingSupported` | Acceleration structure and inline `RayQuery` support |
| `MeshShadingSupported` | Task and mesh shader pipeline support |

Check an optional capability before creating its resources or pipelines.

## Diagnostics

Enable validation during development and subscribe to messages:

```csharp
GraphicsContext context = GraphicsContext.CreateVulkan(useValidationLayer: true);
context.ValidationMessage += static (_, args) => Console.WriteLine($"[{args.Severity}] {args.Message}");

Buffer vertexBuffer = context.CreateBuffer(BufferDesc.Vertex(sizeInBytes));
vertexBuffer.Name = "Scene vertices";
```

`MessageSeverity` is `Error`, `Warning`, or `Info`. Resource names appear in validation and graphics debugging tools.

## Ownership and Disposal

Contexts and graphics resources implement `IDisposable`. Complete submitted work before releasing its resources, and dispose dependent resources before their context:

```csharp
submission.Wait();

pipeline.Dispose();
constantBuffer.Dispose();
output.Dispose();
swapChain.Dispose();
context.Dispose();
```

