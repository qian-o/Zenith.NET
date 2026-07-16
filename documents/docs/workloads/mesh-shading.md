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

## MeshShadingPipelineDesc

Create mesh shading pipelines with `GraphicsContext.CreateMeshShadingPipeline` and `MeshShadingPipelineDesc`.

| Field | Type | Description |
|-------|------|-------------|
| `TaskShader` | `Shader?` | Optional task shader |
| `MeshShader` | `Shader` | Required mesh shader |
| `FragmentShader` | `Shader` | Fragment shader |
| `PrimitiveTopology` | `PrimitiveTopology` | Primitive topology for rasterization |
| `AttachmentFormats` | `AttachmentFormats` | Render-pass compatibility |
| `RenderState` | `RenderState` | Rasterizer, depth/stencil, and blend state |

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
commandBuffer.Transition(color, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
commandBuffer.Transition(depthStencil, default, TextureLayout.Undefined, TextureLayout.DepthStencilAttachment);

commandBuffer.BeginRenderPass([ColorAttachment.Clear(color, clearColor)], DepthStencilAttachment.Clear(depthStencil, 1.0f, 0));
commandBuffer.SetPipeline(pipeline);
commandBuffer.SetConstantBuffer(constantBuffer, 0);
commandBuffer.DispatchMesh(groupCountX, groupCountY, groupCountZ);
commandBuffer.EndRenderPass();
```

## Indirect Mesh Dispatch

Create the argument buffer with `BufferUsages.Indirect`. When GPU work writes the group counts, also include the matching storage usage and synchronize the producer before dispatch:

```csharp
commandBuffer.DispatchMeshIndirect(indirectBuffer, offsetInBytes, dispatchCount);
```

`IndirectDispatchMeshArgs` contains `GroupCountX`, `GroupCountY`, and `GroupCountZ`.

Use [Synchronization](../fundamentals/synchronization.md) when GPU work produces mesh data or indirect arguments. See [Bindless Resources](../fundamentals/bindless-resources.md) for shader-visible resources.
