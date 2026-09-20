# Shader Data and Binding

A shader receives data supplied by the application. Vertex attributes describe individual vertices; buffers hold parameters or arrays; resource handles identify other resources the shader will access. The C# declarations and Slang declarations must agree on what those values mean. Matching a field name is not enough: layout, resource access and binding all need to match.

## Compile an entry point for the selected context

[`ZenithCompiler`](xref:Zenith.NET.ZenithCompiler) compiles a named Slang entry point and returns a [`ShaderDesc`](xref:Zenith.NET.ShaderDesc). Use the context's `GraphicsApi` so the result targets the backend that will create the shader: DXIL for DirectX 12, a Metal library for Metal, or SPIR-V for Vulkan.

For example, with an existing `context` and the triangle's shader copied beside the executable:

```csharp
string shaderPath = Path.Combine(AppContext.BaseDirectory, "Triangle.slang");

Shader vertexShader = context.CreateShader(ZenithCompiler.CompileFromFile(context.GraphicsApi, shaderPath, "VSMain"));
```

`VSMain` must match the entry-point name in the file. Create the pipeline while its shader objects are valid; they can be disposed after pipeline creation, as shown in [First Triangle](../first-triangle.md#pipeline). Compile shaders and create pipelines during initialization rather than repeating that work for every frame.

<a id="data-layout"></a>
## Vertex input and shader data have different layout rules

For vertex input, the pipeline's [`InputLayout`](xref:Zenith.NET.InputLayout) describes formats, offsets, stride and semantics. The triangle's `Float3` position followed by a `Float4` color occupies 28 bytes, with color at byte 12. `InputLayout.Add` computes this packed layout; it does not inspect the C# struct. If the C# fields contain padding, supply a matching layout instead of assuming the fields are packed.

Constant buffers hold shader parameters, while structured buffers hold arrays of elements of a declared type. Both are read according to their Slang declarations. They do not use `InputLayout`. Verify field offsets and element sizes for the compiled shader representation, including padding. An unmanaged C# type is suitable for copying as bytes, but that alone does not prove it matches a shader type. In particular, do not apply a vertex's tightly packed `float3` layout to every shader buffer.

A resource handle is a small value that refers to an existing resource. Copying the handle into a constant buffer does not copy the resource's contents.

The [compute sample's C# constants](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/ComputeShaderRenderer.cs) make their offsets explicit. This declaration needs `System.Runtime.InteropServices` and `Zenith.NET` in scope:

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

The corresponding declaration in [ComputeShader.slang](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/ComputeShader.slang) is:

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

The dimensions occupy the first eight bytes, followed by two eight-byte resource handles. The C# sample reserves 256 bytes in total. That size is the sample's chosen storage size, not a rule that all Slang structs or constant payloads occupy 256 bytes. Padding the total size also does not correct a field at the wrong offset.

### Keep matrix storage and multiplication consistent

The compiler requests row-major matrix storage, which stores each row's elements together. The [spinning cube renderer](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/SpinningCubeRenderer.cs) places three `Matrix4x4` values at offsets 0, 64 and 128. Its [shader](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/SpinningCube.slang) uses `mul(vector, matrix)` in model, view and projection order.

Storage order determines how matrix elements occupy memory; multiplication order determines the transformation. Keep both sides consistent. Adding a transpose without checking those two choices can conceal one mismatch while introducing another.

<a id="resource-handles"></a>
## Bind the constant buffer and the resources it describes

[`SetConstantBuffer`](xref:Zenith.NET.CommandBuffer.SetConstantBuffer(Zenith.NET.Buffer,System.UInt32)) binds a buffer and byte offset for the current pipeline's constant data. It binds existing GPU-visible storage; it does not upload a C# value. Fill the buffer first, and ensure earlier GPU readers have finished before overwriting its bytes.

Set the pipeline before its constant buffer. For a compute workload with resources already created and uploaded, recording uses this order:

```csharp
commandBuffer.SetPipeline(computePipeline);
commandBuffer.SetConstantBuffer(constantBuffer, 0);
commandBuffer.Dispatch(groupCountX, groupCountY, 1);
```

This fragment assumes an already-recording `commandBuffer`, a `computePipeline`, an uploaded `constantBuffer` with `BufferUsages.Constant`, and the thread-group counts for the workload. Offset 0 binds the beginning of the buffer. If several constant-data records share a buffer, each record's starting offset must satisfy the backend's binding alignment. That requirement is separate from the layout of fields inside each record.

In the compute sample, `Input` is filled with `inputTexture.SampledHandle` and `Output` with `outputTexture.StorageHandle`. Slang's typed `DescriptorHandle` tells the shader how to access each resource:

| C# handle | Slang field type | Intended access |
| --- | --- | --- |
| `texture.SampledHandle` | `DescriptorHandle<Texture2D>` | Read a texture. |
| `texture.StorageHandle` | `DescriptorHandle<RWTexture2D<float4>>` | Read or write a storage texture. |
| `buffer.StorageReadOnlyHandle` | `DescriptorHandle<StructuredBuffer<T>>` | Read structured elements of shader type `T`. |
| `buffer.StorageReadWriteHandle` | `DescriptorHandle<RWStructuredBuffer<T>>` | Read or write structured elements of shader type `T`. |
| `sampler.Handle` | `DescriptorHandle<SamplerState>` | Supply texture sampling state. |

[`ResourceHandle`](xref:Zenith.NET.ResourceHandle) stores two 32-bit fields, but its interpretation belongs to the backend. Do not treat it as a universal descriptor index, address or value that can be transferred between contexts. A handle does not create a resource, add a missing usage or perform synchronization.

Keep references to the resource and any view used to obtain its handle, and dispose them only after the shader's last use completes. When replacing a texture or view, update the constants that contain its old handle. [Resource Management](resource-management.md#views-and-lifetime) explains that lifetime relationship.

## Dispatch thread groups, not pixels

`Dispatch` takes the number of thread groups. The compute sample's `[numthreads(16, 16, 1)]` entry point runs 16 × 16 threads per group. Divide the image dimensions by 16 and round up to find the group counts. For example, a 17 × 19 image needs 2 × 2 groups. The shader checks its pixel coordinates against the image dimensions because the last groups can contain threads outside the image.

The compiler stores the entry point's declared group size in `ShaderDesc.ThreadGroupSize`; it does not convert pixel dimensions supplied to `Dispatch` into group counts. Use [ComputeShaderRenderer.cs](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/ComputeShaderRenderer.cs) with its shader for the complete binding and dispatch example, and [Synchronization](synchronization.md#layouts) before another pass consumes the output.
