# Project Setup

Do this once, then jump straight into [Hello Triangle](../rasterization/hello-triangle.md). Clone the tutorial repository, build it, and run it. The host application already provides the graphics context, the window, the frame loop, and the shared assets, so every tutorial focuses only on its own renderer.

## What You Need

- [Git](https://git-scm.com/downloads)
- The [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A GPU and driver that support one backend: DirectX 12 on Windows, Metal on Apple platforms, or Vulkan on Windows, Linux, or Android

## Clone and Run

Clone the repository and run it. The default renderer is Hello Triangle, so a triangle window confirms your setup works.

```bash
git clone https://github.com/qian-o/ZenithTutorials.git
cd ZenithTutorials
dotnet run --project ZenithTutorials
```

The project already targets .NET 10, enables unsafe code, references the Zenith.NET packages, and copies the shaders and textures to the output directory. The Slang compiler is restored with Zenith.NET, so there is no separate install step. If the window opens, you are ready to start [Hello Triangle](../rasterization/hello-triangle.md).

## Under the Hood (Optional)

The rest of this page is reference only. You never edit the host, and you can skip straight to Hello Triangle. Come back when you want to know where a renderer gets its graphics context and how a frame reaches the screen.

The host picks a backend from the current operating system and creates the graphics context. It turns on the validation layer, which reports incorrect API use to the console, and creates one sampler that several tutorials reuse.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/App.cs" data-source-region="initialize-graphics-context" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/App.cs" data-language="csharp"></div>

It exposes those shared values, the current size, and a helper that resolves shader paths from the output directory. Every renderer reads from here instead of creating its own context.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/App.cs" data-source-region="shared-services" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/App.cs" data-language="csharp"></div>

Common namespaces are declared once as global usings, so tutorial files stay focused on rendering. The last line makes `Buffer` mean the Zenith.NET buffer rather than `System.Buffer`.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Usings.cs" data-source-region="global-usings" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Usings.cs" data-language="csharp"></div>

### The Renderer Contract

Every tutorial implements this interface. The host drives it: `Update` advances animation, `Render` records commands into the frame's drawable, and `Resize` rebuilds size-dependent resources. `RequiredLayout` tells the host what state the drawable must be in before `Render` runs.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/IRenderer.cs" data-source-region="renderer-contract" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/IRenderer.cs" data-language="csharp"></div>

Running the host prints a numbered menu of every renderer. Type the number to launch that tutorial, so switching between tutorials needs no code changes.

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Program.cs" data-source-region="application-entry" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Program.cs" data-language="csharp"></div>

### The Shared Texture

Textured Quad and Image Processing both load this image. It is already in the repository under `Assets/Textures/shoko.png`, so there is nothing to download.

[![Tutorial texture](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Textures/shoko.png)](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Textures/shoko.png)

For the concepts behind the host, see [Runtime](../../docs/fundamentals/runtime.md) and [Commands](../../docs/fundamentals/commands.md).
