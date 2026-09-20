# Synchronization

When one operation uses data written by another, the later operation must wait for the writes it depends on and be able to read their results. This is a data dependency: the operation writing the data is the producer, and the operation using it is the consumer. The CPU also needs to know when it can read, overwrite or release that data. Zenith.NET exposes command barriers, texture transitions and queue timelines for these different dependencies.

## Start with the producer and the consumer

For each shared resource, identify the operation that accesses it first, the operation that follows, and whether either access writes. Reading the same data twice does not require ordering to protect its contents, although layout changes or presentation can impose other requirements. A write followed by a read, a read followed by an overwrite, or two writes can require a dependency.

Then choose the mechanism for where those operations execute:

| Situation | Mechanism |
| --- | --- |
| Dependent GPU stages in a command stream | `CommandBuffer.Barrier` with the producing and consuming stages. |
| A texture subresource changes how it is used | `CommandBuffer.Transition` with its previous and next layouts. |
| A submission on another queue produces the data | Pass its `TimelineValue` to the consuming command buffer's `Submit`. |
| The CPU must wait before accessing or releasing data | Call `Wait()` on the relevant completion value. |

These mechanisms are related, but not interchangeable. Submitting commands in order does not replace every memory dependency. A queue wait does not declare a new texture layout, and a command barrier does not wait on the calling CPU thread.

<a id="barriers"></a>
## Order dependent commands with a barrier

Consider two compute dispatches: the first writes simulation data, and the second reads that data to perform the next step. Both use storage buffers, so there is no texture layout to change. Insert this between the dispatches on their command buffer:

```csharp
commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
```

The first argument identifies the producing stage and the second the consuming stage. The barrier establishes the corresponding execution and memory dependency on the GPU. The backend translates those stages into its execution and memory-access rules. This barrier does not change a texture's layout or wait for all work on the device.

The [FluidTank simulation](https://github.com/qian-o/Zenith.NET/blob/master/sources/Experiments/FluidTank/Simulation.cs) uses this pattern between dependent dispatches. Place a barrier where the dependency occurs. A barrier after the consumer has already been recorded cannot order that consumer after an earlier producer.

`BarrierStages.All` is a broader scope, not an automatic dependency detector. Select stages that actually cover the work, and use them only on a queue that supports those operations.

<a id="layouts"></a>
## Track the layout of each texture subresource

Usage flags describe which roles a texture supports. [`TextureLayout`](xref:Zenith.NET.TextureLayout) describes how a particular subresource will be accessed for an operation. A subresource is a selected mip level and array layer. Changing its layout prepares that access; it does not convert the texture to a different pixel format. For example, the compute sample writes an output texture in `Storage` layout and then samples it in a graphics pass. Between those uses it records:

```csharp
commandBuffer.Transition(outputTexture, default, TextureLayout.Storage, TextureLayout.Sampled);
```

This fragment assumes that `outputTexture` was created with `Sampled | Storage` usage and that its selected subresource is in `Storage` layout. `default` selects mip level 0 and array layer 0. The operation does not transition every mip or layer of a texture.

The application supplies the previous layout. `Transition` does not maintain an application-visible layout tracker or infer that value from earlier commands. Track the layout wherever a resource passes between rendering stages.

`Undefined` means the previous contents need not be preserved. It is suitable for a new image or an attachment that will be fully cleared. It is not a way to avoid tracking the layout of an image whose contents will be loaded, sampled or accumulated into.

### A transition is not identical on every backend

DirectX 12 and Vulkan encode texture transitions with layout and access information. Metal has no operation in Zenith.NET's `Transition` implementation. Metal groups recorded work into render and compute encoders. The backend inserts visibility barriers when it ends these groups, making earlier writes available to later work. Explicit `Barrier` calls handle dependencies such as successive compute dispatches within the same encoder.

This distinction matters when changing a workload. In the [compute sample](../samples.md#compute-shader), the dispatch is followed by a graphics render pass, which also creates an encoder boundary on Metal. Two dependent compute dispatches do not create that boundary merely by changing the texture's declared layout. Keep the transitions required by the shared API, and separately identify the dependency between the actual producing and consuming commands.

<a id="cpu-and-gpu"></a>
## Use a timeline for completion and queue handoffs

A timeline tracks completion on a queue using increasing values. `Submit()` returns a [`TimelineValue`](xref:Zenith.NET.TimelineValue) containing that timeline and the value associated with the submission. Completion values from different timelines are not comparable just by their numeric `Value` fields.

Suppose `producer` and `consumer` are already-recorded command buffers from two queues of the same context. The producer writes data that the consumer uses. Submit them like this:

```csharp
TimelineValue produced = producer.Submit();

TimelineValue consumed = consumer.Submit(produced);
```

The supplied value becomes a GPU-side queue wait before the consuming work. The CPU does not call `produced.Wait()` in this sequence. Keep the shared resources valid until `consumed` has completed, and still record any texture layout changes required by the consumer.

Before the CPU reads, overwrites or releases those resources, wait for their final use:

```csharp
consumed.Wait();
```

`Wait()` waits for that timeline value and then checks its owning queue for completed submissions. Finished command buffers are reset for reuse, and their staged downloads are copied into the supplied CPU destinations. `queue.Timeline.Signal()` can also enqueue a completion point after work already submitted to that queue. Calling `Signal()` does not mean the GPU has already reached the point.

### GPU completion and downloaded CPU data are separate steps

`IsCompleted` only queries whether the GPU timeline has reached a value. It does not itself reclaim command buffers or copy staged downloads into the application's destination pointer.

`CommandBuffer.Download` first records a copy into internal readback storage. Resetting the completed command buffer for reuse performs the CPU copy to the supplied pointer. Keep that destination valid, and managed storage pinned, through the download submission's `Wait()` before reading it or releasing the memory. Waiting on another queue's later submission does not itself poll the download's owning queue.

This is why polling `IsCompleted` and immediately reading an asynchronous download destination is insufficient. [Resource Management](resource-management.md#memory-placement) covers the direct-copy and staged-transfer paths.

## Reuse data only after its final access

For a first renderer, an immediate wait after each frame is straightforward. To overlap work, give outstanding frames separate mutable data and retain the completion value of each frame's final use. Before updating a constant buffer, replacing a texture or disposing a pipeline, finish every outstanding access to the affected resource.

On resize or shutdown, stop new uses before waiting for old ones. `SwapChain.Resize`, resource disposal and context disposal do not serve as general waits for application work. The triangle can resize safely between callbacks because each preceding frame has already completed its submission.

The current `SwapChain.Present()` also signals and waits on the graphics queue after its presentation operation. Removing the triangle's explicit submission wait therefore does not by itself establish an overlapping presentation loop. A graphics-queue wait also says nothing about independent work still running on another queue.
