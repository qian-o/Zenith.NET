---
title: '@concepts.synchronization.title'
---

<h1 id="synchronization"><resource key="concepts.synchronization.title"></resource></h1>
<p><resource key="concepts.synchronization.description"></resource></p>
<h2 id="start-with-the-producer-and-the-consumer"><resource key="concepts.synchronization.dependencies.title"></resource></h2>
<p><resource key="concepts.synchronization.dependencies.description"></resource></p>
<p><resource key="concepts.synchronization.dependencies.details"></resource></p>
<table>
<thead>
<tr>
<th><resource key="concepts.synchronization.dependencies.table.headings.situation"></resource></th>
<th><resource key="concepts.synchronization.dependencies.table.headings.mechanism"></resource></th>
</tr>
</thead>
<tbody>
<tr>
<td><resource key="concepts.synchronization.dependencies.table.pipelineStages.situation"></resource></td>
<td><resource key="concepts.synchronization.dependencies.table.pipelineStages.mechanism"><slot name="commandBufferBarrier"><code>CommandBuffer.Barrier</code></slot></resource></td>
</tr>
<tr>
<td><resource key="concepts.synchronization.dependencies.table.layoutChange.situation"></resource></td>
<td><resource key="concepts.synchronization.dependencies.table.layoutChange.mechanism"><slot name="commandBufferTransition"><code>CommandBuffer.Transition</code></slot></resource></td>
</tr>
<tr>
<td><resource key="concepts.synchronization.dependencies.table.crossQueue.situation"></resource></td>
<td><resource key="concepts.synchronization.dependencies.table.crossQueue.mechanism"><slot name="timelineValue"><code>TimelineValue</code></slot><slot name="submit"><code>Submit</code></slot></resource></td>
</tr>
<tr>
<td><resource key="concepts.synchronization.dependencies.table.cpuAccess.situation"></resource></td>
<td><resource key="concepts.synchronization.dependencies.table.cpuAccess.mechanism"><slot name="wait"><code>Wait()</code></slot></resource></td>
</tr>
</tbody>
</table>
<p><resource key="concepts.synchronization.dependencies.guidance"></resource></p>
<p><a id="barriers"></a></p>
<h2 id="order-dependent-commands-with-a-barrier"><resource key="concepts.synchronization.barriers.title"></resource></h2>
<p><resource key="concepts.synchronization.barriers.description"></resource></p>

```csharp
commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
```

<p><resource key="concepts.synchronization.barriers.details"></resource></p>
<p><resource key="concepts.synchronization.barriers.guidance"><slot name="link"><a href="https://github.com/qian-o/Zenith.NET/blob/master/sources/Experiments/FluidTank/Simulation.cs"><resource key="concepts.synchronization.barriers.guidance.link"></resource></a></slot></resource></p>
<p><resource key="concepts.synchronization.barriers.context"><slot name="barrierStagesAll"><code>BarrierStages.All</code></slot></resource></p>
<p><a id="layouts"></a></p>
<h2 id="track-the-layout-of-each-texture-subresource"><resource key="concepts.synchronization.layouts.title"></resource></h2>
<p><resource key="concepts.synchronization.layouts.description"><slot name="textureLayout"><a class="xref" href="~/api/Zenith.NET.TextureLayout.yml"><code>TextureLayout</code></a></slot><slot name="storage"><code>Storage</code></slot></resource></p>

```csharp
commandBuffer.Transition(outputTexture, default, TextureLayout.Storage, TextureLayout.Sampled);
```

<p><resource key="concepts.synchronization.layouts.details"><slot name="outputTexture"><code>outputTexture</code></slot><slot name="sampledStorage"><code>Sampled | Storage</code></slot><slot name="storage"><code>Storage</code></slot><slot name="default"><code>default</code></slot></resource></p>
<p><resource key="concepts.synchronization.layouts.guidance"><slot name="transition"><code>Transition</code></slot></resource></p>
<p><resource key="concepts.synchronization.layouts.context"><slot name="undefined"><code>Undefined</code></slot></resource></p>
<h3 id="a-transition-is-not-identical-on-every-backend"><resource key="concepts.synchronization.backends.title"></resource></h3>
<p><resource key="concepts.synchronization.backends.description"><slot name="transition"><code>Transition</code></slot><slot name="barrier"><code>Barrier</code></slot></resource></p>
<p><resource key="concepts.synchronization.backends.details"><slot name="link"><a href="~/learn/samples.md#compute-shader"><resource key="concepts.synchronization.backends.details.link"></resource></a></slot></resource></p>
<p><a id="cpu-and-gpu"></a></p>
<h2 id="use-a-timeline-for-completion-and-queue-handoffs"><resource key="concepts.synchronization.timelines.title"></resource></h2>
<p><resource key="concepts.synchronization.timelines.description"><slot name="submit"><code>Submit()</code></slot><slot name="timelineValue"><a class="xref" href="~/api/Zenith.NET.TimelineValue.yml"><code>TimelineValue</code></a></slot><slot name="value"><code>Value</code></slot></resource></p>
<p><resource key="concepts.synchronization.timelines.details"><slot name="producer"><code>producer</code></slot><slot name="consumer"><code>consumer</code></slot></resource></p>

```csharp
TimelineValue produced = producer.Submit();

TimelineValue consumed = consumer.Submit(produced);
```

<p><resource key="concepts.synchronization.timelines.guidance"><slot name="producedWait"><code>produced.Wait()</code></slot><slot name="consumed"><code>consumed</code></slot></resource></p>
<p><resource key="concepts.synchronization.timelines.context"></resource></p>

```csharp
consumed.Wait();
```

<p><resource key="concepts.synchronization.timelines.notes"><slot name="wait"><code>Wait()</code></slot><slot name="queueTimelineSignal"><code>queue.Timeline.Signal()</code></slot><slot name="signal"><code>Signal()</code></slot></resource></p>
<h3 id="gpu-completion-and-downloaded-cpu-data-are-separate-steps"><resource key="concepts.synchronization.readback.title"></resource></h3>
<p><resource key="concepts.synchronization.readback.description"><slot name="isCompleted"><code>IsCompleted</code></slot></resource></p>
<p><resource key="concepts.synchronization.readback.details"><slot name="commandBufferDownload"><code>CommandBuffer.Download</code></slot><slot name="wait"><code>Wait()</code></slot></resource></p>
<p><resource key="concepts.synchronization.readback.guidance"><slot name="isCompleted"><code>IsCompleted</code></slot><slot name="link"><a href="~/learn/concepts/resource-management.md#memory-placement"><resource key="concepts.synchronization.readback.guidance.link"></resource></a></slot></resource></p>
<h2 id="reuse-data-only-after-its-final-access"><resource key="concepts.synchronization.reuse.title"></resource></h2>
<p><resource key="concepts.synchronization.reuse.description"></resource></p>
<p><resource key="concepts.synchronization.reuse.details"><slot name="swapChainResize"><code>SwapChain.Resize</code></slot></resource></p>
<p><resource key="concepts.synchronization.reuse.guidance"><slot name="swapChainPresent"><code>SwapChain.Present()</code></slot></resource></p>
