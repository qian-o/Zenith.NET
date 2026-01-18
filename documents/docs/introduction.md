# Introduction

Zenith.NET is a Rendering Hardware Interface (RHI) library built on Silk.NET, designed to simplify GPU programming.
It provides a high-level abstraction layer that enables developers to easily interact with various graphics APIs (DirectX 12, Metal, and Vulkan) without needing to understand the underlying implementation details.

## Features

### 🎮 Modern GPU Features
- **Programmable Pipelines**: Full graphics pipeline support (Vertex, Hull, Domain, Geometry, Pixel) and compute pipelines.
- **Ray Tracing**: Complete hardware-accelerated ray tracing support, including:
  - Acceleration structure (BLAS/TLAS) building and updating
  - Ray tracing pipeline (RayGeneration, Miss, AnyHit, Intersection, ClosestHit)
  - HitGroup configuration
- **Mesh Shaders**: Modern Mesh Shading pipeline support (Amplification + Mesh + Pixel).
- **GPU Queries**: Occlusion Query and Timestamp Query support.

### 🌍 Cross-Platform
You can run on any platform that supports .NET and the supported graphics APIs, except for the Web platform.

| Backend    | Platform                 |
| ---------- | ------------------------ |
| DirectX 12 | Windows                  |
| Metal      | macOS, iOS               |
| Vulkan     | Windows, Linux, Android  |

### 🖼️ Multiple UI Framework Integration
Out-of-the-box view components supporting various mainstream UI frameworks:
- **Avalonia** - Cross-platform XAML framework
- **MAUI** - Cross-platform UI framework
- **WinForms** - Windows Forms
- **WinUI** - Windows App SDK (with Uno Platform support)
- **WPF** - Windows Presentation Foundation

### 🔧 Extension Ecosystem
- **ImageSharp Integration**: Through the `Zenith.NET.Extensions.ImageSharp` extension, provides image loading and processing support.
- **ImGui Integration**: Through the `Zenith.NET.Extensions.ImGui` extension, provides immediate mode GUI support for debugging and tool development.
- **SkiaSharp Integration**: Through the `Zenith.NET.Extensions.Skia` extension, enables 2D rendering.
- **Slang Shader Compilation**: Through the `Zenith.NET.Extensions.Slang` extension, write shaders in Slang language with automatic compilation to target backend formats (DXIL, SPIR-V, Metal).

### ✨ Other Features
- **Validation Layer**: Built-in validation layer to help developers catch errors during development.
- **Resource Management**: Unified resource creation and lifecycle management.
- **Command Queues**: Support for Graphics, Compute, and Copy command queues to fully utilize GPU asynchronous execution capabilities.
- **Rich Pixel Formats**: Support for multiple pixel formats to meet different rendering needs.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                         Application                         │
├─────────────────────────────────────────────────────────────┤
│        Extensions (ImageSharp, ImGui, Skia, Slang)          │
├─────────────────────────────────────────────────────────────┤
│      Views (Avalonia, MAUI, WinForms, WinUI, WPF)           │
├─────────────────────────────────────────────────────────────┤
│                       Zenith.NET (RHI)                      │
│  ┌───────────────────┬───────────────┬───────────────────┐  │
│  │  GraphicsContext  │ CommandBuffer │     Pipeline      │  │
│  │  Buffer, Texture  │  FrameBuffer  │   ResourceSet     │  │
│  │     SwapChain     │    Sampler    │ AccelerationStruct│  │
│  └───────────────────┴───────────────┴───────────────────┘  │
├─────────────────────────────────────────────────────────────┤
│      DirectX 12      │     Metal     │      Vulkan          │
├─────────────────────────────────────────────────────────────┤
│                          Silk.NET                           │
└─────────────────────────────────────────────────────────────┘
```
