# Tutorials

Welcome to the Zenith.NET tutorials! These step-by-step guides will help you learn how to use Zenith.NET for GPU programming.

## Getting Started

New to Zenith.NET? Start here to set up your environment and render your first graphics.

| Tutorial | Description |
|----------|-------------|
| [Prerequisites](getting-started/prerequisites.md) | Set up your development environment with `App` framework, `IRenderer` interface, and `BindingHelper` |
| [Hello Triangle](getting-started/hello-triangle.md) | Create vertex buffers, compile Slang shaders, and build your first graphics pipeline |
| [Textured Quad](getting-started/textured-quad.md) | Load textures, create samplers, and bind resources with `ResourceLayout` and `ResourceTable` |
| [Spinning Cube](getting-started/spinning-cube.md) | Use constant buffers for MVP matrices and render 3D geometry with depth testing |

## Intermediate

Build on the basics with GPU compute and advanced rendering techniques.

| Tutorial | Description |
|----------|-------------|
| [Compute Shader](intermediate/compute-shader.md) | Create compute pipelines for GPU image processing (grayscale conversion) |
| [Indirect Drawing](intermediate/indirect-drawing.md) | GPU-driven rendering with `DrawIndexedIndirect` for multi-instance drawing |

## Advanced

Explore cutting-edge GPU features for modern rendering (requires hardware support).

| Tutorial | Description | Requirement |
|----------|-------------|-------------|
| [Ray Tracing](advanced/ray-tracing.md) | Build acceleration structures (BLAS/TLAS), use `RayQuery` in compute shaders for ray tracing, and implement hard shadows | `RayTracingSupported` |
| [Mesh Shading](advanced/mesh-shading.md) | Use meshlet-based geometry processing with mesh shading pipelines | `MeshShadingSupported` |

## Tutorial Structure

Each tutorial follows a consistent pattern:

1. **Overview** - What you'll build and the key concepts covered
2. **Key Concepts** (advanced tutorials) - In-depth explanation of new API features
3. **Renderer Class** - Complete, runnable implementation code
4. **Running the Tutorial** - How to switch renderers and run the example
5. **Result** - Screenshot of the expected output
6. **Code Breakdown** - Step-by-step explanation of important code sections

All tutorials share the same `App` framework, making it easy to switch between examples by changing a single line in `Program.cs`.

## Learning Path

| Stage | You Will Learn |
|-------|----------------|
| **Prerequisites** | Set up the application framework, graphics context, and cross-platform resource binding |
| **Hello Triangle** | Create GPU buffers, compile shaders, configure graphics pipelines, and submit draw commands |
| **Textured Quad** | Load and sample textures, use index buffers, and bind shader resources |
| **Spinning Cube** | Pass data to shaders via constant buffers, implement 3D transformations, and enable depth testing |
| **Compute Shader** | Run general-purpose GPU computations for image processing |
| **Indirect Drawing** | Let the GPU control draw parameters for efficient multi-instance rendering |
| **Ray Tracing** | Build acceleration structures, trace rays with `RayQuery`, handle intersections, and implement shadows |
| **Mesh Shading** | Process geometry in meshlets using the modern mesh shading pipeline |

## Requirements

Before starting, ensure you have:

- .NET 10.0 SDK or later
- A GPU with DirectX 12, Metal 4, or Vulkan 1.4 support
- Visual Studio 2026, VS Code, or JetBrains Rider

> [!NOTE]
> These tutorials are designed for desktop platforms (Windows, Linux, and macOS).
> See [Prerequisites](getting-started/prerequisites.md) for detailed platform support and setup instructions.

## Source Code

> [!TIP]
> The complete source code for all tutorials is available on GitHub: [ZenithTutorials](https://github.com/qian-o/ZenithTutorials)