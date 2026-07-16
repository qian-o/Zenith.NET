# Project Setup

Create the shared desktop host used by every tutorial. It selects a graphics API, owns the window and swap chain, and passes each renderer a command buffer and the current drawable.

## Development Environment

- .NET 10 SDK or later
- A platform and GPU supported by one Zenith.NET graphics API
- A current GPU driver

## Get the Project

Clone Zenith.NET and the tutorial repository into the same parent directory. The tutorial project currently references the local Zenith.NET projects:

```bash
git clone https://github.com/qian-o/Zenith.NET.git
git clone https://github.com/qian-o/ZenithTutorials.git
cd ZenithTutorials/ZenithTutorials
```

`Silk.NET.Windowing` supplies the cross-platform desktop window. The tutorial project references the DirectX 12, Metal, Vulkan, and ImageSharp projects from the adjacent Zenith.NET checkout.

## Project Structure

```text
ZenithTutorials/
|-- Program.cs
|-- App.cs
|-- CocoaHelper.cs
|-- IRenderer.cs
|-- ScreenCapture.cs
|-- Usings.cs
|-- Assets/
|   |-- Textures/
|   |   `-- shoko.png
|   `-- Shaders/
`-- Renderers/
    `-- ClearRenderer.cs
```

The tutorial image is maintained with the runnable project:

![shoko.png](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Textures/shoko.png)

## Build and Run

```bash
dotnet build
dotnet run
```

Choose **Project Setup** from the console menu. A clear-only frame confirms that the host is connected.

## Frame Ownership

The host acquires the current swap-chain drawable and creates one graphics command buffer per frame. The renderer records into that borrowed command buffer and returns it in `ColorAttachment`; it does not submit, wait, or retain the command buffer. The host transitions the drawable to `Present`, submits, waits, and presents.

Each renderer owns its workload resources and responds to drawable-size changes through `Resize`. The host owns the window, context, swap chain, and frame lifecycle.

## Source

### Project

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/ZenithTutorials.csproj" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/ZenithTutorials.csproj" data-language="xml"></div>

### Application Host

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/App.cs" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/App.cs" data-language="csharp"></div>

### Renderer Contract

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/IRenderer.cs" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/IRenderer.cs" data-language="csharp"></div>

### Clear Renderer

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/ClearRenderer.cs" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/ClearRenderer.cs" data-language="csharp"></div>

The repository also contains the [entry point](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Program.cs), [Apple layer helper](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/CocoaHelper.cs), and [shared usings](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Usings.cs).