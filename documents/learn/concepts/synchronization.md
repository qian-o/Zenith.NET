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
            <td><resource key="concepts.synchronization.dependencies.table.pipelineStages.mechanism"><slot name="commandBufferBarrier"><a class="xref" href="xref:Zenith.NET.CommandBuffer.Barrier(Zenith.NET.BarrierStages,Zenith.NET.BarrierStages)"><code>CommandBuffer.Barrier</code></a></slot></resource></td>
        </tr>
        <tr>
            <td><resource key="concepts.synchronization.dependencies.table.layoutChange.situation"></resource></td>
            <td><resource key="concepts.synchronization.dependencies.table.layoutChange.mechanism"><slot name="commandBufferTransition"><a class="xref" href="xref:Zenith.NET.CommandBuffer.Transition(Zenith.NET.Texture,Zenith.NET.TextureSubresource,Zenith.NET.TextureLayout,Zenith.NET.TextureLayout)"><code>CommandBuffer.Transition</code></a></slot></resource></td>
        </tr>
        <tr>
            <td><resource key="concepts.synchronization.dependencies.table.crossQueue.situation"></resource></td>
            <td><resource key="concepts.synchronization.dependencies.table.crossQueue.mechanism"><slot name="timelineValue"><a class="xref" href="xref:Zenith.NET.TimelineValue"><code>TimelineValue</code></a></slot><slot name="submit"><a class="xref" href="xref:Zenith.NET.CommandBuffer.Submit(System.ReadOnlySpan{Zenith.NET.TimelineValue})"><code>Submit</code></a></slot></resource></td>
        </tr>
        <tr>
            <td><resource key="concepts.synchronization.dependencies.table.cpuAccess.situation"></resource></td>
            <td><resource key="concepts.synchronization.dependencies.table.cpuAccess.mechanism"><slot name="wait"><a class="xref" href="xref:Zenith.NET.TimelineValue.Wait"><code>Wait()</code></a></slot></resource></td>
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
<p><resource key="concepts.synchronization.barriers.context"><slot name="barrierStagesAll"><a class="xref" href="xref:Zenith.NET.BarrierStages.All"><code>BarrierStages.All</code></a></slot></resource></p>
<p><a id="layouts"></a></p>
<h2 id="track-the-layout-of-each-texture-subresource"><resource key="concepts.synchronization.layouts.title"></resource></h2>
<p><resource key="concepts.synchronization.layouts.description"><slot name="textureLayout"><a class="xref" href="xref:Zenith.NET.TextureLayout"><code>TextureLayout</code></a></slot><slot name="storage"><a class="xref" href="xref:Zenith.NET.TextureLayout.Storage"><code>Storage</code></a></slot></resource></p>

```csharp
commandBuffer.Transition(outputTexture, default, TextureLayout.Storage, TextureLayout.Sampled);
```

<p><resource key="concepts.synchronization.layouts.details"><slot name="outputTexture"><code>outputTexture</code></slot><slot name="sampledStorage"><code><a class="code-reference" href="xref:Zenith.NET.TextureUsages.Sampled">Sampled</a> | <a class="code-reference" href="xref:Zenith.NET.TextureUsages.Storage">Storage</a></code></slot><slot name="storage"><a class="xref" href="xref:Zenith.NET.TextureLayout.Storage"><code>Storage</code></a></slot><slot name="default"><code>default</code></slot></resource></p>
<p><resource key="concepts.synchronization.layouts.guidance"><slot name="transition"><a class="xref" href="xref:Zenith.NET.CommandBuffer.Transition(Zenith.NET.Texture,Zenith.NET.TextureSubresource,Zenith.NET.TextureLayout,Zenith.NET.TextureLayout)"><code>Transition</code></a></slot></resource></p>
<p><resource key="concepts.synchronization.layouts.context"><slot name="undefined"><a class="xref" href="xref:Zenith.NET.TextureLayout.Undefined"><code>Undefined</code></a></slot></resource></p>
<h3 id="a-transition-is-not-identical-on-every-backend"><resource key="concepts.synchronization.backends.title"></resource></h3>
<p><resource key="concepts.synchronization.backends.description"><slot name="transition"><a class="xref" href="xref:Zenith.NET.CommandBuffer.Transition(Zenith.NET.Texture,Zenith.NET.TextureSubresource,Zenith.NET.TextureLayout,Zenith.NET.TextureLayout)"><code>Transition</code></a></slot><slot name="barrier"><a class="xref" href="xref:Zenith.NET.CommandBuffer.Barrier(Zenith.NET.BarrierStages,Zenith.NET.BarrierStages)"><code>Barrier</code></a></slot></resource></p>
<p><resource key="concepts.synchronization.backends.details"><slot name="link"><a href="~/learn/samples.md#compute-shader"><resource key="concepts.synchronization.backends.details.link"></resource></a></slot></resource></p>
<p><a id="cpu-and-gpu"></a></p>
<h2 id="use-a-timeline-for-completion-and-queue-handoffs"><resource key="concepts.synchronization.timelines.title"></resource></h2>
<p><resource key="concepts.synchronization.timelines.description"><slot name="submit"><a class="xref" href="xref:Zenith.NET.CommandBuffer.Submit(System.ReadOnlySpan{Zenith.NET.TimelineValue})"><code>Submit()</code></a></slot><slot name="timelineValue"><a class="xref" href="xref:Zenith.NET.TimelineValue"><code>TimelineValue</code></a></slot><slot name="value"><a class="xref" href="xref:Zenith.NET.TimelineValue.Value"><code>Value</code></a></slot></resource></p>
<p><resource key="concepts.synchronization.timelines.details"><slot name="producer"><code>producer</code></slot><slot name="consumer"><code>consumer</code></slot></resource></p>

