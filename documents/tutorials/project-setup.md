# Project Setup

Prepare the shared desktop host used by every tutorial. The host creates the window, graphics context, swap chain, and frame command buffer. Each tutorial supplies one renderer.

## Development Environment

- .NET 10 SDK
- Windows with DirectX 12, macOS with Metal, or Linux with Vulkan on X11/XWayland
- A compatible GPU and current driver

## Get the Project

Clone both repositories into the same parent directory. The tutorial project uses local project references to the adjacent Zenith.NET repository:

```bash
git clone https://github.com/qian-o/Zenith.NET.git
git clone https://github.com/qian-o/ZenithTutorials.git

cd ZenithTutorials
```

The project references the three graphics API packages and the ImageSharp extension from the adjacent repository. `Silk.NET.Windowing` provides the desktop window.

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

Later tutorials use this texture:

![shoko.png](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Textures/shoko.png)

## Build and Run

```bash
dotnet build ZenithTutorials.slnx
dotnet run --project ZenithTutorials/ZenithTutorials.csproj
```

Choose **Project Setup** from the console menu. A clear-only frame confirms that the host is connected.

## Renderer Contract

Every renderer implements `IRenderer`:

- `Update` changes CPU-side state for the next frame.
- `Render` records commands into the supplied command buffer.
- `Resize` recreates size-dependent resources.
- `Dispose` releases resources owned by the renderer.

`Render` borrows its `CommandBuffer` and drawable from the host. It records the drawable in `ColorAttachment` and returns without submitting, waiting, disposing, or retaining either object. The host completes and presents the frame.

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

The complete repository also contains the entry point, platform setup, screenshot support, and shared usings used by the host.
