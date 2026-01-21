# Tutorials

Welcome to the Zenith.NET tutorials! These step-by-step guides will help you learn how to use Zenith.NET for GPU programming.

## Getting Started

New to Zenith.NET? Start here to set up your environment and render your first triangle.

| Tutorial | Description |
|----------|-------------|
| [Prerequisites](getting-started/prerequisites.md) | Set up your development environment and create the project framework |
| [Hello Triangle](getting-started/hello-triangle.md) | Create your first renderer class and draw a colored triangle |

## Tutorial Structure

Each tutorial follows a consistent pattern:

1. **Prerequisites** - Sets up the `App` framework class shared by all tutorials
2. **Renderer Class** - Each tutorial implements a specific renderer using the `IRenderer` interface
3. **Code Breakdown** - Step-by-step explanation of key concepts

The `App` class provides static access to `GraphicsContext` and `SwapChain`, making renderer code clean and focused on rendering logic.

## What You'll Learn

By completing the Getting Started tutorials, you will understand:

- How to create a reusable `App` framework with `GraphicsContext` and `SwapChain`
- How to implement the `IRenderer` interface for modular rendering
- How to create `Buffer` resources for vertex data
- How to write and compile shaders using Slang
- How to configure a `GraphicsPipeline` for rendering
- How to record and submit drawing commands via `CommandBuffer`
- How to switch between different renderers using `App.Run<TRenderer>()`

## Requirements

Before starting, ensure you have:

- .NET 10.0 SDK or later
- A GPU with DirectX 12, Metal 4, or Vulkan 1.4 support
- Visual Studio 2026 or another .NET 10 compatible IDE

### Supported Platforms

| Platform | DirectX 12 | Metal 4 | Vulkan 1.4 |
|----------|:----------:|:-------:|:----------:|
| Windows  | ✅ | - | ✅ |
| Linux    | - | - | ✅ |
| Android  | - | - | ✅ |
| macOS    | - | ✅ | ✅ |
| iOS      | - | ✅ | ✅ |