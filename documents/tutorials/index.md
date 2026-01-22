# Tutorials

Welcome to the Zenith.NET tutorials! These step-by-step guides will help you learn how to use Zenith.NET for GPU programming.

## Getting Started

New to Zenith.NET? Start here to set up your environment and render your first triangle.

| Tutorial | Description |
|----------|-------------|
| [Prerequisites](getting-started/prerequisites.md) | Set up your development environment and create the project framework |
| [Hello Triangle](getting-started/hello-triangle.md) | Create your first renderer class and draw a colored triangle |
| [Textured Quad](getting-started/textured-quad.md) | Load textures and use samplers with resource binding |
| [Spinning Cube](getting-started/spinning-cube.md) | Render a 3D cube with index buffers and MVP transforms |

## Tutorial Structure

Each tutorial follows a consistent pattern:

1. **Overview** - What you'll build and learn
2. **Renderer Class** - Complete implementation code
3. **Running the Tutorial** - How to run the example
4. **Result** - Screenshot of the expected output
5. **Code Breakdown** - Step-by-step explanation of key concepts

The `App` class provides static access to `GraphicsContext` and `SwapChain`, making renderer code clean and focused on rendering logic.

## What You'll Learn

By completing the Getting Started tutorials, you will understand:

- How to create a reusable `App` framework with `GraphicsContext` and `SwapChain`
- How to implement the `IRenderer` interface for modular rendering
- How to create `Buffer` resources for vertex and index data
- How to write and compile shaders using Slang
- How to configure a `GraphicsPipeline` for rendering
- How to record and submit drawing commands via `CommandBuffer`
- How to load textures and bind resources using `ResourceLayout` and `ResourceSet`
- How to render 3D geometry with MVP transformation matrices

## Requirements

Before starting, ensure you have:

- .NET 10.0 SDK or later
- A GPU with DirectX 12, Metal 4, or Vulkan 1.4 support
- Visual Studio 2026, VS Code, or JetBrains Rider

### Supported Platforms

| Platform | DirectX 12 | Metal 4 | Vulkan 1.4 |
|----------|:----------:|:-------:|:----------:|
| Windows  | ✅ | - | ✅ |
| Linux    | - | - | ✅ |
| Android  | - | - | ✅ |
| macOS    | - | ✅ | ✅ |
| iOS      | - | ✅ | ✅ |