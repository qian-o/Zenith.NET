# Experiments

Standalone applications for exploring rendering techniques, testing integrations, and inspecting graphics capabilities with Zenith.NET. These projects are experimental and may change as the library evolves.

For guided examples, see the [samples](https://qian-o.github.io/Zenith.NET/learn/samples.html).

## Projects

| Project | Purpose |
| --- | --- |
| [CornellBox](CornellBox) | Interactive path-traced Cornell box scene with animated geometry and PBR materials. Requires a ray-tracing-capable GPU. |
| [FluidTank](FluidTank) | Interactive fluid simulation with water surface rendering. |
| [InkCanvas](InkCanvas) | Interactive drawing canvas. |
| [PlatformDetection](PlatformDetection) | Console utility for inspecting graphics devices and backend capabilities. |

## Requirements

Use the .NET 10 SDK and a graphics driver that supports the selected backend. The graphical applications select DirectX 12 on Windows, Metal on macOS, and Vulkan on Linux.
