# Compute Shader

In this tutorial, you'll learn how to use compute shaders with Zenith.NET. We'll create a simple image processing effect that converts a color image to grayscale on the GPU.

## Overview

We'll create a `ComputeShaderRenderer` class that:

- Loads an image as an input texture
- Creates an output texture with read/write access
- Builds a compute pipeline
- Dispatches compute work to process the image
- Copies the result to the swap chain for display

## The Renderer Class

Create a new file `Renderers/ComputeShaderRenderer.cs`:

```csharp
namespace ZenithTutorials.Renderers;

internal unsafe class ComputeShaderRenderer : IRenderer
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

    private readonly Texture inputTexture;
    private readonly Texture outputTexture;
    private readonly ResourceLayout resourceLayout;
    private readonly ResourceTable resourceTable;
    private readonly ComputePipeline pipeline;

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

        resourceLayout = App.Context.CreateResourceLayout(new()
        {
            Bindings = BindingHelper.Bindings
            (
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Compute },
                new() { Type = ResourceType.TextureReadWrite, Count = 1, StageFlags = ShaderStageFlags.Compute }
            )
        });

        resourceTable = App.Context.CreateResourceTable(new()
        {
            Layout = resourceLayout,
            Resources = [inputTexture, outputTexture]
        });

        using Shader computeShader = App.Context.LoadShaderFromSource(ComputeShaderSource, "CSMain", ShaderStageFlags.Compute);

        pipeline = App.Context.CreateComputePipeline(new()
        {
            Compute = computeShader,
            ResourceLayout = resourceLayout,
            ThreadGroupSizeX = ThreadGroupSize,
            ThreadGroupSizeY = ThreadGroupSize,
            ThreadGroupSizeZ = 1
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

            commandBuffer.SetPipeline(pipeline);
            commandBuffer.SetResourceTable(resourceTable);
            commandBuffer.Dispatch(dispatchX, dispatchY, 1);

            processed = true;
        }

        // Copy the processed texture to the swap chain's color target (centered)
        Texture colorTarget = App.SwapChain.FrameBuffer.Desc.ColorAttachments[0].Target;

        // Clamp copy region to fit within both textures
        uint copyWidth = Math.Min(outputTexture.Desc.Width, App.Width);
        uint copyHeight = Math.Min(outputTexture.Desc.Height, App.Height);

        // Center the copy region
        uint srcX = (outputTexture.Desc.Width - copyWidth) / 2;
        uint srcY = (outputTexture.Desc.Height - copyHeight) / 2;
        uint destX = (App.Width - copyWidth) / 2;
        uint destY = (App.Height - copyHeight) / 2;

        commandBuffer.CopyTexture(outputTexture,
                                  default,
                                  new() { X = srcX, Y = srcY, Z = 0 },
                                  colorTarget,
                                  default,
                                  new() { X = destX, Y = destY, Z = 0 },
                                  new() { Width = copyWidth, Height = copyHeight, Depth = 1 });

        commandBuffer.Submit(waitForCompletion: true);
    }

    public void Resize(uint width, uint height)
    {
    }

    public void Dispose()
    {
        pipeline.Dispose();
        resourceTable.Dispose();
        resourceLayout.Dispose();
        outputTexture.Dispose();
        inputTexture.Dispose();
    }
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
    uint width, height;
    outputTexture.GetDimensions(width, height);

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
resourceLayout = App.Context.CreateResourceLayout(new()
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
pipeline = App.Context.CreateComputePipeline(new()
{
    Compute = computeShader,
    ResourceLayout = resourceLayout,
    ThreadGroupSizeX = ThreadGroupSize,
    ThreadGroupSizeY = ThreadGroupSize,
    ThreadGroupSizeZ = 1
});
```

The `ComputePipelineDesc` requires:
- `Compute` - The compiled compute shader
- `ResourceLayout` - Resource bindings (same as graphics pipelines)
- `ThreadGroupSizeX/Y/Z` - Must match `[numthreads()]` in the shader

### Dispatching Compute Work

```csharp
uint dispatchX = (inputTexture.Desc.Width + ThreadGroupSize - 1) / ThreadGroupSize;
uint dispatchY = (inputTexture.Desc.Height + ThreadGroupSize - 1) / ThreadGroupSize;

commandBuffer.SetPipeline(pipeline);
commandBuffer.SetResourceTable(resourceTable);
commandBuffer.Dispatch(dispatchX, dispatchY, 1);
```

The `Dispatch` call executes the compute shader:
- `dispatchX` × `dispatchY` × `dispatchZ` = total thread groups
- Each group runs `ThreadGroupSize` × `ThreadGroupSize` × 1 threads
- The formula `(size + groupSize - 1) / groupSize` ensures full coverage

### Copying to the Swap Chain

```csharp
Texture colorTarget = App.SwapChain.FrameBuffer.Desc.ColorAttachments[0].Target;

// Clamp copy region to fit within both textures
uint copyWidth = Math.Min(outputTexture.Desc.Width, App.Width);
uint copyHeight = Math.Min(outputTexture.Desc.Height, App.Height);

// Center the copy region
uint srcX = (outputTexture.Desc.Width - copyWidth) / 2;
uint srcY = (outputTexture.Desc.Height - copyHeight) / 2;
uint destX = (App.Width - copyWidth) / 2;
uint destY = (App.Height - copyHeight) / 2;

commandBuffer.CopyTexture(outputTexture,
                          default,
                          new() { X = srcX, Y = srcY, Z = 0 },
                          colorTarget,
                          default,
                          new() { X = destX, Y = destY, Z = 0 },
                          new() { Width = copyWidth, Height = copyHeight, Depth = 1 });
```

Instead of using a full-screen quad with a graphics pipeline, we directly copy the processed texture to the swap chain's color target:

- `App.SwapChain.FrameBuffer.Desc.ColorAttachments[0].Target` - Gets the swap chain's render target texture
- `CopyTexture` - Efficiently copies texture data on the GPU without needing shaders or render passes
- `copyWidth` / `copyHeight` - Clamps the copy region to fit within both source and destination textures
- `srcX` / `srcY` - Centers the source region when the texture is larger than the window
- `destX` / `destY` - Centers the destination region when the texture is smaller than the window

This approach is simpler and more efficient when you just need to display a texture without additional processing.

## Next Steps

- [Indirect Drawing](indirect-drawing.md) - Let the GPU control draw parameters for efficient multi-instance rendering

## Source Code

> [!TIP]
> View the complete source code on GitHub: [ComputeShaderRenderer.cs](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/ComputeShaderRenderer.cs)