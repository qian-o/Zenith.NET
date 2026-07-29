# Rasterization

Rasterization draws vertices and indexed geometry into color and depth/stencil attachments. A graphics pipeline combines Slang shaders, vertex input, attachment formats, and render state.

## Compile the Shaders

Compile the vertex and fragment entry points for the active context with `ZenithCompiler`, then create both shaders from their descriptions. See [Shaders](../fundamentals/shaders.md) for compilation from files or source strings.

## Create the Pipeline

Define one `InputLayout` for each vertex-buffer slot. `Add` appends an element and updates the stream stride:

```csharp
InputLayout inputLayout = new();
inputLayout.Add(new()
{
    Format = ElementFormat.Float4,
    Semantic = ElementSemantic.Position
});
```

Add the remaining elements in the same order as the shader input.

Create a pipeline from a description whose attachment formats match the render pass:

```csharp
GraphicsPipelineDesc desc = new()
{
    VertexShader = vertexShader,
    FragmentShader = fragmentShader,
    InputLayouts = [inputLayout],
    PrimitiveTopology = PrimitiveTopology.TriangleList,
    AttachmentFormats = attachmentFormats,
    RenderState = renderState
};

GraphicsPipeline pipeline = context.CreateGraphicsPipeline(desc);
```

Keep the input element order, semantics, and formats aligned with the Slang vertex input.

## Draw in a Render Pass

Transition the attachments, begin the pass, bind the pipeline state, and draw:

```csharp
commandBuffer.Transition(texture, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
commandBuffer.BeginRenderPass([ColorAttachment.Clear(texture, default)], null);

commandBuffer.SetPipeline(pipeline);
commandBuffer.Draw(vertexCount, 1, 0, 0);

commandBuffer.EndRenderPass();
```

Choose `Load`, `Clear`, or `DontCare` for each attachment according to whether previous contents are needed. `BeginRenderPass` initializes the viewport and scissor from the attachment size.

Set a smaller viewport and scissor after beginning the pass when rendering to only part of an attachment.

Use `Draw` for non-indexed geometry and `DrawIndexed` for indexed geometry. `DrawIndirect` and `DrawIndexedIndirect` read commands from a buffer created with `BufferUsages.Indirect`.

See [Bindless Resources](../fundamentals/bindless-resources.md) for shader-visible resources and [Synchronization](../fundamentals/synchronization.md) when GPU work produces indirect arguments.
