# Graphics Context

`GraphicsContext` is the root object of Zenith.NET. It selects one graphics API, exposes the GPU device capabilities and command queues, creates resources, and owns validation services.

Create resources from the context that will use them. Resources cannot be shared between contexts.

## Graphics API Creation

Each graphics API is provided by a separate package and extension namespace:

```csharp
using Zenith.NET;
using Zenith.NET.DirectX12;
using Zenith.NET.Metal;
using Zenith.NET.Vulkan;
```

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

`GraphicsApi` identifies the selected implementation:

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

See [Commands](command-model.md) for recording and [Synchronization and Barriers](synchronization.md) for timeline and memory dependencies.

## Capabilities

`Capabilities` deliberately exposes the optional feature decisions an application needs:

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

Check an optional capability before creating its resources or pipelines. Zenith.NET does not require applications to branch on lower-level graphics API feature tiers.

## Resource Creation

The context creates the following public resource categories:

| Factory | Result |
|---------|--------|
| `CreateSwapChain` | Presentation swap chain for a native `Surface` |
| `CreateHeap` | Explicit memory heap for placed buffers and textures |
| `CreateBuffer` / `CreateBufferView` | Buffers and typed subranges |
| `CreateTexture` / `CreateTextureView` | Textures and typed subresource ranges |
| `CreateSampler` | Texture sampling state |
| `CreateShader` | Graphics API shader object from a `ShaderDesc` |
| `CreateGraphicsPipeline` | Vertex and fragment rasterization pipeline |
| `CreateComputePipeline` | Compute pipeline |
| `CreateMeshShadingPipeline` | Optional task, mesh, and fragment pipeline |
| `CreateQueryHeap` | Occlusion or timestamp query storage |

Acceleration structures are created by command buffers because building them records GPU work.

## Memory Requirements

Query the required size and alignment before placing a resource in a `Heap`:

```csharp
BufferDesc desc = BufferDesc.StorageReadOnly(sizeInBytes, strideInBytes);
SizeAndAlignment requirements = context.GetSizeAndAlignment(desc);

Heap heap = context.CreateHeap(HeapDesc.GpuOnly(requirements.SizeInBytes));
Buffer buffer = heap.CreateBuffer(0, desc);
```

Use the returned `AlignmentInBytes` when packing multiple resources into the same heap.

## Validation

Enable the validation layer during development and subscribe before creating application resources:

```csharp
GraphicsContext context = GraphicsContext.CreateVulkan(useValidationLayer: true);
context.ValidationMessage += static (sender, args) => Console.WriteLine($"[{args.Severity}] {args.Message}");
```

`MessageSeverity` is `Error`, `Warning`, or `Info`. Validation availability and exact messages depend on the selected graphics API and installed development components.

## Resource Names

Every `GraphicsResource` has a `Name`. Assign concise names to improve validation and graphics debugger output:

```csharp
Buffer vertexBuffer = context.CreateBuffer(BufferDesc.Vertex(sizeInBytes));
vertexBuffer.Name = "Scene vertices";
```

Naming does not affect resource identity or shader handles.

## Ownership and Disposal

All contexts and graphics resources implement `IDisposable`. Dispose resources only after their final submission completes, and dispose child resources before their context:

```csharp
submission.Wait();

pipeline.Dispose();
constantBuffer.Dispose();
output.Dispose();
swapChain.Dispose();
context.Dispose();
```

Do not dispose swap-chain drawables. They are owned by the swap chain. Do not retain a drawable as a permanent texture because the current image can change after presentation.

`Dispose()` is idempotent, and `IsDisposed` reports whether disposal has already occurred. Deterministic disposal is still required; finalization is a fallback, not a synchronization mechanism.

## Context Lifetime

A typical application lifetime is:

1. Select and create one `GraphicsContext`.
2. Subscribe to validation messages.
3. Create a surface, swap chain, and persistent resources.
4. Record and submit work through the context queues.
5. Wait for the last submission that uses each resource.
6. Dispose resources, then dispose the context.

Use a separate context only when the application intentionally manages another independent graphics device and resource universe.
