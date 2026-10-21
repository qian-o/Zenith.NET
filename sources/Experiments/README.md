# Welcome to the Zenith.NET Experiments!

Here you'll find applications and utilities used to test Zenith.NET, as well as other experimental projects.

## CornellBox

[CornellBox](CornellBox) is a native-resolution, noisy path-tracing sample requiring a ray-tracing-capable GPU. It traces 4 samples per pixel per frame with up to 8 bounces, without temporal accumulation, denoising, upscaling, or frame generation.

The structure follows [FluidTank](FluidTank):

- `App` owns the window, direct input handlers, swap chain, and the single-threaded update/render loop.
- `Renderer` coordinates scene animation and two passes: `PathTracingPass` produces HDR color, then `OutputPass` applies tone mapping, display gamma, and 8-bit dithering.
- ImGui displays the output texture as its background and draws the controls directly into the swap-chain drawable. No separate UI-compositing pass or presentation scheduler is needed.
- `SceneResources` owns geometry, materials, instance transforms, and acceleration structures. Only the current transforms and the HDR color output are retained.

Use **W/A/S/D** to move, **Q/E** to move down/up, and hold the **right mouse button** to look around. **Pause** freezes scene animation; camera movement and per-frame path-tracing noise remain active.

Run from the repository root:

```powershell
dotnet run --project sources\Experiments\CornellBox\CornellBox.csproj
```

The sample uses DirectX 12 on Windows, Metal on macOS, and Vulkan on Linux. It does not reference or initialize the DLSS extension; the shared extension remains available for other applications.