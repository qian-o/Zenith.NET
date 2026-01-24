# Compute Shader

In this tutorial, you'll learn how to use compute shaders with Zenith.NET. We'll create a simple image processing effect that converts a color image to grayscale on the GPU.

## Overview

We'll create a `ComputeShaderRenderer` class that:

- Loads an image as an input texture
- Creates an output texture with read/write access
- Builds a compute pipeline
- Dispatches compute work to process the image

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

    private const string ComputeShaderSource = """
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
    private const string DisplayShaderSource = """
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
        inputTexture = App.Context.LoadTextureFromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "shoko.png"), generateMipMaps: false);

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

        computeResourceLayout = App.Context.CreateResourceLayout(new()
        {
            Bindings = BindingHelper.Bindings
            (
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.TextureReadWrite, Count = 1, StageFlags = ShaderStageFlags.Compute }
            )
        });

        computeResourceSet = App.Context.CreateResourceSet(new()
        {
            Layout = computeResourceLayout,
            Resources = [inputTexture, outputTexture]
        });

        using Shader computeShader = App.Context.LoadShaderFromSource(ComputeShaderSource, "CSMain", ShaderStageFlags.Compute);

        computePipeline = App.Context.CreateComputePipeline(new()
        {
            Compute = computeShader,
            ResourceLayouts = [computeResourceLayout],
            ThreadGroupSizeX = ThreadGroupSize,
            ThreadGroupSizeY = ThreadGroupSize,
            ThreadGroupSizeZ = 1
        });

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
            U = AddressMode.Clamp,
            V = AddressMode.Clamp,
            W = AddressMode.Clamp,
            Filter = Filter.MinLinearMagLinearMipLinear,
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

        using Shader vertexShader = App.Context.LoadShaderFromSource(DisplayShaderSource, "VSMain", ShaderStageFlags.Vertex);
        using Shader pixelShader = App.Context.LoadShaderFromSource(DisplayShaderSource, "PSMain", ShaderStageFlags.Pixel);

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

        if (!processed)
        {
            uint dispatchX = (inputTexture.Desc.Width + ThreadGroupSize - 1) / ThreadGroupSize;
            uint dispatchY = (inputTexture.Desc.Height + ThreadGroupSize - 1) / ThreadGroupSize;

            commandBuffer.SetPipeline(computePipeline);
            commandBuffer.SetResourceSet(computeResourceSet, 0);
            commandBuffer.Dispatch(dispatchX, dispatchY, 1);

            processed = true;
        }

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

/// <summary>
/// Vertex structure with position and texture coordinates.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
file struct Vertex(Vector3 position, Vector2 texCoord)
{
    public Vector3 Position = position;

    public Vector2 TexCoord = texCoord;
}
```

## Running the Tutorial

Update your `Program.cs` to run the `ComputeShaderRenderer`:

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

![compute-shader](../../images/compute-shader.png)

## Code Breakdown

### Compute Shader

```slang
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

## Next Steps

- [Indirect Drawing](indirect-drawing.md) - Let the GPU control draw parameters for efficient multi-instance rendering

## Source Code

> [!TIP]
> View the complete source code on GitHub: [ComputeShaderRenderer.cs](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/ComputeShaderRenderer.cs)