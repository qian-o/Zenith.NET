# Best Practices

Guidelines for getting the most out of Zenith.NET.

## Resource Lifecycle

- **Dispose resources** when no longer needed. All GPU resources implement `IDisposable`.
- **Use `using` statements** for short-lived resources like shaders used only during pipeline creation:

```csharp
using Shader vertexShader = context.LoadShaderFromSource(source, "VSMain", ShaderStageFlags.Vertex);
using Shader pixelShader = context.LoadShaderFromSource(source, "PSMain", ShaderStageFlags.Pixel);

pipeline = context.CreateGraphicsPipeline(new() { Vertex = vertexShader, Pixel = pixelShader, /* ... */ });
```

- **Create resources upfront** rather than per-frame to avoid allocation overhead.
- **Dispose in reverse order** — dispose dependent resources before the objects they reference, and dispose the `GraphicsContext` last.

## Command Recording

- **Batch similar operations** to minimize pipeline and resource table switches.
- **Minimize render pass switches** by grouping draws with the same frame buffer.
- **Reuse command buffers** — the queue automatically pools and recycles them. Just call `CommandBuffer()`.
- **Use `waitForCompletion: true`** sparingly — it stalls the CPU until the GPU finishes. Prefer submitting without waiting when possible.

## Resource Binding

- **Ensure `ResourceBinding[]` layout compatibility** between pipeline descriptors and resource tables — same types and counts at each index.
- **Call `Write()` to update bindings** at any time — avoid recreating resource tables for dynamic resource updates.
- **Pass resource tables to `BeginRenderPass`** to allow the backend to perform resource transitions.

## Data Alignment

Follow the [alignment constants](concepts/graphics-context.md#alignment-constants) defined in `GraphicsContext` for buffer and texture data.

Structs passed to the GPU via constant or structured buffers must use `[StructLayout(LayoutKind.Explicit)]` with 16-byte aligned field offsets. DirectX 12, Metal, and Vulkan have different alignment rules for buffer data — using 16-byte alignment ensures compatibility across all three backends. Avoid using `float3` in shader struct declarations for the same reason; use `float4` with padding instead. Vertex input data does not require this alignment.

```csharp
[StructLayout(LayoutKind.Explicit, Size = 240)]
file struct Constants
{
    [FieldOffset(0)]
    public Matrix4x4 Model;

    [FieldOffset(64)]
    public Matrix4x4 View;

    [FieldOffset(128)]
    public Matrix4x4 Projection;

    [FieldOffset(192)]
    public Vector3 LightPos;       // 12 bytes, padded to 16

    [FieldOffset(208)]
    public Vector3 LightColor;     // 12 bytes, padded to 16

    [FieldOffset(224)]
    public Vector3 CameraPos;      // 12 bytes, padded to 16
}
```

## Performance

- **Avoid per-frame resource creation.** Create buffers, textures, pipelines, and resource tables once and reuse.
- **Use `MapWrite` buffers** for frequently updated data (constant buffers, instance data). `Upload()` is convenient and efficient for these cases.
- **Use the Copy queue** for asynchronous data transfers when the Graphics queue is busy.
- **Prefer indexed drawing** (`DrawIndexed`) over non-indexed (`Draw`) to reduce vertex processing through vertex reuse.
- **Use indirect drawing** (`DrawIndexedIndirect`, `DispatchMeshIndirect`) for GPU-driven rendering to reduce CPU overhead.

## Debugging

- **Enable the validation layer** during development: `GraphicsContext.Create*(useValidationLayer: true)`.
- **Subscribe to `ValidationMessage`** to capture driver-level diagnostics emitted by the backend's native validation layer.
- **Use debug events** to annotate command streams for GPU debugging tools (RenderDoc, PIX, Xcode GPU Debugger):

```csharp
commandBuffer.BeginDebugEvent("Shadow Pass");
// ... shadow draw calls ...
commandBuffer.EndDebugEvent();
```

- **Name resources** for easier identification in GPU debuggers:

```csharp
buffer.Name = "VertexBuffer";
texture.Name = "Albedo";
```

## Common Pitfalls

| Pitfall | Solution |
|---------|----------|
| Forgetting to dispose GPU resources | Use `using` or explicit `Dispose()` calls |
| Mismatched resource bindings between pipeline and table | Ensure the `ResourceBinding[]` layout is compatible (same types and counts at each index) |
| Constant buffer struct size not matching shader layout | Use `LayoutKind.Explicit` with 16-byte aligned field offsets; avoid `float3` in shaders |
| Calling `WaitIdle()` every frame | Only wait when synchronization is actually needed |
| Creating pipelines per-frame | Create once, reuse across frames |
