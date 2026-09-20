# Samples

Use these examples to see how a particular rendering or compute workload fits together. The focused examples link to both the C# code that creates resources and records commands and the Slang code executed by the GPU. For a step-by-step application from an empty project, start with [First Triangle](first-triangle.md).

<a id="tutorial-sources"></a>
## Run the tutorial samples

[ZenithTutorials](https://github.com/qian-o/ZenithTutorials) contains six independent renderers in one desktop application. Use the .NET 10 SDK and a supported desktop backend as described below. Build and run the application, then select a sample from the console menu:

```sh
git clone https://github.com/qian-o/ZenithTutorials.git
cd ZenithTutorials
dotnet build ZenithTutorials.slnx
dotnet run --project ZenithTutorials/ZenithTutorials.csproj
```

The host selects DirectX 12 on Windows, Metal on macOS and Vulkan on Linux. Use a device, driver and shader compiler environment supported by the selected backend. The Linux window path requires X11 or XWayland. The [project file](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/ZenithTutorials.csproj) declares package dependencies and copies the shader and texture assets needed at runtime.

The samples share windowing and presentation code. You can choose any example independently according to the feature you want to study.

### Hello Triangle

Creates a vertex buffer and matching input layout, then draws three vertices through a graphics pipeline. Use it to connect resource creation with the commands in a render pass.

[HelloTriangleRenderer.cs](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/HelloTriangleRenderer.cs) · [HelloTriangle.slang](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/HelloTriangle.slang) · [Complete tutorial](first-triangle.md)

### Spinning Cube

Adds indexed drawing, model, view and projection matrices in a constant buffer and depth testing. The depth texture is recreated on resize. Use it when moving from clip-space geometry to a transformed object; the renderer uses a `D32FloatS8UInt` depth/stencil attachment.

[SpinningCubeRenderer.cs](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/SpinningCubeRenderer.cs) · [SpinningCube.slang](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/SpinningCube.slang)

### Compute Shader

Converts an image to grayscale with a compute dispatch and displays the resulting texture. It shows resource handles for a sampled input texture and a writable output texture, rounded-up thread-group counts and a shader bounds check. Keep the included `Assets/Textures/shoko.png` asset; the renderer processes it once and presents the result on subsequent frames.

[ComputeShaderRenderer.cs](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/ComputeShaderRenderer.cs) · [ComputeShader.slang](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/ComputeShader.slang)

### Indirect Drawing

Draws several copies of a cube using an indexed indirect argument buffer and per-instance data in a structured buffer. The CPU supplies the draw arguments and updates the transforms. Use it to understand the indirect argument layout and shader access to instance data before adding GPU-generated arguments.

[IndirectDrawingRenderer.cs](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/IndirectDrawingRenderer.cs) · [IndirectDrawing.slang](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/IndirectDrawing.slang)

### Ray Tracing

Builds bottom-level and top-level acceleration structures for a floor and procedural spheres, then traces inline ray queries in a compute shader. The renderer checks `context.Capabilities.RayTracingSupported` before constructing the workload. Use this example for acceleration-structure inputs and shader-side ray traversal.

[RayTracingRenderer.cs](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/RayTracingRenderer.cs) · [RayTracing.slang](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/RayTracing.slang)

### Mesh Shading

Uses a task shader to cull sphere instances and pass their indices to a mesh shader that emits geometry. The renderer checks `context.Capabilities.MeshShadingSupported` before creating its pipeline. Use it to follow resource handles and payload data through the task and mesh stages.

[MeshShadingRenderer.cs](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/MeshShadingRenderer.cs) · [MeshShading.slang](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/MeshShading.slang)

### Shared host and presentation

[App.cs](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/App.cs) creates the context, window and swap chain. It obtains a command buffer, calls the selected renderer, submits the frame and presents it. [IRenderer.cs](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/IRenderer.cs) defines the renderer's update, render, resize and disposal methods.

Compute and ray tracing produce offscreen textures. [TexturePresenter.cs](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/TexturePresenter.cs) and [PresentTexture.slang](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/PresentTexture.slang) draw those textures into the window's drawable. The host still owns submission and swap-chain presentation.

<a id="experiments"></a>
## Explore larger workloads

The main repository's [Experiments directory](https://github.com/qian-o/Zenith.NET/tree/master/sources/Experiments) contains applications that combine several features. Their projects reference the library source. From a checkout of Zenith.NET, run the chosen project, for example:

```sh
dotnet run --project sources/Experiments/FluidTank/FluidTank.csproj
```

| Example | What it demonstrates and requires |
| --- | --- |
| [CornellBox](https://github.com/qian-o/Zenith.NET/tree/master/sources/Experiments/CornellBox) | Path tracing, denoising, tone mapping, upscaling and ImGui controls. Requires ray tracing support and the shader compilation environment for its desktop backend. |
| [FluidTank](https://github.com/qian-o/Zenith.NET/tree/master/sources/Experiments/FluidTank) | Compute fluid simulation, dependencies between simulation stages and multipass surface rendering. Requires its desktop backend and shader compilation environment. |
| [InkCanvas](https://github.com/qian-o/Zenith.NET/tree/master/sources/Experiments/InkCanvas) | Skia drawing and input handling in a native window. Requires the backend and Skia native library for the target platform. |
| [PlatformDetection](https://github.com/qian-o/Zenith.NET/blob/master/sources/Experiments/PlatformDetection/Program.cs) | Attempts context creation for each backend and reports device capabilities. A context-creation failure is reported as unsupported; that result can also indicate a missing driver or runtime dependency. |

The windowed experiments use the same Windows, macOS and Linux/X11 host choices. For drawing inside Avalonia, WinForms, WPF, WinUI/Uno or MAUI, see [Platform Integration](concepts/platform-integration.md#ui-views) for the control's frame lifecycle and implementation links.
