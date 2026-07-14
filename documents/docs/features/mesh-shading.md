# Mesh Shading

Mesh shading is an optional graphics path that replaces vertex/index assembly with task and mesh shader stages.

## Capability Check

Mesh shading is gated per device:

```csharp
if (!context.Capabilities.MeshShadingSupported)
{
    return;
}
```

## Slang Stage Terminology

Current stage naming in Slang should match pipeline roles:

- `[shader("task")]` for optional task shader
- `[shader("mesh")]` for required mesh shader
- `[shader("fragment")]` for fragment shader

## MeshShadingPipelineDesc

Create mesh shading pipelines with `GraphicsContext.CreateMeshShadingPipeline` and `MeshShadingPipelineDesc`.

| Field | Type | Description |
|-------|------|-------------|
| `TaskShader` | `Shader?` | Optional task shader |
| `MeshShader` | `Shader` | Required mesh shader |
| `FragmentShader` | `Shader` | Fragment shader |
| `PrimitiveTopology` | `PrimitiveTopology` | Primitive topology for rasterization |
| `AttachmentFormats` | `AttachmentFormats` | Color/depth formats and sample count |
| `RenderState` | `RenderState` | Rasterizer, depth/stencil, blend state |

```csharp
MeshShadingPipeline pipeline = context.CreateMeshShadingPipeline(new()
{
    TaskShader = taskShader,
    MeshShader = meshShader,
    FragmentShader = fragmentShader,
    PrimitiveTopology = PrimitiveTopology.TriangleList,
    AttachmentFormats = new()
    {
        ColorFormats = [PixelFormat.B8G8R8A8SRgb],
        DepthStencilFormat = PixelFormat.D32Float,
        SampleCount = SampleCount.Count1
    },
    RenderState = new()
    {
        Rasterizer = RasterizerState.CullNone(),
        DepthStencil = DepthStencilState.DepthReadWrite(),
        Blend = BlendState.Opaque()
    }
});
```

`TaskShader` may be `null` for mesh-only pipelines.

## DispatchMesh and Render Pass Flow

Mesh dispatch runs inside a render pass, like graphics draws:

```csharp
commandBuffer.Transition(color, default, TextureLayout.ColorAttachment);
commandBuffer.Transition(depthStencil, default, TextureLayout.DepthStencilAttachment);

commandBuffer.BeginRenderPass([ColorAttachment.Clear(color, clearColor)], DepthStencilAttachment.Clear(depthStencil, 1.0f, 0));
commandBuffer.SetPipeline(pipeline);
commandBuffer.SetConstantBuffer(constantBuffer, 0);
commandBuffer.DispatchMesh(groupCountX, groupCountY, groupCountZ);
commandBuffer.EndRenderPass();
```

## Indirect Mesh Dispatch

For GPU-driven group counts:

```csharp
commandBuffer.DispatchMeshIndirect(indirectBuffer, offsetInBytes, dispatchCount);
```

`IndirectDispatchMeshArgs` contains `GroupCountX`, `GroupCountY`, and `GroupCountZ`.

If compute generates this indirect buffer, add synchronization before dispatching mesh work.

## Bindless Resources and Barriers

Mesh/task/fragment shader data follows the same bindless model:

- Put `ResourceHandle` values in explicit-layout C# constants.
- Bind constants with `SetConstantBuffer`.
- Resolve handles in Slang with `DescriptorHandle<T>`.

Typical dependency when compute culls or compacts before mesh shading:

```csharp
computeCommands.SetPipeline(cullingPipeline);
computeCommands.SetConstantBuffer(cullingConstants, 0);
computeCommands.Dispatch(cullX, cullY, cullZ);
computeCommands.Barrier(BarrierStages.ComputeShading, BarrierStages.VertexShading);
```

Use texture `Transition(...)` for layout changes and `Barrier(...)` for producer/consumer ordering without layout changes.

See [Synchronization and Barriers](../concepts/synchronization.md) and [Bindless Resources](../concepts/resource-binding.md).