```csharp
TimelineValue produced = producer.Submit();

TimelineValue consumed = consumer.Submit(produced);
```

<p><resource key="concepts.synchronization.timelines.guidance"><slot name="producedWait"><code>produced.<a class="code-reference" href="xref:Zenith.NET.TimelineValue.Wait">Wait</a>()</code></slot><slot name="consumed"><code>consumed</code></slot></resource></p>
<p><resource key="concepts.synchronization.timelines.context"></resource></p>

```csharp
consumed.Wait();
```

<p><resource key="concepts.synchronization.timelines.notes"><slot name="wait"><a class="xref" href="xref:Zenith.NET.TimelineValue.Wait"><code>Wait()</code></a></slot><slot name="queueTimelineSignal"><code>queue.<a class="code-reference" href="xref:Zenith.NET.CommandQueue.Timeline">Timeline</a>.<a class="code-reference" href="xref:Zenith.NET.Timeline.Signal">Signal</a>()</code></slot><slot name="signal"><a class="xref" href="xref:Zenith.NET.Timeline.Signal"><code>Signal()</code></a></slot></resource></p>
<h3 id="gpu-completion-and-downloaded-cpu-data-are-separate-steps"><resource key="concepts.synchronization.readback.title"></resource></h3>
<p><resource key="concepts.synchronization.readback.description"><slot name="isCompleted"><a class="xref" href="xref:Zenith.NET.TimelineValue.IsCompleted"><code>IsCompleted</code></a></slot></resource></p>
<p><resource key="concepts.synchronization.readback.details"><slot name="commandBufferDownload"><a class="xref" href="xref:Zenith.NET.CommandBuffer.Download*"><code>CommandBuffer.Download</code></a></slot><slot name="wait"><a class="xref" href="xref:Zenith.NET.TimelineValue.Wait"><code>Wait()</code></a></slot></resource></p>
<p><resource key="concepts.synchronization.readback.guidance"><slot name="isCompleted"><a class="xref" href="xref:Zenith.NET.TimelineValue.IsCompleted"><code>IsCompleted</code></a></slot><slot name="link"><a href="~/learn/concepts/resource-management.md#memory-placement"><resource key="concepts.synchronization.readback.guidance.link"></resource></a></slot></resource></p>
<h2 id="reuse-data-only-after-its-final-access"><resource key="concepts.synchronization.reuse.title"></resource></h2>
<p><resource key="concepts.synchronization.reuse.description"></resource></p>
<p><resource key="concepts.synchronization.reuse.details"><slot name="swapChainResize"><a class="xref" href="xref:Zenith.NET.SwapChain.Resize(System.UInt32,System.UInt32)"><code>SwapChain.Resize</code></a></slot></resource></p>
<p><resource key="concepts.synchronization.reuse.guidance"><slot name="swapChainPresent"><a class="xref" href="xref:Zenith.NET.SwapChain.Present"><code>SwapChain.Present()</code></a></slot></resource></p>
