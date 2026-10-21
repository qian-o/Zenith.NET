# Experiments

Standalone applications for exploring rendering techniques, testing integrations, and inspecting graphics capabilities with Zenith.NET. These projects are experimental and may change as the library evolves.

For guided examples, see the [samples](https://qian-o.github.io/Zenith.NET/learn/samples.html).

## Projects

| Project | Purpose |
| --- | --- |
| [CornellBox](CornellBox) | Interactive path tracing with animated geometry and PBR materials. Requires a ray-tracing-capable GPU; the output is intentionally noisy, without temporal accumulation or denoising. |
| [FluidTank](FluidTank) | GPU particle-and-grid fluid simulation with water surface rendering. |
| [InkCanvas](InkCanvas) | Interactive drawing canvas using the Skia extension. |
| [PlatformDetection](PlatformDetection) | Console utility that attempts to initialize each graphics backend and reports device, ray tracing, and mesh shading capabilities. |

## Requirements

Use the .NET 10 SDK and a graphics driver that supports the selected backend. The graphical applications select DirectX 12 on Windows, Metal on macOS, and Vulkan on Linux.

## Navigation

In CornellBox and FluidTank, use **W/A/S/D** to move, **Q/E** to move down/up, and hold the **right mouse button** to look around. Additional controls are available in each application's settings panel.