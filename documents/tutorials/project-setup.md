# Project Setup

Create the shared desktop host used by every tutorial. It selects a graphics API, owns the window and swap chain, and passes each renderer a command buffer and the current drawable.

## Development Environment

- .NET 10 SDK or later
- A platform and GPU supported by one Zenith.NET graphics API
- A current GPU driver

## Create the Project

Create a console project and add the graphics API and ImageSharp packages used by the tutorials:

```bash
dotnet new console -n ZenithTutorials
cd ZenithTutorials

dotnet add package Zenith.NET.DirectX12
dotnet add package Zenith.NET.Metal
dotnet add package Zenith.NET.Vulkan
dotnet add package Zenith.NET.Extensions.ImageSharp
dotnet add package Silk.NET.Windowing
```

> [!NOTE]
> The tutorial repository temporarily uses local project references while the redesigned RHI is validated before publication. Those references will return to the Zenith.NET NuGet packages when the release is available; they are not part of the tutorial architecture.

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

Choose **Project Setup** from the console menu. A dark blue-gray window confirms that context creation, presentation, command recording, submission, and cleanup are connected.

## Source

### Project

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/ZenithTutorials.csproj" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/ZenithTutorials.csproj" data-language="xml"></div>

### Application Host

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/App.cs" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/App.cs" data-language="csharp"></div>

### Renderer Contract

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/IRenderer.cs" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/IRenderer.cs" data-language="csharp"></div>

### Clear Renderer

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/ClearRenderer.cs" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/ClearRenderer.cs" data-language="csharp"></div>

### Entry Point

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Program.cs" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Program.cs" data-language="csharp"></div>

### Platform and Shared Usings

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/CocoaHelper.cs" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/CocoaHelper.cs" data-language="csharp"></div>

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Usings.cs" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Usings.cs" data-language="csharp"></div>

## Frame Ownership

The host acquires the current swap-chain drawable and creates one graphics command buffer per frame. The selected renderer records its workload, after which the host transitions the drawable for presentation, submits the command buffer, waits for completion, and presents.

Each renderer owns only its workload resources and responds to drawable-size changes through `Resize`. The host retains window, context, swap-chain, submission, and presentation ownership for every tutorial.