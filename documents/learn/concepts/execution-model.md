---
title: '@concepts.execution.title'
---

<h1 id="execution-model">
    <resource key="concepts.execution.title"></resource>
</h1>
<p>
    <resource key="concepts.execution.description"></resource>
</p>
<h2 id="the-context-connects-resources-and-queues">
    <resource key="concepts.execution.context.title"></resource>
</h2>
<p>
    <resource key="concepts.execution.context.description">
        <slot name="graphicsContext"><a class="xref" href="xref:Zenith.NET.GraphicsContext"><code>GraphicsContext</code></a></slot>
    </resource>
</p>
<p>
    <resource key="concepts.execution.context.details">
        <slot name="capabilities"><a class="xref" href="xref:Zenith.NET.Capabilities"><code>Capabilities</code></a></slot>
        <slot name="rayTracingSupported"><a class="xref" href="xref:Zenith.NET.Capabilities.RayTracingSupported"><code>RayTracingSupported</code></a></slot>
        <slot name="meshShadingSupported"><a class="xref" href="xref:Zenith.NET.Capabilities.MeshShadingSupported"><code>MeshShadingSupported</code></a></slot>
        <slot name="link"><a href="~/learn/samples.md#ray-tracing"><resource key="concepts.execution.context.details.link"></resource></a></slot>
        <slot name="detail"><a href="~/learn/samples.md#mesh-shading"><resource key="concepts.execution.context.details.detail"></resource></a></slot>
    </resource>
</p>
<p>
    <a id="queues"></a>
</p>
<h2 id="choose-a-queue-for-the-work-it-will-execute">
    <resource key="concepts.execution.queues.title"></resource>
</h2>
<p>
    <resource key="concepts.execution.queues.description">
        <slot name="graphicsQueue"><a class="xref" href="xref:Zenith.NET.GraphicsContext.GraphicsQueue"><code>GraphicsQueue</code></a></slot>
    </resource>
</p>
<table>
    <thead>
        <tr>
            <th>
                <resource key="concepts.execution.queues.table.headings.queue"></resource>
            </th>
            <th>
                <resource key="concepts.execution.queues.table.headings.workToRecord"></resource>
            </th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td>
                <a class="xref" href="xref:Zenith.NET.GraphicsContext.GraphicsQueue">
                    <code>GraphicsQueue</code>
                </a>
            </td>
            <td>
                <resource key="concepts.execution.queues.table.graphicsQueue.workToRecord"></resource>
            </td>
        </tr>
        <tr>
            <td>
                <a class="xref" href="xref:Zenith.NET.GraphicsContext.ComputeQueue">
                    <code>ComputeQueue</code>
                </a>
            </td>
            <td>
                <resource key="concepts.execution.queues.table.computeQueue.workToRecord"></resource>
            </td>
        </tr>
        <tr>
            <td>
                <a class="xref" href="xref:Zenith.NET.GraphicsContext.TransferQueue">
                    <code>TransferQueue</code>
                </a>
            </td>
            <td>
                <resource key="concepts.execution.queues.table.transferQueue.workToRecord"></resource>
            </td>
        </tr>
    </tbody>
</table>
<p>
    <resource key="concepts.execution.queues.details"></resource>
</p>
<p>
    <resource key="concepts.execution.queues.guidance">
        <slot name="link"><a href="~/learn/concepts/synchronization.md"><resource key="concepts.execution.queues.guidance.link"></resource></a></slot>
    </resource>
</p>
<h2 id="recording-does-not-execute-commands">
    <resource key="concepts.execution.recording.title"></resource>
</h2>
<p>
    <resource key="concepts.execution.recording.description">
        <slot name="queueCommandBuffer"><code>queue.<a class="code-reference" href="xref:Zenith.NET.CommandQueue.CommandBuffer">CommandBuffer</a>()</code></slot>
        <slot name="commandBuffer"><a class="xref" href="xref:Zenith.NET.CommandBuffer"><code>CommandBuffer</code></a></slot>
        <slot name="submit"><a class="xref" href="xref:Zenith.NET.CommandBuffer.Submit(System.ReadOnlySpan{Zenith.NET.TimelineValue})"><code>Submit()</code></a></slot>
        <slot name="timelineValue"><a class="xref" href="xref:Zenith.NET.TimelineValue"><code>TimelineValue</code></a></slot>
    </resource>
