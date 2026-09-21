---
title: '@concepts.shaders.title'
---

<h1 id="shader-data-and-binding">
    <resource key="concepts.shaders.title"></resource>
</h1>
<p>
    <resource key="concepts.shaders.description"></resource>
</p>
<h2 id="compile-an-entry-point-for-the-selected-context">
    <resource key="concepts.shaders.compilation.title"></resource>
</h2>
<p>
    <resource key="concepts.shaders.compilation.description">
        <slot name="zenithCompiler"><a class="xref" href="xref:Zenith.NET.ZenithCompiler"><code>ZenithCompiler</code></a></slot>
        <slot name="shaderDesc"><a class="xref" href="xref:Zenith.NET.ShaderDesc"><code>ShaderDesc</code></a></slot>
        <slot name="graphicsApi"><a class="xref" href="xref:Zenith.NET.GraphicsContext.GraphicsApi"><code>GraphicsApi</code></a></slot>
    </resource>
</p>
<p>
    <resource key="concepts.shaders.compilation.details"><slot name="context"><code>context</code></slot>
    </resource>
</p>

```csharp
string shaderPath = Path.Combine(AppContext.BaseDirectory, "Triangle.slang");

Shader vertexShader = context.CreateShader(ZenithCompiler.CompileFromFile(context.GraphicsApi, shaderPath, "VSMain"));
```

<p>
    <resource key="concepts.shaders.compilation.guidance"><slot name="vSMain"><code>VSMain</code></slot>
        <slot name="link"><a href="~/learn/first-triangle.md#pipeline"><resource key="concepts.shaders.compilation.guidance.link"></resource></a></slot>
    </resource>
</p>
<p>
    <a id="data-layout"></a>
</p>
<h2 id="vertex-input-and-shader-data-have-different-layout-rules">
    <resource key="concepts.shaders.layout.title"></resource>
</h2>
<p>
    <resource key="concepts.shaders.layout.description">
        <slot name="inputLayout"><a class="xref" href="xref:Zenith.NET.InputLayout"><code>InputLayout</code></a></slot>
        <slot name="float3"><a class="xref" href="xref:Zenith.NET.ElementFormat.Float3"><code>Float3</code></a></slot>
        <slot name="float4"><a class="xref" href="xref:Zenith.NET.ElementFormat.Float4"><code>Float4</code></a></slot>
        <slot name="inputLayoutAdd"><a class="xref" href="xref:Zenith.NET.InputLayout.Add(Zenith.NET.InputElement)"><code>InputLayout.Add</code></a></slot>
    </resource>
</p>
<p>
    <resource key="concepts.shaders.layout.details">
        <slot name="inputLayout"><a class="xref" href="xref:Zenith.NET.InputLayout"><code>InputLayout</code></a></slot>
        <slot name="float3"><code>float3</code></slot>
    </resource>
</p>
<p>
    <resource key="concepts.shaders.layout.guidance"></resource>
</p>
<p>
    <resource key="concepts.shaders.layout.context">
        <slot name="link"><a href="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/ComputeShaderRenderer.cs"><resource key="concepts.shaders.layout.context.link"></resource></a></slot>
        <slot name="systemRuntimeInteropServices"><code>System.Runtime.InteropServices</code></slot>
        <slot name="zenithNET"><a class="xref" href="xref:Zenith.NET"><code>Zenith.NET</code></a></slot>
    </resource>
</p>

```csharp
[StructLayout(LayoutKind.Explicit, Size = 256)]
file struct Constants
{
    [FieldOffset(0)]
    public uint Width;

    [FieldOffset(4)]
    public uint Height;

    [FieldOffset(8)]
    public ResourceHandle Input;

    [FieldOffset(16)]
    public ResourceHandle Output;
}
```

<p>
    <resource key="concepts.shaders.layout.notes">
        <slot name="computeShaderSlang"><a href="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/ComputeShader.slang">ComputeShader.slang</a></slot>
    </resource>
</p>

```slang
struct Constants
{
    uint Width;

    uint Height;

    DescriptorHandle<Texture2D> Input;

    DescriptorHandle<RWTexture2D<float4>> Output;
};

ConstantBuffer<Constants> constants;
```

<p>
    <resource key="concepts.shaders.layout.reference"></resource>
</p>
<h3 id="keep-matrix-storage-and-multiplication-consistent">
    <resource key="concepts.shaders.matrices.title"></resource>
</h3>
<p>
    <resource key="concepts.shaders.matrices.description">
        <slot name="link"><a href="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/SpinningCubeRenderer.cs"><resource key="concepts.shaders.matrices.description.link"></resource></a></slot>
        <slot name="matrix4x4"><code>Matrix4x4</code></slot>
        <slot name="detail"><a href="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/SpinningCube.slang"><resource key="concepts.shaders.matrices.description.detail"></resource></a></slot>
        <slot name="mulVectorMatrix"><code>mul(vector, matrix)</code></slot>
    </resource>
</p>
<p>
    <resource key="concepts.shaders.matrices.details"></resource>
</p>
<p>
    <a id="resource-handles"></a>
</p>
<h2 id="bind-the-constant-buffer-and-the-resources-it-describes">
    <resource key="concepts.shaders.binding.title"></resource>
</h2>
<p>
    <resource key="concepts.shaders.binding.description">
        <slot name="setConstantBuffer"><a class="xref" href="xref:Zenith.NET.CommandBuffer.SetConstantBuffer(Zenith.NET.Buffer,System.UInt32)"><code>SetConstantBuffer</code></a></slot>
    </resource>
</p>
<p>
    <resource key="concepts.shaders.binding.details"></resource>
</p>

