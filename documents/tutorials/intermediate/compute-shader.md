# Compute Shader

In this tutorial, you'll learn how to use compute shaders with Zenith.NET. We'll create a simple image processing effect that converts a color image to grayscale on the GPU.

## Overview

We'll create a `ComputeShaderRenderer` class that:

- Loads an image as an input texture
- Creates an output texture with read/write access
- Builds a compute pipeline
- Dispatches compute work to process the image

## Key Concepts

| Concept | Description |
|---------|-------------|
| `ComputePipeline` | Pipeline for compute shader execution |
| `ShaderStageFlags.Compute` | Indicates a compute shader stage |
| `TextureUsageFlags.UnorderedAccess` | Allows read/write access in compute shaders |
| `ResourceType.TextureReadWrite` | Binding type for writable textures (UAV) |
| `Dispatch` | Execute compute shader with specified thread groups |

## The Renderer Class

Create a new file `Renderers/ComputeShaderRenderer.cs`:

```csharp
using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Zenith.NET;
using Zenith.NET.Extensions.ImageSharp;
using Zenith.NET.Extensions.Slang;
using Buffer = Zenith.NET.Buffer;

namespace ZenithTutorials.Renderers;

internal class ComputeShaderRenderer : IRenderer
{
    private const uint ThreadGroupSize = 16;

    private const string computeShaderSource = """
        Texture2D inputTexture;
        RWTexture2D outputTexture;

        [numthreads(16, 16, 1)]
        void CSMain(uint3 dispatchThreadID : SV_DispatchThreadID)
        {
            uint width, height;
            outputTexture.GetDimensions(width, height);

            // Bounds check
            if (dispatchThreadID.x >= width || dispatchThreadID.y >= height)
            {
                return;
            }

            // Read input pixel
            float4 color = inputTexture[dispatchThreadID.xy];

            // Convert to grayscale using luminance weights
            float gray = dot(color.rgb, float3(0.299, 0.587, 0.114));

            // Write to output
            outputTexture[dispatchThreadID.xy] = float4(gray, gray, gray, color.a);
        }
        """;

    // Shader for displaying the processed texture
    private const string displayShaderSource = """
        struct VSInput
        {
            float3 Position : POSITION0;

            float2 TexCoord : TEXCOORD0;
        };

        struct PSInput
        {
            float4 Position : SV_POSITION;

            float2 TexCoord : TEXCOORD0;
        };

        Texture2D displayTexture;
        SamplerState samplerState;

        PSInput VSMain(VSInput input)
        {
            PSInput output;
            output.Position = float4(input.Position, 1.0);
            output.TexCoord = input.TexCoord;

            return output;
        }

        float4 PSMain(PSInput input) : SV_TARGET
        {
            return displayTexture.Sample(samplerState, input.TexCoord);
        }
        """;

    [StructLayout(LayoutKind.Sequential)]
    private struct Vertex(Vector3 position, Vector2 texCoord)
    {
        public Vector3 Position = position;

        public Vector2 TexCoord = texCoord;
    }

    // Compute resources
    private readonly Texture inputTexture;
    private readonly Texture outputTexture;
    private readonly ResourceLayout computeResourceLayout;
    private readonly ResourceSet computeResourceSet;
    private readonly ComputePipeline computePipeline;

    // Display resources
    private readonly Buffer vertexBuffer;
    private readonly Buffer indexBuffer;
    private readonly Sampler sampler;
    private readonly ResourceLayout displayResourceLayout;
    private readonly ResourceSet displayResourceSet;
    private readonly GraphicsPipeline displayPipeline;

    private bool processed;

    public ComputeShaderRenderer()
    {
        // Load input texture
        inputTexture = App.Context.LoadTextureFromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "shoko.png"), generateMipMaps: false);

        // Create output texture with read/write access
        outputTexture = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R8G8B8A8UNorm,
            Width = inputTexture.Desc.Width,
            Height = inputTexture.Desc.Height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.ShaderResource | TextureUsageFlags.UnorderedAccess
        });

        // Create compute resource layout
        computeResourceLayout = App.Context.CreateResourceLayout(new()
        {
            Bindings = BindingHelper.Bindings
            (
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.TextureReadWrite, Count = 1, StageFlags = ShaderStageFlags.Compute }
            )
        });

        // Create compute resource set
        computeResourceSet = App.Context.CreateResourceSet(new()
        {
            Layout = computeResourceLayout,
            Resources = [inputTexture, outputTexture]
        });

        // Compile compute shader
        using Shader computeShader = App.Context.LoadShaderFromSource(computeShaderSource, "CSMain", ShaderStageFlags.Compute);

        // Create compute pipeline
        computePipeline = App.Context.CreateComputePipeline(new()
        {
            Compute = computeShader,
            ResourceLayouts = [computeResourceLayout],
            ThreadGroupSizeX = ThreadGroupSize,
            ThreadGroupSizeY = ThreadGroupSize,
            ThreadGroupSizeZ = 1
        });

        // Create display resources (fullscreen quad)
        Vertex[] vertices =
        [
            new(new(-1.0f,  1.0f, 0.0f), new(0.0f, 0.0f)),
            new(new( 1.0f,  1.0f, 0.0f), new(1.0f, 0.0f)),
            new(new( 1.0f, -1.0f, 0.0f), new(1.0f, 1.0f)),
            new(new(-1.0f, -1.0f, 0.0f), new(0.0f, 1.0f)),
        ];

        uint[] indices = [0, 1, 2, 0, 2, 3];

        vertexBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(Marshal.SizeOf<Vertex>() * vertices.Length),
            StrideInBytes = (uint)Marshal.SizeOf<Vertex>(),
            Flags = BufferUsageFlags.Vertex | BufferUsageFlags.MapWrite
        });
        vertexBuffer.Upload(vertices, 0);

        indexBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(uint) * indices.Length),
            StrideInBytes = sizeof(uint),
            Flags = BufferUsageFlags.Index | BufferUsageFlags.MapWrite
        });
        indexBuffer.Upload(indices, 0);

        sampler = App.Context.CreateSampler(new()
        {
            Filter = Filter.MinLinearMagLinearMipLinear,
            U = AddressMode.Clamp,
            V = AddressMode.Clamp,
            W = AddressMode.Clamp,
            MaxLod = uint.MaxValue
        });

        displayResourceLayout = App.Context.CreateResourceLayout(new()
        {
            Bindings = BindingHelper.Bindings
            (
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Sampler, Count = 1, StageFlags = ShaderStageFlags.Pixel }
            )
        });

        displayResourceSet = App.Context.CreateResourceSet(new()
        {
            Layout = displayResourceLayout,
            Resources = [outputTexture, sampler]
        });

        InputLayout inputLayout = new();
        inputLayout.Add(new() { Format = ElementFormat.Float3, Semantic = ElementSemantic.Position });
        inputLayout.Add(new() { Format = ElementFormat.Float2, Semantic = ElementSemantic.TexCoord });

        using Shader vertexShader = App.Context.LoadShaderFromSource(displayShaderSource, "VSMain", ShaderStageFlags.Vertex);
        using Shader pixelShader = App.Context.LoadShaderFromSource(displayShaderSource, "PSMain", ShaderStageFlags.Pixel);

        displayPipeline = App.Context.CreateGraphicsPipeline(new()
        {
            RenderStates = new()
            {
                RasterizerState = RasterizerStates.CullNone,
                DepthStencilState = DepthStencilStates.None,
                BlendState = BlendStates.Opaque
            },
            Vertex = vertexShader,
            Pixel = pixelShader,
            ResourceLayouts = [displayResourceLayout],
            InputLayouts = [inputLayout],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            Output = App.SwapChain.FrameBuffer.Output
        });
    }

    public void Update(double deltaTime)
    {
    }

    public void Render()
    {
        CommandBuffer commandBuffer = App.Context.Graphics.CommandBuffer();

        // Run compute shader once to process the image
        if (!processed)
        {
            uint dispatchX = (inputTexture.Desc.Width + ThreadGroupSize - 1) / ThreadGroupSize;
            uint dispatchY = (inputTexture.Desc.Height + ThreadGroupSize - 1) / ThreadGroupSize;

            commandBuffer.SetPipeline(computePipeline);
            commandBuffer.SetResourceSet(computeResourceSet, 0);
            commandBuffer.Dispatch(dispatchX, dispatchY, 1);

            processed = true;
        }

        // Display the processed texture
        commandBuffer.BeginRenderPass(App.SwapChain.FrameBuffer, new()
        {
            ColorValues = [new(0.0f, 0.0f, 0.0f, 1.0f)],
            Depth = 1.0f,
            Stencil = 0,
            Flags = ClearFlags.All
        }, displayResourceSet);

        commandBuffer.SetPipeline(displayPipeline);
        commandBuffer.SetResourceSet(displayResourceSet, 0);
        commandBuffer.SetVertexBuffer(vertexBuffer, 0, 0);
        commandBuffer.SetIndexBuffer(indexBuffer, 0, IndexFormat.UInt32);
        commandBuffer.DrawIndexed(6, 1, 0, 0, 0);

        commandBuffer.EndRenderPass();

        commandBuffer.Submit(waitForCompletion: true);
    }

    public void Resize(uint width, uint height)
    {
    }

    public void Dispose()
    {
        displayPipeline.Dispose();
        displayResourceSet.Dispose();
        displayResourceLayout.Dispose();
        sampler.Dispose();
        indexBuffer.Dispose();
        vertexBuffer.Dispose();

        computePipeline.Dispose();
        computeResourceSet.Dispose();
        computeResourceLayout.Dispose();
        outputTexture.Dispose();
        inputTexture.Dispose();
    }
}
```

