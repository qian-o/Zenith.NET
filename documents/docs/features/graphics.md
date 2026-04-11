# Graphics

The graphics pipeline handles traditional rasterization with vertex and pixel shaders, configurable render states, and input layouts.

## Graphics Pipeline

Create a pipeline by specifying shaders, render states, input layout, and output format:

```csharp
GraphicsPipeline pipeline = context.CreateGraphicsPipeline(new GraphicsPipelineDesc
{
    RenderStates = new()
    {
        RasterizerState = RasterizerStates.CullBack,
        DepthStencilState = DepthStencilStates.Default,
        BlendState = BlendStates.Opaque
    },
    Vertex = vertexShader,
    Pixel = pixelShader,
    ResourceBindings = resourceBindings,
    InputLayouts = [inputLayout],
    PrimitiveTopology = PrimitiveTopology.TriangleList,
    Output = frameBuffer.Output
});
```

### GraphicsPipelineDesc

| Field | Type | Description |
|-------|------|-------------|
| `RenderStates` | `RenderStates` | Rasterizer, depth/stencil, and blend configuration |
| `Vertex` | `Shader` | Vertex shader |
| `Pixel` | `Shader` | Pixel (fragment) shader |
| `ResourceBindings` | `ResourceBinding[]` | Shader resource binding declarations |
| `InputLayouts` | `InputLayout[]` | Vertex attribute layouts |
| `PrimitiveTopology` | `PrimitiveTopology` | Primitive assembly mode |
| `Output` | `Output` | Render target format description |

## Render States

### Rasterizer State

Controls face culling, fill mode, and depth bias:

```csharp
RasterizerState rasterizerState = new()
{
    CullMode = CullMode.Back,
    FillMode = FillMode.Solid,
    FrontFace = FrontFace.CounterClockwise,
    DepthClipEnable = true
};
```

Built-in presets: `RasterizerStates.CullNone`, `RasterizerStates.CullBack`, `RasterizerStates.CullFront`.

### Depth/Stencil State

Controls depth testing and stencil operations:

```csharp
DepthStencilState depthStencilState = new()
{
    DepthEnable = true,
    DepthWriteEnable = true,
    DepthFunc = ComparisonFunc.Less
};
```

Built-in presets: `DepthStencilStates.Default` (depth enabled), `DepthStencilStates.None` (depth disabled).

### Blend State

Controls color blending for each render target:

```csharp
BlendState blendState = new()
{
    RenderTarget0 = new()
    {
        BlendEnable = true,
        SrcBlend = Blend.SrcAlpha,
        DestBlend = Blend.InverseSrcAlpha,
        BlendOp = BlendOp.Add,
        SrcBlendAlpha = Blend.One,
        DestBlendAlpha = Blend.InverseSrcAlpha,
        BlendOpAlpha = BlendOp.Add,
        Flags = ColorComponentFlags.All
    }
};
```

Built-in presets: `BlendStates.Opaque`, `BlendStates.AlphaBlend`, `BlendStates.Additive`.

## Input Layout

Input layouts describe how vertex data maps to shader inputs:

```csharp
InputLayout inputLayout = new();
inputLayout.Add(new() { Format = ElementFormat.Float3, Semantic = ElementSemantic.Position });
inputLayout.Add(new() { Format = ElementFormat.Float2, Semantic = ElementSemantic.TexCoord });
```

The order must match the shader's vertex input struct. Multiple input layouts can be used for multi-stream vertex data.

## Shader Stages

| Stage | Flag | Purpose |
|-------|------|---------|
| Vertex | `ShaderStageFlags.Vertex` | Transform vertices and pass data to the pixel stage |
| Pixel | `ShaderStageFlags.Pixel` | Compute the final color for each fragment |

## Primitive Topology

| Topology | Description |
|----------|-------------|
| `PointList` | Each vertex is a point |
| `LineList` | Every 2 vertices form a line |
| `LineStrip` | Connected line segments |
| `TriangleList` | Every 3 vertices form a triangle |
| `TriangleStrip` | Connected triangles sharing edges |

## Rendering

```csharp
CommandBuffer commandBuffer = context.Graphics.CommandBuffer();

commandBuffer.BeginRenderPass(frameBuffer, clearValue, resourceTable);
commandBuffer.SetPipeline(pipeline);
commandBuffer.PushResourceTable(resourceTable);
commandBuffer.SetVertexBuffer(vertexBuffer, 0, 0);
commandBuffer.SetIndexBuffer(indexBuffer, 0, IndexFormat.UInt32);
commandBuffer.DrawIndexed(indexCount, 1, 0, 0, 0);
commandBuffer.EndRenderPass();

commandBuffer.Submit(waitForCompletion: true);
```

## Viewports and Scissors

Custom viewports and scissors can be set within a render pass:

```csharp
commandBuffer.SetViewports([new Viewport
{
    X = 0, Y = 0,
    Width = width, Height = height,
    MinDepth = 0, MaxDepth = 1
}]);

commandBuffer.SetScissors([new Scissor
{
    X = 0, Y = 0,
    Width = width, Height = height
}]);
```
