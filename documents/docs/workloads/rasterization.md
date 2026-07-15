# Rasterization

Rasterization combines compiled Slang stages, vertex input, fixed-function state, compatible attachments, and draw commands inside a render pass.

## Compile Slang Vertex and Fragment Shaders

Use `ZenithCompiler` with the selected Graphics API and create `Shader` objects from the returned `ShaderDesc`:

```csharp
using Shader vertexShader = context.CreateShader(ZenithCompiler.CompileFromFile(context.GraphicsApi, "Assets/Shaders/Rasterization.slang", "VSMain"));
using Shader fragmentShader = context.CreateShader(ZenithCompiler.CompileFromFile(context.GraphicsApi, "Assets/Shaders/Rasterization.slang", "FSMain"));
```

## GraphicsPipelineDesc

Create pipelines with `GraphicsContext.CreateGraphicsPipeline` and `GraphicsPipelineDesc`.

| Field | Type | Description |
|-------|------|-------------|
| `VertexShader` | `Shader` | Vertex shader |
| `FragmentShader` | `Shader` | Fragment shader |
| `InputLayouts` | `InputLayout[]` | Vertex stream layout(s) |
| `PrimitiveTopology` | `PrimitiveTopology` | Primitive type (`TriangleList`, etc.) |
| `AttachmentFormats` | `AttachmentFormats` | Color/depth formats and sample count |
| `RenderState` | `RenderState` | Rasterizer, depth/stencil, and blend state |

```csharp
InputLayout inputLayout = new();
inputLayout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Position });
inputLayout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Normal });

GraphicsPipeline pipeline = context.CreateGraphicsPipeline(new()
{
    VertexShader = vertexShader,
    FragmentShader = fragmentShader,
    InputLayouts = [inputLayout],
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

## Vertex Input

`InputLayout` stores stream stride and `InputElement[]`. Calling `Add` appends an element and advances `StrideInBytes` automatically using the element format size.

Keep element order and semantics aligned with the Slang vertex input struct.

The render-pass attachments must match the pipeline's `AttachmentFormats`. `RenderState` groups rasterizer, depth/stencil, and blend state.

## Direct Attachment Render Passes

Render passes receive explicit attachment structs directly:

```csharp
commandBuffer.Transition(color, default, TextureLayout.ColorAttachment);
commandBuffer.Transition(depthStencil, default, TextureLayout.DepthStencilAttachment);

commandBuffer.BeginRenderPass([ColorAttachment.Clear(color, new(0.51f, 0.518f, 0.557f, 1.0f))], DepthStencilAttachment.Clear(depthStencil, 1.0f, 0));

commandBuffer.SetPipeline(pipeline);
commandBuffer.SetVertexBuffer(vertexBuffer, 0, 0);
commandBuffer.SetIndexBuffer(indexBuffer, 0, IndexFormat.UInt32);
commandBuffer.SetConstantBuffer(constantBuffer, 0);
commandBuffer.DrawIndexed(indexCount, 1, 0, 0, 0);

commandBuffer.EndRenderPass();
commandBuffer.Transition(color, default, TextureLayout.Sampled);
```

Use `ColorAttachment.Clear/Load/DontCare` and `DepthStencilAttachment.Clear/Load/DontCare` to choose load/store behavior per pass.

## Draw and Indirect Commands

Graphics draw entry points:

- `Draw(vertexCount, instanceCount, firstVertex, firstInstance)`
- `DrawIndexed(indexCount, instanceCount, firstIndex, vertexOffset, firstInstance)`
- `DrawIndirect(indirectBuffer, offsetInBytes, drawCount)`
- `DrawIndexedIndirect(indirectBuffer, offsetInBytes, drawCount)`

See [Synchronization](../fundamentals/synchronization.md) when another GPU command produces indirect arguments.

## Viewport and Scissor

`BeginRenderPass` initializes default viewport/scissor from attachment size. Override dynamically when needed:

```csharp
commandBuffer.SetViewports([new() { Width = width, Height = height, MaxDepth = 1.0f }]);
commandBuffer.SetScissors([new() { Width = width, Height = height }]);
```

For shader-visible resources, see [Bindless Resources](../fundamentals/bindless-resources.md).
