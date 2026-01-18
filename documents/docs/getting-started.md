# Getting Started

This guide will help you create a simple Hello Triangle application using Zenith.NET with Silk.NET windowing.

> **Note**: You can also use other supported UI frameworks (Avalonia, MAUI, WinForms, WinUI, WPF) or any window library that can provide a native window handle. However, we recommend using Silk.NET for its simplicity and cross-platform support.

## Prerequisites

- .NET 10 or later
- A GPU that supports DirectX 12, Metal, or Vulkan

## Create a New Project

```bash
dotnet new console -n HelloTriangle
cd HelloTriangle
```

## Install Packages

```bash
# Core library
dotnet add package Zenith.NET

# Backend (choose one based on your platform)
dotnet add package Zenith.NET.DirectX12  # Windows
dotnet add package Zenith.NET.Vulkan     # Windows, Linux, Android
dotnet add package Zenith.NET.Metal      # macOS, iOS

# Shader compiler
dotnet add package Zenith.NET.Extensions.Slang

# Windowing
dotnet add package Silk.NET.Windowing
dotnet add package Silk.NET.Input
```

## Project Structure

```
HelloTriangle/
├── Program.cs
└── Shaders/
    └── Triangle.slang
```

## Write the Shader

Create `Shaders/Triangle.slang`:

```hlsl
struct VSInput
{
    float3 Position : POSITION;
    float4 Color : COLOR;
};

struct VSOutput
{
    float4 Position : SV_Position;
    float4 Color : COLOR;
};

[shader("vertex")]
VSOutput VSMain(VSInput input)
{
    VSOutput output;
    output.Position = float4(input.Position, 1.0);
    output.Color = input.Color;
    return output;
}

[shader("pixel")]
float4 PSMain(VSOutput input) : SV_Target
{
    return input.Color;
}
```

## Write the Application

Replace `Program.cs` with the following code:

