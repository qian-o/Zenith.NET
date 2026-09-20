# Execution Model

A Zenith.NET application creates GPU resources, such as buffers and textures, records commands that use them, and submits those commands to a queue. The CPU can continue while the GPU executes the submission. Understanding where that boundary lies tells you when data may be changed and when resources may be released.

## The context connects resources and queues

A [`GraphicsContext`](xref:Zenith.NET.GraphicsContext) represents one backend device and provides its graphics, compute and transfer queues. Create the buffers, textures, shaders and pipelines for a workload through the same context. A resource or handle created by another context cannot simply be bound to this one's command buffers.

Choose the backend for the platform and presentation surface. The API remains shared, but device support still matters. [`Capabilities`](xref:Zenith.NET.Capabilities) exposes the device name and the `RayTracingSupported` and `MeshShadingSupported` flags. Check the corresponding flag before creating an optional workload, as the [ray tracing](../samples.md#ray-tracing) and [mesh shading](../samples.md#mesh-shading) samples do. These flags are not a complete list of format, memory or platform restrictions.

<a id="queues"></a>
## Choose a queue for the work it will execute

Start with `GraphicsQueue` when a frame combines drawing, compute and copies. Keeping dependent work on one queue makes its ordering easier to follow.

| Queue | Work to record |
| --- | --- |
| `GraphicsQueue` | Render passes and drawing, plus compute and transfer operations used by the frame. |
| `ComputeQueue` | Compute dispatches and compatible transfer work, without graphics render passes. |
| `TransferQueue` | Upload, download and copy work, without drawing or compute dispatches. |

A queue accepts batches of recorded commands for execution. It does not necessarily correspond to a dedicated hardware engine. Separate queue properties do not guarantee that their work runs simultaneously. Use another queue when there is useful work to overlap, and make the handoff explicit when it shares resources with the first queue.

When later commands depend on earlier writes, the application must also ensure that those writes are available to the later commands. Submitting both command buffers to the same queue does not establish every such dependency automatically. Commands can use different GPU stages, and later accesses may need a barrier or texture transition. [Synchronization](synchronization.md) explains the distinction between submission order and resource visibility.

## Recording does not execute commands

`queue.CommandBuffer()` returns a [`CommandBuffer`](xref:Zenith.NET.CommandBuffer) with recording already begun. Record the commands and their dependencies, then call `Submit()` once. Submission ends recording and returns a [`TimelineValue`](xref:Zenith.NET.TimelineValue) identifying a completion point on that queue.

For example, after recording a frame into `commandBuffer`, the triangle submits it and waits for completion with:

```csharp
commandBuffer.Submit().Wait();
```

`Submit()` schedules the work; `Wait()` keeps the CPU from continuing until that submission has completed. Recording a draw, ending a render pass and submitting a command buffer are separate operations. Neither recording the draw nor ending the pass waits for the GPU.

The queue polls for completion and recycles finished command buffers. Call `queue.CommandBuffer()` for each new recording; it may return a recycled instance. Do not dispose a borrowed command buffer, submit it twice, or keep recording into it after submission. Record a given command buffer from one thread at a time; the queue's internal locking does not protect concurrent edits to a command buffer.

## A pipeline determines how commands interpret data

A graphics pipeline combines shaders, the vertex input layout, the rule for assembling vertices into primitives, attachment formats and render state. Set it before binding vertex, index or constant data and issuing draw commands. Its attachment formats and sample count must match the render pass's attachments.

A compute pipeline supplies the shader entry point for a dispatch, which launches groups of GPU threads. Set it and its constants before dispatching. Compute dispatches belong outside graphics render passes. Changing the pipeline is a change in how subsequent commands interpret their inputs; bind the data required by the selected pipeline before using it.

The [triangle's render callback](../first-triangle.md#frame-loop) shows the full graphics sequence. The [compute sample](../samples.md#compute-shader) records a dispatch followed by a graphics pass that displays the result.

<a id="ownership"></a>
## Keep ownership separate from GPU completion

| Object | Who releases it? |
| --- | --- |
| Context queues and their timelines | The context and queues manage them. |
| Borrowed command buffers | The queue manages and recycles them. |
| `SwapChain.Drawable` | The swap chain manages it. |
| Resources created by the application | The application disposes them after their last GPU use. |

Here, ownership means responsibility for disposal. A C# reference keeps a managed object reachable; it does not prevent an explicit `Dispose()` from destroying the native resource. Likewise, submitting a command that uses a buffer does not transfer ownership of that buffer to the queue.

On shutdown, stop submitting new work, wait for the last uses on every queue involved, release the application's resources, then dispose the context. Context disposal releases its own queues and internal helpers; it does not wait for all application work or dispose every resource the application created.

The same rule applies during normal rendering. A completion value can tell you when a frame's buffer is safe to overwrite or a resized texture is safe to replace. Resource lifetime is covered further in [Resource Management](resource-management.md#views-and-lifetime).
