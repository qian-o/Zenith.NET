# Hello Triangle

Hello Triangle is the smallest complete graphics workload in the series. It uploads three colored vertices, compiles one vertex and one fragment entry point, creates a graphics pipeline, and records a single draw into the swap-chain texture.

This guide focuses on how vertex data moves from C# through a Zenith.NET pipeline into Slang. The shared window, command submission, and presentation loop come from [Application Host](../getting-started/application-host.md).

## Result

![A vertex-colored triangle rendered with Zenith.NET](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/hello-triangle.png)

The final image is one triangle on a dark clear color. Red, green, and blue values are supplied per vertex and interpolated by the rasterizer across the covered fragments.

## Frame overview

The renderer creates its long-lived resources once. Each frame then records one render pass:

```mermaid
flowchart LR
    A[Drawable in ColorAttachment] --> B[Begin render pass and clear]
    B --> C[Bind graphics pipeline]
    C --> D[Bind vertex buffer]
    D --> E[Draw 3 vertices]
    E --> F[End render pass]
```

The host transitions the drawable into `ColorAttachment` before calling the renderer and transitions it to `Present` afterward.

## Resource map

| Resource | Created from | Consumed by |
| --- | --- | --- |
| Vertex buffer | Three `Vertex` values | Vertex input stage |
| Vertex shader | `VSMain` in `HelloTriangle.slang` | Graphics pipeline |
| Fragment shader | `FSMain` in `HelloTriangle.slang` | Graphics pipeline |
| Graphics pipeline | Shaders, input layout, attachment format, render state | Draw command |
| Swap-chain drawable | Application host | Color attachment |

There is no index buffer, constant buffer, depth attachment, or descriptor handle in this first workload.

## Data contract

The CPU vertex stores a three-component position followed by a four-component color:

```csharp
[StructLayout(LayoutKind.Sequential)]
file struct Vertex(Vector3 position, Vector4 color)
{
    public Vector3 Position = position;

    public Vector4 Color = color;
}
```

The corresponding Slang input uses matching attribute types and semantic names:

```slang
struct VSInput
{
    float3 Position : POSITION0;

    float4 Color : COLOR0;
};
```

An `InputLayout` connects the buffer fields to those semantics:

```csharp
InputLayout inputLayout = new();
inputLayout.Add(new() { Format = ElementFormat.Float3, Semantic = ElementSemantic.Position });
inputLayout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Color });
```

The layout order matches the C# field order. `App.LoadBuffer` supplies `sizeof(Vertex)` as the buffer stride, so the GPU advances by one complete position-and-color record for each vertex.

## Create the vertex buffer

The renderer defines three positions directly in clip space. Their colors become the three corners visible in the result:

```csharp
Vertex[] vertices =
[
    new(new(0.0f, 0.6f, 0.0f), new(1.0f, 0.2f, 0.15f, 1.0f)),
    new(new(0.6f, -0.5f, 0.0f), new(0.15f, 0.85f, 0.35f, 1.0f)),
    new(new(-0.6f, -0.5f, 0.0f), new(0.2f, 0.45f, 1.0f, 1.0f))
];

vertexBuffer = App.LoadBuffer(vertices, BufferUsages.Vertex);
```

`BufferUsages.Vertex` declares that later commands will bind this allocation as vertex input. Because the coordinates already lie in clip space, the workload does not need model, view, or projection matrices.

## Shader interface

The vertex shader converts each `float3` position into homogeneous clip-space coordinates and forwards the color:

```slang
struct FSInput
{
    float4 Position : SV_POSITION;

    float4 Color : COLOR0;
};

[shader("vertex")]
FSInput VSMain(VSInput input)
{
    FSInput output;
    output.Position = float4(input.Position, 1.0);
    output.Color = input.Color;

    return output;
}
```

`SV_POSITION` is consumed by rasterization. `COLOR0` is a user varying: the rasterizer interpolates it, then supplies the interpolated value to the fragment shader.

```slang
[shader("fragment")]
float4 FSMain(FSInput input) : SV_TARGET
{
    return input.Color;
}
```

`SV_TARGET` marks the returned value as the color written to the active color attachment.

## Create the pipeline

Compile both named entry points and create the graphics pipeline:

```csharp
using Shader vertexShader = App.LoadShader("HelloTriangle.slang", "VSMain");
using Shader fragmentShader = App.LoadShader("HelloTriangle.slang", "FSMain");

pipeline = App.Context.CreateGraphicsPipeline(new()
{
    VertexShader = vertexShader,
    FragmentShader = fragmentShader,
    InputLayouts = [inputLayout],
    PrimitiveTopology = PrimitiveTopology.TriangleList,
    AttachmentFormats = new()
    {
        ColorFormats = [App.ColorFormat],
        SampleCount = SampleCount.Count1
    },
    RenderState = new()
    {
        Rasterizer = RasterizerState.CullNone(),
        DepthStencil = DepthStencilState.DepthNone(),
        Blend = BlendState.Opaque()
    }
});
```

The attachment format must match the swap chain. A triangle list consumes every group of three vertices as one triangle. Culling and depth testing are unnecessary because this workload has one two-dimensional primitive and no depth attachment.

The `using` declarations limit the temporary `Shader` objects to pipeline creation.

## Record the frame

The render method clears the drawable, binds the two resources required by the draw, and emits one triangle:

```csharp
public void Render(CommandBuffer commandBuffer, Texture drawable)
{
    commandBuffer.BeginRenderPass([ColorAttachment.Clear(drawable, new(0.04f, 0.055f, 0.075f, 1.0f))], null);

    commandBuffer.SetPipeline(pipeline);
    commandBuffer.SetVertexBuffer(vertexBuffer, 0, 0);

    commandBuffer.Draw(3, 1, 0, 0);

    commandBuffer.EndRenderPass();
}
```

The arguments to `Draw` request three vertices, one instance, first vertex zero, and first instance zero. No explicit viewport or scissor is needed here; the render pass uses the drawable dimensions.

## Resource lifetime

This renderer has no animated or size-dependent resources, so `Update` and `Resize` remain empty. Dispose both renderer-owned resources after the final submitted frame has completed:

```csharp
public void Dispose()
{
    pipeline.Dispose();
    vertexBuffer.Dispose();
}
```

## Inspect the result

Run the tutorial host and select **Hello Triangle**. Confirm that:

- the triangle appears centered on the dark background;
- all three corner colors are visible and interpolate smoothly;
- resizing the window preserves the workload without recreating resources;
- the validation layer reports no errors.

If the pipeline is rejected, first compare `App.ColorFormat` with `AttachmentFormats.ColorFormats`. If geometry is missing or corrupted, compare the C# field order, `InputLayout`, and Slang semantics.

## Explore further

Try these changes independently:

1. Change the x and y components while keeping them in $[-1,1]$; leave z at 0 so the vertices remain in the visible depth range.
2. Replace all three colors with one constant color and observe that interpolation no longer produces a gradient.
3. Reverse two vertices and enable back-face culling to inspect how winding affects visibility.
4. Add a fourth vertex and change the topology to explore how a triangle list groups input vertices.

## Complete source

- [HelloTriangleRenderer.cs](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/HelloTriangleRenderer.cs)
- [HelloTriangle.slang](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/HelloTriangle.slang)

Continue with [Spinning Cube](spinning-cube.md) to add indexed geometry, transformation constants, back-face culling, and a depth attachment.
