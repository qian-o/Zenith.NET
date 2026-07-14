# Prerequisites

This page creates the shared desktop application used by every tutorial. The application selects one Graphics API, creates a native surface and swap chain, drives a synchronous frame loop, and gives each renderer a command buffer plus the current drawable.

## Development Environment

- .NET 10 SDK or later
- DirectX 12 on Windows, Metal 4 on macOS, or Vulkan 1.4 with Zenith.NET's required bindless extensions on Linux
- A compatible GPU and current driver
- Visual Studio, VS Code, or JetBrains Rider

The Linux path below uses Xlib, matching the repository's current desktop experiment.

## Create the Project

```bash
dotnet new console -n ZenithTutorials
cd ZenithTutorials

dotnet add package Zenith.NET.DirectX12
dotnet add package Zenith.NET.Metal
dotnet add package Zenith.NET.Vulkan
dotnet add package Zenith.NET.Extensions.ImageSharp
dotnet add package Silk.NET.Windowing
```

Slang compilation is provided by `ZenithCompiler` in the core `Zenith.NET` package.

Enable unsafe code and copy tutorial assets to the output directory:

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
</PropertyGroup>

<ItemGroup>
  <None Update="Assets\**\*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

`AllowUnsafeBlocks` is used for explicit unmanaged GPU data and pointer-based upload descriptions.

## Project Structure

```text
ZenithTutorials/
|-- Program.cs
|-- App.cs
|-- CocoaHelper.cs
|-- IRenderer.cs
|-- Usings.cs
|-- Assets/
|   |-- Textures/
|   |   `-- shoko.png
|   `-- Shaders/
`-- Renderers/
    `-- ClearRenderer.cs
```

Save the tutorial image as `Assets/Textures/shoko.png`:

![shoko.png](../../images/shoko.png)

## Global Usings

Create `Usings.cs`:

```csharp
global using System.Numerics;
global using System.Runtime.InteropServices;
global using Zenith.NET;
global using Zenith.NET.Extensions.ImageSharp;
global using Buffer = Zenith.NET.Buffer;
```

## Renderer Contract

Create `IRenderer.cs`:

```csharp
namespace ZenithTutorials;

internal interface IRenderer : IDisposable
{
    void Update(double deltaTime);

    void Render(CommandBuffer commandBuffer, Texture drawable);

    void Resize(uint width, uint height);
}
```

The application owns command-buffer acquisition, final transition to `Present`, submission, and presentation. A renderer records its workload into the supplied command buffer and transitions the drawable into the role it needs.

## Application Shell

Create `App.cs`:

```csharp
using Silk.NET.Windowing;
using Zenith.NET.DirectX12;
using Zenith.NET.Metal;
using Zenith.NET.Vulkan;

namespace ZenithTutorials;

internal static class App
{
    private static readonly IWindow window;
    private static readonly SwapChain swapChain;

    static App()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("The tutorials support Windows, macOS, and Linux.");
        }

        if (OperatingSystem.IsWindows())
        {
            Context = GraphicsContext.CreateDirectX12(useValidationLayer: true);
        }
        else if (OperatingSystem.IsMacOS())
        {
            Context = GraphicsContext.CreateMetal(useValidationLayer: true);
        }
        else
        {
            Context = GraphicsContext.CreateVulkan(useValidationLayer: true);
        }

        Context.ValidationMessage += static (_, args) => Console.WriteLine($"[{args.Severity}] {args.Message}");

        window = Window.Create(WindowOptions.Default with
        {
            API = GraphicsAPI.None,
            Title = "Zenith.NET Tutorials",
            Size = new(1280, 720)
        });

        window.Initialize();
        window.Center();

        Surface surface;
        if (OperatingSystem.IsWindows())
        {
            surface = Surface.Win32(window.Native!.Win32!.Value.Hwnd, Width, Height);
        }
        else if (OperatingSystem.IsMacOS())
        {
            surface = Surface.Apple(CocoaHelper.CreateLayer(window.Native!.Cocoa!.Value), Width, Height);
        }
        else
        {
            surface = Surface.Xlib(window.Native!.X11!.Value.Display, (nint)window.Native.X11.Value.Window, Width, Height);
        }

        swapChain = Context.CreateSwapChain(new()
        {
            Surface = surface,
            Format = PixelFormat.B8G8R8A8UNorm
        });
    }

    public static GraphicsContext Context { get; }

    public static PixelFormat ColorFormat => swapChain.Desc.Format;

    public static uint Width => (uint)window.FramebufferSize.X;

    public static uint Height => (uint)window.FramebufferSize.Y;

    public static string ShaderPath(string file)
    {
        return Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", file);
    }

    public static void Run<TRenderer>() where TRenderer : IRenderer, new()
    {
        try
        {
            using TRenderer renderer = new();

            window.Update += delta =>
            {
                if (Width is 0 || Height is 0)
                {
                    return;
                }

                renderer.Update(delta);
            };

            window.Render += _ =>
            {
                if (Width is 0 || Height is 0)
                {
                    return;
                }

                Texture drawable = swapChain.Drawable;
                CommandBuffer commandBuffer = Context.GraphicsQueue.CommandBuffer();

                renderer.Render(commandBuffer, drawable);
                commandBuffer.Transition(drawable, default, TextureLayout.Present);
                commandBuffer.Submit().Wait();

                swapChain.Present();
            };

            window.Resize += _ =>
            {
                if (Width is 0 || Height is 0)
                {
                    return;
                }

                renderer.Resize(Width, Height);
                swapChain.Resize(Width, Height);
            };

            window.Run();
        }
        finally
        {
            swapChain.Dispose();
            window.Dispose();
            Context.Dispose();
        }
    }
}
```

