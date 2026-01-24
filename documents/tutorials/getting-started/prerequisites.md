# Prerequisites

This guide covers the environment setup required before working with Zenith.NET.

## System Requirements

### Hardware

Zenith.NET supports multiple graphics backends across platforms:

| Platform | DirectX 12 | Metal 4 | Vulkan 1.4 |
|----------|:----------:|:-------:|:----------:|
| Windows  | ✅ | - | ✅ |
| Linux    | - | - | ✅ |
| Android  | - | - | ✅ |
| macOS    | - | ✅ | ✅ |
| iOS      | - | ✅ | ✅ |

> [!NOTE]
> Metal backend is currently under development.

### Software

- **.NET SDK**: 10.0 or later
- **IDE**: Visual Studio 2026, VS Code, or JetBrains Rider

## Building the Tutorials

The example code in these tutorials is designed to be extensible. We'll create a base project structure that all tutorials will share.

### Creating the Project

```bash
dotnet new console -n ZenithTutorials
cd ZenithTutorials
```

### Required Packages

Install the following NuGet packages:

```bash
dotnet add package Zenith.NET.DirectX12
dotnet add package Zenith.NET.Metal
dotnet add package Zenith.NET.Vulkan
dotnet add package Zenith.NET.Extensions.Slang
dotnet add package Silk.NET.Windowing
dotnet add package Silk.NET.Input
```

### Project Configuration

Update your `.csproj` file:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Zenith.NET.DirectX12" Version="*" />
    <PackageReference Include="Zenith.NET.Metal" Version="*" />
    <PackageReference Include="Zenith.NET.Vulkan" Version="*" />
    <PackageReference Include="Zenith.NET.Extensions.Slang" Version="*" />
    <PackageReference Include="Silk.NET.Windowing" Version="*" />
    <PackageReference Include="Silk.NET.Input" Version="*" />
  </ItemGroup>

</Project>
```

> [!NOTE]
> `AllowUnsafeBlocks` is required for using `sizeof()` with custom structs.

## Project Structure

Organize your project with the following directory structure:

```
ZenithTutorials/
├── Program.cs         # Application entry point
├── App.cs             # Application framework
├── IRenderer.cs       # Renderer interface
├── BindingHelper.cs   # Cross-platform resource binding helper
├── Usings.cs          # Global using statements
└── Renderers/         # All tutorial renderers
```

## Global Usings

Create `Usings.cs` for shared using statements across all files:

```csharp
global using System.Numerics;
global using System.Runtime.InteropServices;
global using Zenith.NET;
global using Zenith.NET.Extensions.ImageSharp;
global using Zenith.NET.Extensions.Slang;
global using Buffer = Zenith.NET.Buffer;
```

This eliminates repetitive using statements in each renderer file.

## Renderer Interface

All tutorial renderers implement a common interface. Create `IRenderer.cs`:

```csharp
namespace ZenithTutorials;

internal interface IRenderer : IDisposable
{
    void Update(double deltaTime);

    void Render();

    void Resize(uint width, uint height);
}
```

This interface ensures all renderers follow a consistent pattern:

- `Update` - Called each frame for logic updates (animations, input handling)
- `Render` - Called each frame to record and submit draw commands
- `Resize` - Called when the window size changes
- `Dispose` - Cleanup GPU resources

## Binding Helper

Different graphics backends use different indexing schemes for resource bindings:

| Backend | Index Scheme |
|---------|--------------|
| DirectX 12 | Per-type: CBV, SRV, UAV, Sampler each start at 0 |
| Vulkan | Global: All resources share index space (0, 1, 2, ...) |
| Metal | Per-category: Buffer, Texture, Sampler each start at 0 |

Create `BindingHelper.cs` to handle these differences automatically:

```csharp
using Zenith.NET;

namespace ZenithTutorials;

internal static class BindingHelper
{
    public static ResourceBinding[] Bindings(params ResourceBinding[] bindings)
    {
        switch (App.Context.Backend)
        {
            case Backend.DirectX12:
                {
                    uint cbvIndex = 0;
                    uint srvIndex = 0;
                    uint uavIndex = 0;
                    uint samplerIndex = 0;

                    for (int i = 0; i < bindings.Length; i++)
                    {
                        ref ResourceBinding binding = ref bindings[i];

                        binding = binding with
                        {
                            Index = binding.Type switch
                            {
                                ResourceType.ConstantBuffer => cbvIndex++,

                                ResourceType.StructuredBuffer or
                                ResourceType.Texture or
                                ResourceType.AccelerationStructure => srvIndex++,

                                ResourceType.StructuredBufferReadWrite or
                                ResourceType.TextureReadWrite => uavIndex++,

                                ResourceType.Sampler => samplerIndex++,

                                _ => binding.Index
                            }
                        };
                    }
                }
                break;

            case Backend.Vulkan:
                {
                    for (int i = 0; i < bindings.Length; i++)
                    {
                        ref ResourceBinding binding = ref bindings[i];

                        binding = binding with { Index = (uint)i };
                    }
                }
                break;

            case Backend.Metal:
                {
                    uint bufferIndex = 0;
                    uint textureIndex = 0;
                    uint samplerIndex = 0;

                    for (int i = 0; i < bindings.Length; i++)
                    {
                        ref ResourceBinding binding = ref bindings[i];

                        binding = binding with
                        {
                            Index = binding.Type switch
                            {
                                ResourceType.ConstantBuffer or
                                ResourceType.StructuredBuffer or
                                ResourceType.StructuredBufferReadWrite => bufferIndex++,

                                ResourceType.Texture or
                                ResourceType.TextureReadWrite => textureIndex++,

                                ResourceType.Sampler => samplerIndex++,

                                _ => binding.Index
                            }
                        };
                    }
                }
                break;
        }

        return bindings;
    }
}
```

Usage example:

```csharp
resourceLayout = App.Context.CreateResourceLayout(new()
{
    Bindings = BindingHelper.Bindings
    (
        new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Pixel },
        new() { Type = ResourceType.Sampler, Count = 1, StageFlags = ShaderStageFlags.Pixel }
    )
});
```

The helper automatically assigns the correct `Index` values based on the current backend, so you don't need to specify them manually.

## Application Framework

All tutorials share a common application framework that handles window creation, graphics context initialization, and the main loop. This is split into two files for clarity.

### App.cs

Create `App.cs` as the reusable application framework:

```csharp
using System;
using Silk.NET.Windowing;
using Zenith.NET;
using Zenith.NET.DirectX12;
using Zenith.NET.Metal;
using Zenith.NET.Vulkan;

