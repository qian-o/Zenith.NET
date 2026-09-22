<p align="center">
  <img src="https://raw.githubusercontent.com/qian-o/Zenith.NET/master/documents/images/Zenith.NET.png" alt="Zenith.NET icon" width="128" height="128">
</p>

<h1 align="center">Zenith.NET</h1>

<p align="center">
  Cross-platform graphics for .NET
</p>

<p align="center">
  <a href="https://qian-o.github.io/Zenith.NET/">Documentation</a> ·
  <a href="https://qian-o.github.io/Zenith.NET/learn/samples.html">Samples</a> ·
  <a href="https://qian-o.github.io/Zenith.NET/api/">API Reference</a> ·
  <a href="https://www.nuget.org/packages/Zenith.NET">NuGet</a>
</p>

Zenith.NET is a rendering hardware interface (RHI) for building graphics and compute applications in C#. A shared API connects your rendering code to DirectX 12, Metal, and Vulkan, while giving you control over GPU resources and execution.

[![Reflective spheres above a checkerboard floor, rendered with Zenith.NET](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/ray-tracing.png)](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/ray-tracing.png)

<p align="center">
  <a href="https://qian-o.github.io/Zenith.NET/learn/samples.html#ray-tracing">Ray tracing sample</a>
</p>

## Features

- **Graphics and compute.** Combine rasterization, compute shaders, and indirect drawing within the same rendering API.
- **Slang shaders.** Compile shared shader source for each backend, and pass resource handles alongside shader parameters.
- **Explicit GPU control.** Manage memory allocation, command recording, synchronization, and resource lifetime to suit your renderer.
- **Ray queries and mesh shaders.** Use hardware ray tracing and task/mesh shading on devices that support them.

## Get started

Use a .NET 10 project with the [Zenith.NET](https://www.nuget.org/packages/Zenith.NET) core package. Add [Zenith.NET.Compiler](https://www.nuget.org/packages/Zenith.NET.Compiler) to compile Slang shaders, and choose a backend for your target platform:

| Backend package | Graphics API | Platforms |
| --- | --- | --- |
| [Zenith.NET.DirectX12](https://www.nuget.org/packages/Zenith.NET.DirectX12) | DirectX 12 | Windows |
| [Zenith.NET.Metal](https://www.nuget.org/packages/Zenith.NET.Metal) | Metal 4 | Apple platforms |
| [Zenith.NET.Vulkan](https://www.nuget.org/packages/Zenith.NET.Vulkan) | Vulkan 1.4 | Windows, Linux, Android |

The [First Triangle tutorial](https://qian-o.github.io/Zenith.NET/learn/first-triangle.html) takes you from an empty console project to a rendered triangle, explaining the window, vertex data, shaders, and drawing commands along the way.

Explore [ZenithTutorials](https://github.com/qian-o/ZenithTutorials) for examples of compute, indirect drawing, ray queries, and mesh shading. The [Experiments directory](https://github.com/qian-o/Zenith.NET/tree/master/sources/Experiments) contains larger applications, including a room scene, a water simulation, and a drawing canvas.

## Integrations

Embed rendering in [Avalonia](https://www.nuget.org/packages/Zenith.NET.Views.Avalonia), [.NET MAUI](https://www.nuget.org/packages/Zenith.NET.Views.Maui), [Windows Forms](https://www.nuget.org/packages/Zenith.NET.Views.WinForms), [WinUI and Uno Platform](https://www.nuget.org/packages/Zenith.NET.Views.WinUI), or [WPF](https://www.nuget.org/packages/Zenith.NET.Views.WPF) applications. The controls share a rendering event interface; [Platform Integration](https://qian-o.github.io/Zenith.NET/learn/concepts/platform-integration.html) explains their presentation and lifecycle responsibilities.

Optional extensions provide [ImageSharp image loading](https://www.nuget.org/packages/Zenith.NET.Extensions.ImageSharp), [Dear ImGui integration](https://www.nuget.org/packages/Zenith.NET.Extensions.ImGui), [SkiaSharp drawing](https://www.nuget.org/packages/Zenith.NET.Extensions.Skia), and [spatial and temporal upscaling](https://www.nuget.org/packages/Zenith.NET.Extensions.Upscaling).

## Contributing

[Bug reports and feature requests](https://github.com/qian-o/Zenith.NET/issues/new/choose), code contributions, and documentation improvements are welcome. For documentation and translations, see the [maintenance guide](https://github.com/qian-o/Zenith.NET/blob/master/documents/maintenance.md).

## License

[MIT](https://github.com/qian-o/Zenith.NET/blob/master/LICENSE)