The frame loop is intentionally synchronous. `Submit().Wait()` completes the recorded frame, and `SwapChain.Present()` performs its own queue timeline synchronization. Zenith.NET does not expose a frames-in-flight configuration.

## macOS Surface Helper

Create `CocoaHelper.cs`. It attaches a `CAMetalLayer` to the Silk.NET window:

```csharp
namespace ZenithTutorials;

internal static partial class CocoaHelper
{
    private const string LibObjC = "/usr/lib/libobjc.A.dylib";

    [LibraryImport(LibObjC, EntryPoint = "objc_getClass")]
    private static partial nint GetClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [LibraryImport(LibObjC, EntryPoint = "sel_registerName")]
    private static partial nint Selector([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static partial nint Send(nint receiver, nint selector);

    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static partial nint Send(nint receiver, nint selector, [MarshalAs(UnmanagedType.I1)] bool value);

    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static partial nint Send(nint receiver, nint selector, nint value);

    public static nint CreateLayer(nint cocoa)
    {
        nint layer = Send(GetClass("CAMetalLayer"), Selector("layer"));

        nint view = Send(cocoa, Selector("contentView"));
        Send(view, Selector("setWantsLayer:"), true);
        Send(view, Selector("setLayer:"), layer);

        return layer;
    }
}
```

The file is compiled on every desktop platform, but the helper is called only on macOS.

## First Renderer

Create `Renderers/ClearRenderer.cs`:

```csharp
namespace ZenithTutorials.Renderers;

internal sealed class ClearRenderer : IRenderer
{
    public void Update(double deltaTime)
    {
    }

    public void Render(CommandBuffer commandBuffer, Texture drawable)
    {
        commandBuffer.Transition(drawable, default, TextureLayout.ColorAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.Clear(drawable, new(0.04f, 0.055f, 0.075f, 1.0f))], null);
        commandBuffer.EndRenderPass();
    }

    public void Resize(uint width, uint height)
    {
    }

    public void Dispose()
    {
    }
}
```

The renderer changes the drawable from its current presentation state to `ColorAttachment`, clears it, and leaves the final transition to `App`.

## Entry Point

Replace `Program.cs`:

```csharp
using ZenithTutorials;
using ZenithTutorials.Renderers;

App.Run<ClearRenderer>();
```

Run the application:

```bash
dotnet run
```

You should see a dark blue-gray window. Validation output is written to the terminal.

## Frame Ownership

The shared frame follows this order:

1. `App` obtains the current `SwapChain.Drawable`.
2. `App` requests a pooled command buffer from `GraphicsQueue`.
3. The renderer records transitions, passes, draws, or dispatches.
4. `App` transitions the drawable to `TextureLayout.Present`.
5. The command buffer is submitted and its `TimelineValue` is waited on.
6. The swap chain presents synchronously.
7. At shutdown, the renderer is disposed before the swap chain and context.

Continue with [Hello Triangle](hello-triangle.md) to replace the clear-only renderer with a graphics pipeline and draw call.