## Running the Tutorial

Update your `Program.cs`:

```csharp
using ZenithTutorials;
using ZenithTutorials.Renderers;

App.Run<ComputeShaderRenderer>();

App.Cleanup();
```

Run the application:

```bash
dotnet run
```

## Result

The original color image is converted to grayscale using the GPU compute shader:

![compute-shader](../../images/compute-shader.png)

## Code Breakdown

### Compute Shader

```hlsl
Texture2D inputTexture;
RWTexture2D outputTexture;

[numthreads(16, 16, 1)]
void CSMain(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    // Bounds check
    if (dispatchThreadID.x >= width || dispatchThreadID.y >= height)
    {
        return;
    }

    // Read, process, write
    float4 color = inputTexture[dispatchThreadID.xy];
    float gray = dot(color.rgb, float3(0.299, 0.587, 0.114));
    outputTexture[dispatchThreadID.xy] = float4(gray, gray, gray, color.a);
}
```

Key elements:

| Element | Description |
|---------|-------------|
| `Texture2D` | Read-only input texture |
| `RWTexture2D` | Read/write output texture |
| `[numthreads(16, 16, 1)]` | Thread group size (16×16 threads) |
| `SV_DispatchThreadID` | Global thread index across all groups |

### Output Texture Creation

```csharp
outputTexture = App.Context.CreateTexture(new()
{
    Type = TextureType.Texture2D,
    Format = PixelFormat.R8G8B8A8UNorm,
    Width = inputTexture.Desc.Width,
    Height = inputTexture.Desc.Height,
    Depth = 1,
    MipLevels = 1,
    ArrayLayers = 1,
    SampleCount = SampleCount.Count1,
    Flags = TextureUsageFlags.ShaderResource | TextureUsageFlags.UnorderedAccess
});
```