```csharp
commandBuffer.SetPipeline(computePipeline);
commandBuffer.SetConstantBuffer(constantBuffer, 0);
commandBuffer.Dispatch(groupCountX, groupCountY, 1);
```

<p>
    <resource key="concepts.shaders.binding.guidance"><slot name="commandBuffer"><code>commandBuffer</code></slot>
        <slot name="computePipeline"><code>computePipeline</code></slot>
        <slot name="constantBuffer"><code>constantBuffer</code></slot>
        <slot name="bufferUsagesConstant"><a class="xref" href="xref:Zenith.NET.BufferUsages.Constant"><code>BufferUsages.Constant</code></a></slot>
    </resource>
</p>
<p>
    <resource key="concepts.shaders.binding.context"><slot name="input"><code>Input</code></slot>
        <slot name="inputTextureSampledHandle"><code>inputTexture.<a class="code-reference" href="xref:Zenith.NET.Texture.SampledHandle">SampledHandle</a></code></slot>
        <slot name="output"><code>Output</code></slot>
        <slot name="outputTextureStorageHandle"><code>outputTexture.<a class="code-reference" href="xref:Zenith.NET.Texture.StorageHandle">StorageHandle</a></code></slot>
        <slot name="descriptorHandle"><code>DescriptorHandle</code></slot>
    </resource>
</p>
<table>
    <thead>
        <tr>
            <th>
                <resource key="concepts.shaders.binding.table.headings.cHandle"></resource>
            </th>
            <th>
                <resource key="concepts.shaders.binding.table.headings.slangFieldType"></resource>
            </th>
            <th>
                <resource key="concepts.shaders.binding.table.headings.access"></resource>
            </th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td><code>texture.<a class="code-reference" href="xref:Zenith.NET.Texture.SampledHandle">SampledHandle</a></code>
            </td>
            <td><code>DescriptorHandle&lt;Texture2D&gt;</code>
            </td>
            <td>
                <resource key="concepts.shaders.binding.table.textureSampledHandle.access"></resource>
            </td>
        </tr>
        <tr>
            <td><code>texture.<a class="code-reference" href="xref:Zenith.NET.Texture.StorageHandle">StorageHandle</a></code>
            </td>
            <td><code>DescriptorHandle&lt;RWTexture2D&lt;float4&gt;&gt;</code>
            </td>
            <td>
                <resource key="concepts.shaders.binding.table.textureStorageHandle.access"></resource>
            </td>
        </tr>
        <tr>
            <td><code>buffer.<a class="code-reference" href="xref:Zenith.NET.Buffer.StorageReadOnlyHandle">StorageReadOnlyHandle</a></code>
            </td>
            <td><code>DescriptorHandle&lt;StructuredBuffer&lt;T&gt;&gt;</code>
            </td>
            <td>
                <resource key="concepts.shaders.binding.table.bufferStorageReadOnlyHandle.access">
                    <slot name="t"><code>T</code></slot>
                </resource>
            </td>
        </tr>
        <tr>
            <td><code>buffer.<a class="code-reference" href="xref:Zenith.NET.Buffer.StorageReadWriteHandle">StorageReadWriteHandle</a></code>
            </td>
            <td><code>DescriptorHandle&lt;RWStructuredBuffer&lt;T&gt;&gt;</code>
            </td>
            <td>
                <resource key="concepts.shaders.binding.table.bufferStorageReadWriteHandle.access">
                    <slot name="t"><code>T</code></slot>
                </resource>
            </td>
        </tr>
        <tr>
            <td><code>sampler.<a class="code-reference" href="xref:Zenith.NET.Sampler.Handle">Handle</a></code>
            </td>
            <td><code>DescriptorHandle&lt;SamplerState&gt;</code>
            </td>
            <td>
                <resource key="concepts.shaders.binding.table.samplerHandle.access"></resource>
            </td>
        </tr>
    </tbody>
</table>
<p>
    <resource key="concepts.shaders.binding.notes">
        <slot name="resourceHandle"><a class="xref" href="xref:Zenith.NET.ResourceHandle"><code>ResourceHandle</code></a></slot>
    </resource>
</p>
<p>
    <resource key="concepts.shaders.binding.reference">
        <slot name="link"><a href="~/learn/concepts/resource-management.md#views-and-lifetime"><resource key="concepts.shaders.binding.reference.link"></resource></a></slot>
    </resource>
</p>
<h2 id="dispatch-thread-groups-not-pixels">
    <resource key="concepts.shaders.dispatch.title"></resource>
</h2>
<p>
    <resource key="concepts.shaders.dispatch.description">
        <slot name="dispatch"><a class="xref" href="xref:Zenith.NET.CommandBuffer.Dispatch(System.UInt32,System.UInt32,System.UInt32)"><code>Dispatch</code></a></slot>
        <slot name="numthreads"><code>[numthreads(16, 16, 1)]</code></slot>
    </resource>
</p>
<p>
    <resource key="concepts.shaders.dispatch.details">
        <slot name="shaderDescThreadGroupSize"><a class="xref" href="xref:Zenith.NET.ShaderDesc.ThreadGroupSize"><code>ShaderDesc.ThreadGroupSize</code></a></slot>
        <slot name="dispatch"><a class="xref" href="xref:Zenith.NET.CommandBuffer.Dispatch(System.UInt32,System.UInt32,System.UInt32)"><code>Dispatch</code></a></slot>
        <slot name="computeShaderRendererCs"><a href="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/ComputeShaderRenderer.cs">ComputeShaderRenderer.cs</a></slot>
        <slot name="link"><a href="~/learn/concepts/synchronization.md#layouts"><resource key="concepts.shaders.dispatch.details.link"></resource></a></slot>
    </resource>
</p>