</p>
<p>
    <resource key="concepts.execution.recording.details">
        <slot name="commandBuffer"><code>commandBuffer</code></slot>
    </resource>
</p>

```csharp
commandBuffer.Submit().Wait();
```

<p>
    <resource key="concepts.execution.recording.guidance">
        <slot name="submit"><a class="xref" href="xref:Zenith.NET.CommandBuffer.Submit(System.ReadOnlySpan{Zenith.NET.TimelineValue})"><code>Submit()</code></a></slot>
        <slot name="wait"><a class="xref" href="xref:Zenith.NET.TimelineValue.Wait"><code>Wait()</code></a></slot>
    </resource>
</p>
<p>
    <resource key="concepts.execution.recording.context">
        <slot name="queueCommandBuffer"><code>queue.<a class="code-reference" href="xref:Zenith.NET.CommandQueue.CommandBuffer">CommandBuffer</a>()</code></slot>
    </resource>
</p>
<h2 id="a-pipeline-determines-how-commands-interpret-data">
    <resource key="concepts.execution.pipelines.title"></resource>
</h2>
<p>
    <resource key="concepts.execution.pipelines.description"></resource>
</p>
<p>
    <resource key="concepts.execution.pipelines.details"></resource>
</p>
<p>
    <resource key="concepts.execution.pipelines.guidance">
        <slot name="link"><a href="~/learn/first-triangle.md#frame-loop"><resource key="concepts.execution.pipelines.guidance.link"></resource></a></slot>
        <slot name="detail"><a href="~/learn/samples.md#compute-shader"><resource key="concepts.execution.pipelines.guidance.detail"></resource></a></slot>
    </resource>
</p>
<p>
    <a id="ownership"></a>
</p>
<h2 id="keep-ownership-separate-from-gpu-completion">
    <resource key="concepts.execution.ownership.title"></resource>
</h2>
<table>
    <thead>
        <tr>
            <th>
                <resource key="concepts.execution.ownership.table.headings.object"></resource>
            </th>
            <th>
                <resource key="concepts.execution.ownership.table.headings.ownership"></resource>
            </th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td>
                <resource key="concepts.execution.ownership.table.queues.object"></resource>
            </td>
            <td>
                <resource key="concepts.execution.ownership.table.queues.ownership"></resource>
            </td>
        </tr>
        <tr>
            <td>
                <resource key="concepts.execution.ownership.table.commandBuffers.object"></resource>
            </td>
            <td>
                <resource key="concepts.execution.ownership.table.commandBuffers.ownership"></resource>
            </td>
        </tr>
        <tr>
            <td>
                <a class="xref" href="xref:Zenith.NET.SwapChain.Drawable">
                    <code>SwapChain.Drawable</code>
                </a>
            </td>
            <td>
                <resource key="concepts.execution.ownership.table.swapChainDrawable.ownership"></resource>
            </td>
        </tr>
        <tr>
            <td>
                <resource key="concepts.execution.ownership.table.applicationResources.object"></resource>
            </td>
            <td>
                <resource key="concepts.execution.ownership.table.applicationResources.ownership"></resource>
            </td>
        </tr>
    </tbody>
</table>
<p>
    <resource key="concepts.execution.ownership.description">
        <slot name="dispose"><a class="xref" href="xref:Zenith.NET.DisposableObject.Dispose"><code>Dispose()</code></a></slot>
    </resource>
</p>
<p>
    <resource key="concepts.execution.ownership.details"></resource>
</p>
<p>
    <resource key="concepts.execution.ownership.guidance">
        <slot name="link"><a href="~/learn/concepts/resource-management.md#views-and-lifetime"><resource key="concepts.execution.ownership.guidance.link"></resource></a></slot>
    </resource>
</p>