```csharp
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Windowing;
using Zenith.NET;
using Zenith.NET.DirectX12;
using Zenith.NET.Extensions.Slang;
using Zenith.NET.Metal;
using Zenith.NET.Vulkan;
using Buffer = Zenith.NET.Buffer;

// Create window
IWindow window = Window.Create(WindowOptions.Default with
{
    Title = "Hello Triangle - Zenith.NET",
    Size = new(1280, 720),
    API = GraphicsAPI.None  // We manage graphics ourselves
});

// Graphics resources (initialized in window.Load)
GraphicsContext context = null!;
SwapChain swapChain = null!;
Buffer vertexBuffer = null!;
GraphicsPipeline pipeline = null!;

window.Load += () =>
{
    // Create graphics context
    if (OperatingSystem.IsWindows())
    {
        context = GraphicsContext.CreateDirectX12(useValidationLayer: true);
    }
    else if (OperatingSystem.IsLinux())
    {
        context = GraphicsContext.CreateVulkan(useValidationLayer: true);
    }
    else if (OperatingSystem.IsMacOS())
    {
        context = GraphicsContext.CreateMetal(useValidationLayer: true);
    }

    // Listen to validation messages
    context.ValidationMessage += (sender, args) =>
    {
        Console.WriteLine($"[{args.Source} - {args.Severity}] {args.Message}");
    };

    // Create swap chain
    Surface surface;
    if (OperatingSystem.IsWindows())
    {
        surface = Surface.Win32(window.Native!.Win32!.Value.Hwnd, (uint)window.Size.X, (uint)window.Size.Y);
    }
    else if (OperatingSystem.IsLinux())
    {
        surface = Surface.Xlib(window.Native!.X11!.Value.Display, (nint)window.Native.X11.Value.Window, (uint)window.Size.X, (uint)window.Size.Y);
    }
    else
    {
        throw new PlatformNotSupportedException();
    }

    swapChain = context.CreateSwapChain(new()
    {
        Surface = surface,
        ColorTargetFormat = PixelFormat.R8G8B8A8UNorm,
        DepthStencilTargetFormat = PixelFormat.D24UNormS8UInt
    });

    // Create vertex buffer with triangle data
    // Note: Vertices are in counter-clockwise order (front face)
    Vertex[] vertices =
    [
        new(new( 0.0f,  0.5f, 0.0f), new(1.0f, 0.0f, 0.0f, 1.0f)),  // Top - Red
        new(new(-0.5f, -0.5f, 0.0f), new(0.0f, 1.0f, 0.0f, 1.0f)),  // Left - Green
        new(new( 0.5f, -0.5f, 0.0f), new(0.0f, 0.0f, 1.0f, 1.0f))   // Right - Blue
    ];

    vertexBuffer = context.CreateBuffer(new()
    {
        SizeInBytes = (uint)(Marshal.SizeOf<Vertex>() * vertices.Length),
        StrideInBytes = (uint)Marshal.SizeOf<Vertex>(),
        Flags = BufferUsageFlags.Vertex
    });
    vertexBuffer.Upload(vertices, 0);

    // Load shaders using Slang
    string shaderPath = Path.Combine(AppContext.BaseDirectory, "Shaders", "Triangle.slang");

    using Shader vertexShader = context.LoadShaderFromFile(shaderPath, "VSMain", ShaderStageFlags.Vertex);
    using Shader pixelShader = context.LoadShaderFromFile(shaderPath, "PSMain", ShaderStageFlags.Pixel);

    // Create graphics pipeline
    pipeline = context.CreateGraphicsPipeline(new()
    {
        RenderStates = new()
        {
            RasterizerState = RasterizerStates.CullBack,
            DepthStencilState = DepthStencilStates.None,
            BlendState = BlendStates.Opaque
        },
        Vertex = vertexShader,
        Pixel = pixelShader,
        ResourceLayouts = [],
        InputLayouts = [Vertex.GetInputLayout()],
        PrimitiveTopology = PrimitiveTopology.TriangleList,
        Output = swapChain.FrameBuffer.Output
    });
};

window.Render += delta =>
{
    if (window.Size.X == 0 || window.Size.Y == 0)
    {
        return;
    }

    // Get command buffer
    CommandBuffer cmd = context.Graphics.CommandBuffer();

    // Begin render pass with clear color
    cmd.BeginRenderPass(swapChain.FrameBuffer, new ClearValue
    {
        ColorValues = [new(0.1f, 0.1f, 0.1f, 1.0f)],
        Depth = 1.0f,
        Stencil = 0,
        Flags = ClearFlags.Color | ClearFlags.Depth | ClearFlags.Stencil
    });

    // Set pipeline and draw
    cmd.SetPipeline(pipeline);
    cmd.SetVertexBuffer(vertexBuffer, 0, 0);
    cmd.Draw(vertexCount: 3, instanceCount: 1, firstVertex: 0, firstInstance: 0);

    // End render pass
    cmd.EndRenderPass();

    // Submit and wait
    cmd.Submit(waitForCompletion: true);

    // Present
    swapChain.Present();
};

window.Resize += size =>
{
    if (size.X == 0 || size.Y == 0)
    {
        return;
    }

    swapChain.Resize((uint)size.X, (uint)size.Y);
};

window.Closing += () =>
{
    // Wait for GPU to finish
    context.Graphics.WaitIdle();

    // Dispose resources in reverse order
    pipeline.Dispose();
    vertexBuffer.Dispose();
    swapChain.Dispose();
    context.Dispose();
};

// Center and run
window.Initialize();
window.Center();
window.Run();

// Define vertex structure
[StructLayout(LayoutKind.Sequential)]
struct Vertex(Vector3 position, Vector4 color)
{
    public Vector3 Position = position;

    public Vector4 Color = color;

    public static InputLayout GetInputLayout()
    {
        InputLayout layout = new();
        layout.Add(new() { Format = ElementFormat.Float3, Semantic = ElementSemantic.Position });
        layout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Color });

        return layout;
    }
}
```

## Build and Run

Make sure to copy the shader file to the output directory. Add this to your `.csproj`:

```xml
<ItemGroup>
    <None Update="Shaders\**\*">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
</ItemGroup>
```

Then build and run:

```bash
dotnet run
```

You should see a window with a colorful triangle!

## Next Steps

Check out the [SponzaScene](https://github.com/qian-o/Zenith.NET/tree/master/sources/Experiments/SponzaScene) example for a complete deferred rendering pipeline.