---
title: '@concepts.resources.title'
---

<h1 id="resource-management"><resource key="concepts.resources.title"></resource></h1>
<p><resource key="concepts.resources.description"></resource></p>
<h2 id="declare-the-required-uses-before-creating-a-resource"><resource key="concepts.resources.usages.title"></resource></h2>
<p><resource key="concepts.resources.usages.description"><slot name="bufferUsages"><a class="xref" href="~/api/Zenith.NET.BufferUsages.yml"><code>BufferUsages</code></a></slot><slot name="textureUsages"><a class="xref" href="~/api/Zenith.NET.TextureUsages.yml"><code>TextureUsages</code></a></slot><slot name="bufferDescUsages"><code>BufferDesc.Usages</code></slot><slot name="textureDescUsages"><code>TextureDesc.Usages</code></slot><slot name="value"><code>|</code></slot></resource></p>
<h3 id="buffer-usages"><resource key="concepts.resources.bufferUsages.title"></resource></h3>
<table>
<thead>
<tr>
<th><resource key="concepts.resources.bufferUsages.table.headings.usage"></resource></th>
<th><resource key="concepts.resources.bufferUsages.table.headings.role"></resource></th>
</tr>
</thead>
<tbody>
<tr>
<td><code>None</code></td>
<td><resource key="concepts.resources.bufferUsages.table.none.role"></resource></td>
</tr>
<tr>
<td><code>Vertex</code></td>
<td><resource key="concepts.resources.bufferUsages.table.vertex.role"></resource></td>
</tr>
<tr>
<td><code>Index</code></td>
<td><resource key="concepts.resources.bufferUsages.table.index.role"></resource></td>
</tr>
<tr>
<td><code>Indirect</code></td>
<td><resource key="concepts.resources.bufferUsages.table.indirect.role"></resource></td>
</tr>
<tr>
<td><code>Constant</code></td>
<td><resource key="concepts.resources.bufferUsages.table.constant.role"></resource></td>
</tr>
<tr>
<td><code>StorageReadOnly</code></td>
<td><resource key="concepts.resources.bufferUsages.table.storageReadOnly.role"></resource></td>
</tr>
<tr>
<td><code>StorageReadWrite</code></td>
<td><resource key="concepts.resources.bufferUsages.table.storageReadWrite.role"></resource></td>
</tr>
<tr>
<td><code>TransferSrc</code></td>
<td><resource key="concepts.resources.bufferUsages.table.transferSrc.role"></resource></td>
</tr>
<tr>
<td><code>TransferDst</code></td>
<td><resource key="concepts.resources.bufferUsages.table.transferDst.role"></resource></td>
</tr>
</tbody>
</table>
<h3 id="texture-usages"><resource key="concepts.resources.textureUsages.title"></resource></h3>
<table>
<thead>
<tr>
<th><resource key="concepts.resources.textureUsages.table.headings.usage"></resource></th>
<th><resource key="concepts.resources.textureUsages.table.headings.role"></resource></th>
</tr>
</thead>
<tbody>
<tr>
<td><code>None</code></td>
<td><resource key="concepts.resources.textureUsages.table.none.role"></resource></td>
</tr>
<tr>
<td><code>Sampled</code></td>
<td><resource key="concepts.resources.textureUsages.table.sampled.role"></resource></td>
</tr>
<tr>
<td><code>Storage</code></td>
<td><resource key="concepts.resources.textureUsages.table.storage.role"></resource></td>
</tr>
<tr>
<td><code>ColorAttachment</code></td>
<td><resource key="concepts.resources.textureUsages.table.colorAttachment.role"></resource></td>
</tr>
<tr>
<td><code>DepthStencilAttachment</code></td>
<td><resource key="concepts.resources.textureUsages.table.depthStencilAttachment.role"></resource></td>
</tr>
<tr>
<td><code>TransferSrc</code></td>
<td><resource key="concepts.resources.textureUsages.table.transferSrc.role"></resource></td>
</tr>
<tr>
<td><code>TransferDst</code></td>
<td><resource key="concepts.resources.textureUsages.table.transferDst.role"></resource></td>
</tr>
</tbody>
</table>
<p><resource key="concepts.resources.textureUsages.description"><slot name="transferSrc"><code>TransferSrc</code></slot><slot name="transferDst"><code>TransferDst</code></slot></resource></p>
<p><resource key="concepts.resources.textureUsages.details"><slot name="link"><a href="~/learn/concepts/synchronization.md#layouts"><resource key="concepts.resources.textureUsages.details.link"></resource></a></slot></resource></p>
<p><a id="memory-placement"></a></p>
<h2 id="choose-memory-for-the-cpu-access-you-need"><resource key="concepts.resources.memory.title"></resource></h2>
<p><resource key="concepts.resources.memory.description"><slot name="bufferDesc"><a class="xref" href="~/api/Zenith.NET.BufferDesc.yml"><code>BufferDesc</code></a></slot><slot name="residency"><code>Residency</code></slot></resource></p>
<table>
<thead>
<tr>
<th><resource key="concepts.resources.memory.table.headings.residency"></resource></th>
<th><resource key="concepts.resources.memory.table.headings.typicalRole"></resource></th>
<th><resource key="concepts.resources.memory.table.headings.cPUAccess"></resource></th>
</tr>
</thead>
<tbody>
<tr>
<td><code>GpuOnly</code></td>
<td><resource key="concepts.resources.memory.table.gpuOnly.typicalRole"></resource></td>
<td><resource key="concepts.resources.memory.table.gpuOnly.cPUAccess"></resource></td>
</tr>
<tr>
<td><code>CpuReadOnly</code></td>
<td><resource key="concepts.resources.memory.table.cpuReadOnly.typicalRole"></resource></td>
<td><resource key="concepts.resources.memory.table.cpuReadOnly.cPUAccess"></resource></td>
</tr>
<tr>
<td><code>CpuWriteOnly</code></td>
<td><resource key="concepts.resources.memory.table.cpuWriteOnly.typicalRole"></resource></td>
<td><resource key="concepts.resources.memory.table.cpuWriteOnly.cPUAccess"></resource></td>
</tr>
</tbody>
</table>
<p><resource key="concepts.resources.memory.details"><slot name="cpuWriteOnly"><code>CpuWriteOnly</code></slot></resource></p>
<p><resource key="concepts.resources.memory.guidance"><slot name="textureDesc"><code>TextureDesc</code></slot><slot name="map"><code>Map()</code></slot></resource></p>
<p><resource key="concepts.resources.memory.context"><slot name="map"><code>Map()</code></slot><slot name="unmap"><code>Unmap()</code></slot></resource></p>
<h2 id="choose-between-an-immediate-transfer-and-recorded-transfers"><resource key="concepts.resources.transfers.title"></resource></h2>
<p><resource key="concepts.resources.transfers.description"></resource></p>
<table>
<thead>
<tr>
<th><resource key="concepts.resources.transfers.table.headings.method"></resource></th>
<th><resource key="concepts.resources.transfers.table.headings.transfer"></resource></th>
</tr>
</thead>
<tbody>
<tr>
<td><code>Buffer.Upload</code></td>
<td><resource key="concepts.resources.transfers.table.bufferUpload.transfer"><slot name="cpuWriteOnly"><code>CpuWriteOnly</code></slot></resource></td>
</tr>
<tr>
<td><code>Buffer.Download</code></td>
<td><resource key="concepts.resources.transfers.table.bufferDownload.transfer"><slot name="cpuReadOnly"><code>CpuReadOnly</code></slot></resource></td>
</tr>
<tr>
<td><resource key="concepts.resources.transfers.table.textureTransfers.method"><slot name="textureUpload"><code>Texture.Upload</code></slot><slot name="textureDownload"><code>Texture.Download</code></slot></resource></td>
<td><resource key="concepts.resources.transfers.table.textureTransfers.transfer"></resource></td>
</tr>
</tbody>
</table>
<p><resource key="concepts.resources.transfers.details"></resource></p>
<p><resource key="concepts.resources.transfers.guidance"><slot name="commandBufferUpload"><code>CommandBuffer.Upload</code></slot><slot name="commandBufferDownload"><code>CommandBuffer.Download</code></slot></resource></p>
<ul>
<li><resource key="concepts.resources.transfers.item"></resource></li>
<li><resource key="concepts.resources.transfers.context"><slot name="wait"><code>Wait()</code></slot></resource></li>
</ul>
<p><resource key="concepts.resources.transfers.notes"><slot name="link"><a href="~/learn/concepts/synchronization.md#cpu-and-gpu"><resource key="concepts.resources.transfers.notes.link"></resource></a></slot></resource></p>
<h2 id="place-resources-in-a-heap-when-allocation-control-is-needed"><resource key="concepts.resources.allocation.title"></resource></h2>
<p><resource key="concepts.resources.allocation.description"><slot name="heap"><a class="xref" href="~/api/Zenith.NET.Heap.yml"><code>Heap</code></a></slot></resource></p>
<p><resource key="concepts.resources.allocation.details"><slot name="contextGetSizeAndAlignment"><code>context.GetSizeAndAlignment</code></slot></resource></p>
<p><resource key="concepts.resources.allocation.guidance"></resource></p>
<p><resource key="concepts.resources.allocation.context"></resource></p>
<p><a id="views-and-lifetime"></a></p>
<h2 id="keep-views-and-their-backing-resources-usable-together"><resource key="concepts.resources.views.title"></resource></h2>
<p><resource key="concepts.resources.views.description"><slot name="bufferView"><a class="xref" href="~/api/Zenith.NET.BufferView.yml"><code>BufferView</code></a></slot><slot name="textureView"><a class="xref" href="~/api/Zenith.NET.TextureView.yml"><code>TextureView</code></a></slot></resource></p>
<p><resource key="concepts.resources.views.details"><slot name="link"><a href="~/learn/concepts/shader-data-and-binding.md#resource-handles"><resource key="concepts.resources.views.details.link"></resource></a></slot></resource></p>
<p><resource key="concepts.resources.views.guidance"><slot name="link"><a href="~/learn/samples.md#spinning-cube"><resource key="concepts.resources.views.guidance.link"></resource></a></slot><slot name="detail"><a href="~/learn/samples.md#compute-shader"><resource key="concepts.resources.views.guidance.detail"></resource></a></slot></resource></p>