`TextureUsageFlags.UnorderedAccess` is required for textures that will be written to in compute shaders.

### Compute Resource Layout

```csharp
computeResourceLayout = App.Context.CreateResourceLayout(new()
{
    Bindings = BindingHelper.Bindings
    (
        new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
        new() { Type = ResourceType.TextureReadWrite, Count = 1, StageFlags = ShaderStageFlags.Compute }
    )
});
```

Note the differences from graphics shaders:
- `ShaderStageFlags.Compute` instead of `Vertex` or `Pixel`
- `ResourceType.TextureReadWrite` for writable textures

### Compute Pipeline Creation

```csharp
computePipeline = App.Context.CreateComputePipeline(new()
{
    Compute = computeShader,
    ResourceLayouts = [computeResourceLayout],
    ThreadGroupSizeX = ThreadGroupSize,
    ThreadGroupSizeY = ThreadGroupSize,
    ThreadGroupSizeZ = 1
});
```

The `ComputePipelineDesc` requires:
- `Compute` - The compiled compute shader
- `ResourceLayouts` - Resource bindings (same as graphics pipelines)
- `ThreadGroupSizeX/Y/Z` - Must match `[numthreads()]` in the shader

### Dispatching Compute Work

```csharp
uint dispatchX = (inputTexture.Desc.Width + ThreadGroupSize - 1) / ThreadGroupSize;
uint dispatchY = (inputTexture.Desc.Height + ThreadGroupSize - 1) / ThreadGroupSize;

commandBuffer.SetPipeline(computePipeline);
commandBuffer.SetResourceSet(computeResourceSet, 0);
commandBuffer.Dispatch(dispatchX, dispatchY, 1);
```

The `Dispatch` call executes the compute shader:
- `dispatchX` × `dispatchY` × `dispatchZ` = total thread groups
- Each group runs `ThreadGroupSize` × `ThreadGroupSize` × 1 threads
- The formula `(size + groupSize - 1) / groupSize` ensures full coverage

### Compute vs Graphics Pipeline

| Aspect | Graphics Pipeline | Compute Pipeline |
|--------|-------------------|------------------|
| Shader stages | Vertex, Pixel, etc. | Compute only |
| Output | FrameBuffer | Writable textures/buffers |
| Execution | `Draw`/`DrawIndexed` | `Dispatch` |
| Requires render pass | Yes | No |

## Next Steps

Now that you understand compute shaders, explore more advanced topics:

- [Indirect Drawing](indirect-drawing.md) - GPU-driven rendering with DrawIndirect
- [Ray Tracing](../advanced/ray-tracing.md) - Hardware-accelerated ray tracing

## Source Code

> [!TIP]
> View the complete source code on GitHub: [ComputeShaderRenderer.cs](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/ComputeShaderRenderer.cs)