namespace ZenithTutorials;

internal static class App
{
    private static readonly IWindow window;

    static App()
    {
        // Create window with no graphics API (we manage rendering ourselves)
        window = Window.Create(WindowOptions.Default with
        {
            API = GraphicsAPI.None,
            Title = "Zenith.NET Tutorial",
            Size = new(1280, 720)
        });

        window.Initialize();

        // Select graphics backend based on platform
        if (OperatingSystem.IsWindows())
        {
            Context = GraphicsContext.CreateDirectX12(useValidationLayer: true);
        }
        else if (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS())
        {
            Context = GraphicsContext.CreateMetal(useValidationLayer: true);
        }
        else
        {
            Context = GraphicsContext.CreateVulkan(useValidationLayer: true);
        }

        // Log validation messages for debugging
        Context.ValidationMessage += (sender, args) =>
        {
            Console.WriteLine($"[{args.Source} - {args.Severity}] {args.Message}");
        };

        // Create platform-specific surface for rendering
        Surface surface;
        if (OperatingSystem.IsWindows())
        {
            surface = Surface.Win32(window.Native!.Win32!.Value.Hwnd, Width, Height);
        }
        else if (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS())
        {
            throw new NotImplementedException("TODO: Get CAMetalLayer from Silk.NET.Windowing");
        }
        else if (OperatingSystem.IsLinux())
        {
            surface = Surface.Xlib(window.Native!.X11!.Value.Display, (nint)window.Native.X11.Value.Window, Width, Height);
        }
        else
        {
            throw new NotImplementedException();
        }

        // Create swap chain for double-buffered rendering
        SwapChain = Context.CreateSwapChain(new()
        {
            Surface = surface,
            ColorTargetFormat = PixelFormat.R8G8B8A8UNorm,
            DepthStencilTargetFormat = PixelFormat.D24UNormS8UInt
        });
    }

    public static GraphicsContext Context { get; }

    public static SwapChain SwapChain { get; }

    public static uint Width => (uint)window.Size.X;

    public static uint Height => (uint)window.Size.Y;

    public static void Run<TRenderer>() where TRenderer : IRenderer, new()
    {
        using TRenderer renderer = new();

        window.Update += renderer.Update;

        window.Render += delta =>
        {
            // Skip rendering when window is minimized
            if (Width <= 0 || Height <= 0)
            {
                return;
            }

            renderer.Render();
            SwapChain.Present();
        };

        window.Resize += size =>
        {
            if (Width <= 0 || Height <= 0)
            {
                return;
            }

            // Notify renderer first, then resize swap chain
            renderer.Resize(Width, Height);
            SwapChain.Resize(Width, Height);
        };

        window.Run();
    }

    public static void Cleanup()
    {
        SwapChain.Dispose();
        Context.Dispose();
        window.Dispose();
    }
}
```

### Program.cs

Create `Program.cs` as the simple entry point:

```csharp
using ZenithTutorials;
using ZenithTutorials.Renderers;

App.Run<HelloTriangleRenderer>();

App.Cleanup();
```

> [!NOTE]
> `HelloTriangleRenderer` will be created in the [next tutorial](hello-triangle.md).

This framework provides:

- **Window creation** with Silk.NET (1280×720 default size)
- **Cross-platform backend selection** (DirectX 12 on Windows, Metal on Apple platforms, Vulkan elsewhere)
- **SwapChain management** for presenting frames
- **Resize handling** for responsive rendering
- **Generic renderer pattern** using `App.Run<TRenderer>()` for easy tutorial switching
- **Static access** to `App.Context` and `App.SwapChain` from renderers

## Verify Installation

Before continuing, verify your setup compiles correctly:

```bash
dotnet build
```

If the build succeeds, you're ready to start [Hello Triangle](hello-triangle.md)!

## Source Code

> [!TIP]
> The complete source code for all tutorials is available on GitHub: [ZenithTutorials](https://github.com/qian-o/ZenithTutorials)
