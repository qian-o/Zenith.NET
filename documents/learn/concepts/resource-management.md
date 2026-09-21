---
title: '@concepts.resources.title'
---

<h1 id="resource-management"><resource key="concepts.resources.title"></resource></h1>
<p><resource key="concepts.resources.description"></resource></p>
<h2 id="declare-the-required-uses-before-creating-a-resource"><resource key="concepts.resources.usages.title"></resource></h2>
<p><resource key="concepts.resources.usages.description"><slot name="bufferUsages"><a class="xref" href="xref:Zenith.NET.BufferUsages"><code>BufferUsages</code></a></slot><slot name="textureUsages"><a class="xref" href="xref:Zenith.NET.TextureUsages"><code>TextureUsages</code></a></slot><slot name="bufferDescUsages"><a class="xref" href="xref:Zenith.NET.BufferDesc.Usages"><code>BufferDesc.Usages</code></a></slot><slot name="textureDescUsages"><a class="xref" href="xref:Zenith.NET.TextureDesc.Usages"><code>TextureDesc.Usages</code></a></slot><slot name="value"><code>|</code></slot></resource></p>
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
<td><a class="xref" href="xref:Zenith.NET.BufferUsages.None"><code>None</code></a></td>
<td><resource key="concepts.resources.bufferUsages.table.none.role"></resource></td>
</tr>
<tr>
<td><a class="xref" href="xref:Zenith.NET.BufferUsages.Vertex"><code>Vertex</code></a></td>
<td><resource key="concepts.resources.bufferUsages.table.vertex.role"></resource></td>
</tr>
<tr>
<td><a class="xref" href="xref:Zenith.NET.BufferUsages.Index"><code>Index</code></a></td>
<td><resource key="concepts.resources.bufferUsages.table.index.role"></resource></td>
</tr>
<tr>
<td><a class="xref" href="xref:Zenith.NET.BufferUsages.Indirect"><code>Indirect</code></a></td>
<td><resource key="concepts.resources.bufferUsages.table.indirect.role"></resource></td>
</tr>
<tr>
<td><a class="xref" href="xref:Zenith.NET.BufferUsages.Constant"><code>Constant</code></a></td>
<td><resource key="concepts.resources.bufferUsages.table.constant.role"></resource></td>
</tr>
<tr>
<td><a class="xref" href="xref:Zenith.NET.BufferUsages.StorageReadOnly"><code>StorageReadOnly</code></a></td>
<td><resource key="concepts.resources.bufferUsages.table.storageReadOnly.role"></resource></td>
</tr>
<tr>
<td><a class="xref" href="xref:Zenith.NET.BufferUsages.StorageReadWrite"><code>StorageReadWrite</code></a></td>
<td><resource key="concepts.resources.bufferUsages.table.storageReadWrite.role"></resource></td>
</tr>
<tr>
<td><a class="xref" href="xref:Zenith.NET.BufferUsages.TransferSrc"><code>TransferSrc</code></a></td>
<td><resource key="concepts.resources.bufferUsages.table.transferSrc.role"></resource></td>
</tr>
<tr>
<td><a class="xref" href="xref:Zenith.NET.BufferUsages.TransferDst"><code>TransferDst</code></a></td>
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
<td><a class="xref" href="xref:Zenith.NET.TextureUsages.None"><code>None</code></a></td>
<td><resource key="concepts.resources.textureUsages.table.none.role"></resource></td>
</tr>
<tr>
<td><a class="xref" href="xref:Zenith.NET.TextureUsages.Sampled"><code>Sampled</code></a></td>
<td><resource key="concepts.resources.textureUsages.table.sampled.role"></resource></td>
</tr>
<tr>
<td><a class="xref" href="xref:Zenith.NET.TextureUsages.Storage"><code>Storage</code></a></td>
<td><resource key="concepts.resources.textureUsages.table.storage.role"></resource></td>
</tr>
<tr>
<td><a class="xref" href="xref:Zenith.NET.TextureUsages.ColorAttachment"><code>ColorAttachment</code></a></td>
<td><resource key="concepts.resources.textureUsages.table.colorAttachment.role"></resource></td>
</tr>
<tr>
<td><a class="xref" href="xref:Zenith.NET.TextureUsages.DepthStencilAttachment"><code>DepthStencilAttachment</code></a></td>
<td><resource key="concepts.resources.textureUsages.table.depthStencilAttachment.role"></resource></td>
</tr>
<tr>
<td><a class="xref" href="xref:Zenith.NET.TextureUsages.TransferSrc"><code>TransferSrc</code></a></td>
<td><resource key="concepts.resources.textureUsages.table.transferSrc.role"></resource></td>
</tr>
<tr>
<td><a class="xref" href="xref:Zenith.NET.TextureUsages.TransferDst"><code>TransferDst</code></a></td>
<td><resource key="concepts.resources.textureUsages.table.transferDst.role"></resource></td>
</tr>
</tbody>
</table>
<p><resource key="concepts.resources.textureUsages.description"></resource></p>
<p><resource key="concepts.resources.textureUsages.details"><slot name="link"><a href="~/learn/concepts/synchronization.md#layouts"><resource key="concepts.resources.textureUsages.details.link"></resource></a></slot></resource></p>
<p><a id="memory-placement"></a></p>
<h2 id="choose-memory-for-the-cpu-access-you-need"><resource key="concepts.resources.memory.title"></resource></h2>
<p><resource key="concepts.resources.memory.description"><slot name="bufferDesc"><a class="xref" href="xref:Zenith.NET.BufferDesc"><code>BufferDesc</code></a></slot><slot name="residency"><a class="xref" href="xref:Zenith.NET.BufferDesc.Residency"><code>Residency</code></a></slot></resource></p>
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
<td><a class="xref" href="xref:Zenith.NET.MemoryResidency.GpuOnly"><code>GpuOnly</code></a></td>
<td><resource key="concepts.resources.memory.table.gpuOnly.typicalRole"></resource></td>
<td><resource key="concepts.resources.memory.table.gpuOnly.cPUAccess"></resource></td>
</tr>
<tr>
<td><a class="xref" href="xref:Zenith.NET.MemoryResidency.CpuReadOnly"><code>CpuReadOnly</code></a></td>
<td><resource key="concepts.resources.memory.table.cpuReadOnly.typicalRole"></resource></td>
<td><resource key="concepts.resources.memory.table.cpuReadOnly.cPUAccess"></resource></td>
</tr>
<tr>
<td><a class="xref" href="xref:Zenith.NET.MemoryResidency.CpuWriteOnly"><code>CpuWriteOnly</code></a></td>
<td><resource key="concepts.resources.memory.table.cpuWriteOnly.typicalRole"></resource></td>
<td><resource key="concepts.resources.memory.table.cpuWriteOnly.cPUAccess"></resource></td>
</tr>
</tbody>
</table>
<p><resource key="concepts.resources.memory.details"><slot name="cpuWriteOnly"><a class="xref" href="xref:Zenith.NET.MemoryResidency.CpuWriteOnly"><code>CpuWriteOnly</code></a></slot></resource></p>
<p><resource key="concepts.resources.memory.guidance"><slot name="textureDesc"><a class="xref" href="xref:Zenith.NET.TextureDesc"><code>TextureDesc</code></a></slot></resource></p>
<p><resource key="concepts.resources.memory.context"><slot name="map"><a class="xref" href="xref:Zenith.NET.Buffer.Map"><code>Map()</code></a></slot><slot name="unmap"><a class="xref" href="xref:Zenith.NET.Buffer.Unmap"><code>Unmap()</code></a></slot></resource></p>
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
<td><a class="xref" href="xref:Zenith.NET.Buffer.Upload(System.UInt32,Zenith.NET.BufferData)"><code>Buffer.Upload</code></a></td>
<td><resource key="concepts.resources.transfers.table.bufferUpload.transfer"><slot name="cpuWriteOnly"><a class="xref" href="xref:Zenith.NET.MemoryResidency.CpuWriteOnly"><code>CpuWriteOnly</code></a></slot></resource></td>
</tr>
<tr>
<td><a class="xref" href="xref:Zenith.NET.Buffer.Download(System.UInt32,Zenith.NET.BufferData)"><code>Buffer.Download</code></a></td>
<td><resource key="concepts.resources.transfers.table.bufferDownload.transfer"><slot name="cpuReadOnly"><a class="xref" href="xref:Zenith.NET.MemoryResidency.CpuReadOnly"><code>CpuReadOnly</code></a></slot></resource></td>
</tr>
<tr>
<td><resource key="concepts.resources.transfers.table.textureTransfers.method"><slot name="textureUpload"><a class="xref" href="xref:Zenith.NET.Texture.Upload(Zenith.NET.TextureSubresource,Zenith.NET.TextureLayout,Zenith.NET.TextureLayout,Zenith.NET.Offset3D,Zenith.NET.Extent3D,Zenith.NET.TextureData)"><code>Texture.Upload</code></a></slot><slot name="textureDownload"><a class="xref" href="xref:Zenith.NET.Texture.Download(Zenith.NET.TextureSubresource,Zenith.NET.TextureLayout,Zenith.NET.TextureLayout,Zenith.NET.Offset3D,Zenith.NET.Extent3D,Zenith.NET.TextureData)"><code>Texture.Download</code></a></slot></resource></td>
<td><resource key="concepts.resources.transfers.table.textureTransfers.transfer"></resource></td>
</tr>
</tbody>
</table>
<p><resource key="concepts.resources.transfers.details"></resource></p>
<p><resource key="concepts.resources.transfers.guidance"><slot name="commandBufferUpload"><a class="xref" href="xref:Zenith.NET.CommandBuffer.Upload*"><code>CommandBuffer.Upload</code></a></slot><slot name="commandBufferDownload"><a class="xref" href="xref:Zenith.NET.CommandBuffer.Download*"><code>CommandBuffer.Download</code></a></slot></resource></p>
<ul>
<li><resource key="concepts.resources.transfers.item"></resource></li>
<li><resource key="concepts.resources.transfers.context"><slot name="wait"><a class="xref" href="xref:Zenith.NET.TimelineValue.Wait"><code>Wait()</code></a></slot></resource></li>
</ul>
<p><resource key="concepts.resources.transfers.notes"><slot name="link"><a href="~/learn/concepts/synchronization.md#cpu-and-gpu"><resource key="concepts.resources.transfers.notes.link"></resource></a></slot></resource></p>
<h2 id="place-resources-in-a-heap-when-allocation-control-is-needed"><resource key="concepts.resources.allocation.title"></resource></h2>
<p><resource key="concepts.resources.allocation.description"><slot name="heap"><a class="xref" href="xref:Zenith.NET.Heap"><code>Heap</code></a></slot></resource></p>
<p><resource key="concepts.resources.allocation.details"><slot name="contextGetSizeAndAlignment"><code>context.<a class="code-reference" href="xref:Zenith.NET.GraphicsContext.GetSizeAndAlignment*">GetSizeAndAlignment</a></code></slot></resource></p>
<p><resource key="concepts.resources.allocation.guidance"></resource></p>
<p><resource key="concepts.resources.allocation.context"></resource></p>
<p><a id="views-and-lifetime"></a></p>
<h2 id="keep-views-and-their-backing-resources-usable-together"><resource key="concepts.resources.views.title"></resource></h2>
<p><resource key="concepts.resources.views.description"><slot name="bufferView"><a class="xref" href="xref:Zenith.NET.BufferView"><code>BufferView</code></a></slot><slot name="textureView"><a class="xref" href="xref:Zenith.NET.TextureView"><code>TextureView</code></a></slot></resource></p>
<p><resource key="concepts.resources.views.details"><slot name="link"><a href="~/learn/concepts/shader-data-and-binding.md#resource-handles"><resource key="concepts.resources.views.details.link"></resource></a></slot></resource></p>
