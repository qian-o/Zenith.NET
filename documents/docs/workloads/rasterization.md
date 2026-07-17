# Rasterization

Rasterization draws vertices and indexed geometry into color and depth/stencil attachments. A graphics pipeline combines Slang shaders, vertex input, attachment formats, and render state.

## Compile the Shaders

Compile the vertex and fragment entry points for the active context:

```csharp
string shaderPath = "Assets/Shaders/Rasterization.slang";
using Shader vertexShader = context.CreateShader(
    ZenithCompiler.CompileFromFile(context.GraphicsApi, shaderPath, "VSMain"));
using Shader fragmentShader = context.CreateShader(
    ZenithCompiler.CompileFromFile(context.GraphicsApi, shaderPath, "FSMain"));
```

## Create the Pipeline

Define one `InputLayout` for each vertex-buffer slot. `Add` appends an element and updates the stream stride:

```csharp
InputLayout inputLayout = new();
inputLayout.Add(new()
{
    Format = ElementFormat.Float4,
    Semantic = ElementSemantic.Position
});
inputLayout.Add(new()
{
    Format = ElementFormat.Float4,
    Semantic = ElementSemantic.Normal
});
```

Create a pipeline whose attachment formats match the render pass:

```csharp
using GraphicsPipeline pipeline = context.CreateGraphicsPipeline(new()
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

Keep the input element order, semantics, and formats aligned with the Slang vertex input.

## Draw in a Render Pass

Transition the attachments, begin the pass, bind the pipeline state, and draw:

```csharp
commandBuffer.Transition(color, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
commandBuffer.Transition(depthStencil, default, TextureLayout.Undefined, TextureLayout.DepthStencilAttachment);
commandBuffer.BeginRenderPass(
    [ColorAttachment.Clear(color, clearColor)],
    DepthStencilAttachment.Clear(depthStencil, 1.0f, 0));

commandBuffer.SetPipeline(pipeline);
commandBuffer.SetVertexBuffer(vertexBuffer, 0, 0);
commandBuffer.SetIndexBuffer(indexBuffer, 0, IndexFormat.UInt32);
commandBuffer.SetConstantBuffer(constantBuffer, 0);
commandBuffer.DrawIndexed(indexCount, 1, 0, 0, 0);

commandBuffer.EndRenderPass();
```

Choose `Clear`, `Load`, or `DontCare` for each attachment according to whether previous contents are needed. `BeginRenderPass` initializes the viewport and scissor from the attachment size.

Set a smaller drawing region after beginning the pass when needed:

```csharp
commandBuffer.SetViewports([new()
{
    Width = width,
    Height = height,
    MaxDepth = 1.0f
}]);
commandBuffer.SetScissors([new()
{
    Width = width,
    Height = height
}]);
```

Use `Draw` for non-indexed geometry and `DrawIndexed` for indexed geometry. `DrawIndirect` and `DrawIndexedIndirect` read commands from a buffer created with `BufferUsages.Indirect`.

See [Bindless Resources](../fundamentals/bindless-resources.md) for shader-visible resources and [Synchronization](../fundamentals/synchronization.md) when GPU work produces indirect arguments.
