# Runtime

`GraphicsContext` is the root of a Zenith.NET application. It identifies the selected graphics API, reports device capabilities, exposes command queues, and creates resources.

Create resources that work together from the same context.

## Create a Context

Add the core package and at least one graphics API package:

| Package | Graphics API | Platform support |
|---------|--------------|------------------|
| `Zenith.NET.DirectX12` | DirectX 12 | Windows |
| `Zenith.NET.Metal` | Metal 4 | Apple platforms |
| `Zenith.NET.Vulkan` | Vulkan 1.4 | Windows, Apple platforms, Android, and Linux |

Each package adds a context factory. For example:

```csharp
GraphicsContext context = GraphicsContext.CreateVulkan(useValidationLayer: true);
```

Use `CreateDirectX12`, `CreateMetal`, or `CreateVulkan` for the selected backend. Reference only the packages your application can select. The selected API is available through `context.GraphicsApi`.

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

Borrow a command buffer from the queue that will execute the work, such as `context.GraphicsQueue.CommandBuffer()`. Record and submit it immediately; the queue owns and recycles it.

See [Commands](commands.md) for recording and submission.

## Enable Validation

Enable validation during development and subscribe before creating resources:

```csharp
GraphicsContext context = GraphicsContext.CreateVulkan(useValidationLayer: true);

context.ValidationMessage += static (_, args) => Console.WriteLine($"[{args.Severity}] {args.Message}");
```

Set resource names when they help identify objects in diagnostics:

```csharp
buffer.Name = "Buffer";
```

Validation messages have `Error`, `Warning`, or `Info` severity.

## Dispose Objects

Zenith.NET resources implement `IDisposable`. After rendering has stopped, dispose application-owned resources and dispose the context last. Command buffers are queue-owned borrows for one recording and submission; never dispose or retain them.
