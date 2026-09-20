# First Triangle

In this tutorial, you will build a .NET console application that opens a window and draws a triangle with a smooth color gradient between its corners. You will create the resources it needs, describe how the GPU reads them, and record the commands that turn three vertices into a displayed image.

The application has two parts. Initialization creates the window and the resources needed to draw. The frame loop records drawing commands and presents the result repeatedly. Resources that stay the same between frames are created once, before that loop.

<a id="prerequisites"></a>
## 1. Before you begin

You need the **.NET 10 SDK**, a code editor or IDE, and a desktop with a GPU and driver supported by a Zenith.NET backend. Basic familiarity with C# is enough; the graphics concepts are introduced as they are used. This is an ordinary `net10.0` console project. A backend translates Zenith.NET operations into calls to a graphics API, such as DirectX 12, Metal or Vulkan. The application chooses one at startup:

| Platform | Graphics backend | Window surface |
| --- | --- | --- |
| Windows | DirectX 12 | Win32 window handle |
| macOS | Metal 4 | Metal layer attached to the window |
| Linux | Vulkan 1.4 | X11 window and display; XWayland is also suitable |

Linux needs an X11-compatible window session for the windowing path used here. Optional ray tracing and mesh shading features are not needed for this triangle.

Zenith.NET renders into a surface supplied by the application. Silk.NET.Windowing will create the desktop window and process its events. On macOS, the presentation target is a Metal layer ([CAMetalLayer.cs](https://github.com/qian-o/Metal.NET/blob/master/Metal.NET/CoreAnimation/CAMetalLayer.cs)); the tutorial's window helper attaches that layer for us.

You can follow this page from an empty project. The [Hello Triangle sample](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/HelloTriangleRenderer.cs) and its [shared host](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/App.cs) are available as a reference for the rendering and windowing APIs used here.

<a id="project"></a>
## 2. Create a console project

Create the project and enter its directory:

```sh
dotnet new console -n FirstTriangle -f net10.0
cd FirstTriangle
```

Install the packages:

```sh
dotnet add package Zenith.NET
dotnet add package Zenith.NET.Compiler
dotnet add package Zenith.NET.DirectX12
dotnet add package Zenith.NET.Metal
dotnet add package Zenith.NET.Vulkan
dotnet add package Silk.NET.Windowing
```

`Zenith.NET` supplies the shared rendering API. The three backend packages let the same program select the implementation for its platform. `Zenith.NET.Compiler` compiles Slang shaders, the small GPU programs we will write later, for that implementation. Silk.NET supplies the window and event loop.

Enable **Allow unsafe code** (`AllowUnsafeBlocks`) in the project's build settings. We will use it to pass vertex data to the GPU and for the window helper's native calls.

<a id="window"></a>
## 3. Open a window

Add [CocoaHelper.cs](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/CocoaHelper.cs) to the project. It handles the Metal layer on macOS; the program calls it only on that platform.

Replace the generated `Program.cs` with the following code. It uses top-level statements, which execute in order without an explicit `Main` method. Later sections will tell you where to add code to this same file:

```csharp
global using System.Runtime.InteropServices;
using System.Numerics;
using Silk.NET.Windowing;
using Zenith.NET;
using Zenith.NET.DirectX12;
using Zenith.NET.Metal;
using Zenith.NET.Vulkan;
using ZenithTutorials;
using Buffer = Zenith.NET.Buffer;

IWindow window = Window.Create(WindowOptions.Default with
{
    API = GraphicsAPI.None,
    Title = "Zenith.NET - First Triangle"
});

window.Initialize();
window.Center();

window.Run();

window.Dispose();
```

`GraphicsAPI.None` asks Silk.NET to create a window without its own graphics context. We will create that context with Zenith.NET. `Initialize()` creates the native window and makes its handles available. A handle is an identifier supplied by the windowing system. `Center()` centers the window, and `Run()` processes events until it closes. Execution then continues to `Dispose()`, which releases the window.

`using ZenithTutorials` imports the helper's namespace. The global using supplies its interop attributes, and the `Buffer` alias distinguishes Zenith.NET's GPU buffer from `System.Buffer`.

Run `dotnet run`. An empty window should open and respond to moving, resizing and closing. Its contents are not defined yet because we have not submitted any rendering commands. Close it before continuing.

<a id="context"></a>
## 4. Connect the GPU to the window

### Create the graphics context

The window is ready, but it has no connection to a GPU yet. A [`GraphicsContext`](xref:Zenith.NET.GraphicsContext) provides that connection and the queues that accept recorded commands for execution on the GPU.

Insert this block **after the using directives, before `IWindow window`**:

```csharp
GraphicsContext context;
if (OperatingSystem.IsWindows())
{
    context = GraphicsContext.CreateDirectX12(useValidationLayer: true);
}
else if (OperatingSystem.IsMacOS())
{
    context = GraphicsContext.CreateMetal(useValidationLayer: true);
}
else
{
    context = GraphicsContext.CreateVulkan(useValidationLayer: true);
}

context.ValidationMessage += static (_, args) => Console.WriteLine($"[{args.Severity}] {args.Message}");
```

For the three desktop platforms covered here, this chooses DirectX 12 on Windows, Metal on macOS and Vulkan on Linux. The operations after initialization use the same Zenith.NET API. We request validation during development and print diagnostics reported through `ValidationMessage`. The available diagnostics depend on the backend and installed validation components.

### Describe the native surface

The context also needs to know where to display its output. Add the following **after `window.Center()` and before `window.Run()`**:

```csharp
uint width = (uint)window.FramebufferSize.X;
uint height = (uint)window.FramebufferSize.Y;

Surface surface;
if (OperatingSystem.IsWindows())
{
    surface = Surface.Win32(window.Native!.Win32!.Value.Hwnd, width, height);
}
else if (OperatingSystem.IsMacOS())
{
    surface = Surface.Apple(CocoaHelper.CreateLayer(window.Native!.Cocoa!.Value), width, height);
}
else
{
    surface = Surface.Xlib(window.Native!.X11!.Value.Display, (nint)window.Native.X11.Value.Window, width, height);
}
```

[`Surface`](xref:Zenith.NET.Surface) describes a native presentation target and its pixel dimensions. The framebuffer is the pixel image associated with the window. `FramebufferSize` gives its dimensions in pixels; the window's logical dimensions can differ when display scaling is enabled.

The surface matches the backend selected above. Windows supplies a window handle, Linux supplies an X11 display and window, and the macOS helper supplies the attached Metal layer. This is the only platform-specific part of connecting our window to rendering.

### Create the swap chain

Use the surface description to create the images that will be presented. Immediately after the surface block, add:

```csharp
SwapChain swapChain = context.CreateSwapChain(new()
{
    Surface = surface,
    Format = PixelFormat.B8G8R8A8UNorm
});
```

The [`SwapChain`](xref:Zenith.NET.SwapChain) manages the images displayed in the window. Each frame, `Drawable` gives us the current image as a `Texture`, the API type for image data. After drawing, `Present()` requests display of the result and advances to the next drawable.

`B8G8R8A8UNorm` stores four 8-bit color channels in blue, green, red and alpha order. `UNorm` means integer values from 0 to 255 represent values from 0 to 1. The shader will still return a logical RGBA value; this format determines how those channels are stored in the image. Later, the graphics pipeline will use `swapChain.Desc.Format` to match this attachment.

The swap chain uses both the context and the native window. Replace the `window.Dispose();` line at the bottom of `Program.cs` with this cleanup order:

```csharp
swapChain.Dispose();
window.Dispose();

context.Dispose();
```

<a id="frame-loop"></a>
## 5. Display a background color

Start by filling the image with one background color, an operation called a clear. Seeing that color confirms that the context, surface, command submission and swap chain work together before we add geometry.

A render callback is code that Silk.NET calls when it is time to draw a frame. This callback does not need the event's elapsed-time argument, so it is named `_`. Register it **before `window.Run();`**:

```csharp
window.Render += _ =>
{
    if (width is 0 || height is 0)
    {
        return;
    }

    CommandBuffer commandBuffer = context.GraphicsQueue.CommandBuffer();

    commandBuffer.Transition(swapChain.Drawable, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);

    commandBuffer.BeginRenderPass([ColorAttachment.Clear(swapChain.Drawable, new(0.04f, 0.055f, 0.075f, 1.0f))], null);

    // Add the triangle draw commands here later.

    commandBuffer.EndRenderPass();

    commandBuffer.Transition(swapChain.Drawable, default, TextureLayout.ColorAttachment, TextureLayout.Present);

    commandBuffer.Submit().Wait();

    swapChain.Present();
};
```

### Prepare the drawable

`GraphicsQueue.CommandBuffer()` obtains a command buffer with recording already begun. Recording describes work for the GPU; it does not execute that work immediately.

The first `Transition` prepares the drawable for color-attachment access. `default` selects mip level 0 and array layer 0. We use `Undefined` as the previous layout because we will clear the entire image and discard its previous contents. A pass that needs to preserve existing content must use its actual previous layout instead.

Use the swap chain's current drawable each frame. Presentation can change which image is available, so a texture saved from an earlier frame is not a permanent window render target.

### Clear, submit and present

A render pass groups drawing operations that use the same attachments. Here, the color attachment is the swap chain's drawable, the image that receives our drawing. [`ColorAttachment.Clear`](xref:Zenith.NET.ColorAttachment.Clear(Zenith.NET.Texture,System.Numerics.Vector4)) creates a description telling the pass to clear to the given RGBA color and keep the result when the pass ends. `BeginRenderPass` records those instructions; the GPU performs the clear after the commands are submitted.

The brackets contain our single color attachment. The `null` argument means that there is no depth/stencil attachment, which would store depth values and stencil masks for depth and stencil tests.

`BeginRenderPass` sets a viewport and scissor covering the attachment. The viewport maps the rendered coordinates to pixels, and the scissor limits which pixels can be written. These defaults cover the whole drawable, which is what this triangle needs.

After `EndRenderPass`, the second transition prepares the image for presentation. `Submit()` finishes recording and submits the command buffer; `Wait()` waits for the GPU to complete that submission. `Present()` then requests display of the rendered image.

### Respond to resizing

The swap chain's images must follow the framebuffer dimensions. Use `FramebufferResize` to respond to changes in pixel size, including changes caused by display scaling. After the render callback, still before `window.Run();`, add:

```csharp
window.FramebufferResize += _ =>
{
    width = (uint)window.FramebufferSize.X;
    height = (uint)window.FramebufferSize.Y;

    if (width is 0 || height is 0)
    {
        return;
    }

    swapChain.Resize(width, height);
};
```

Silk.NET distinguishes window-size changes from [framebuffer-size changes](https://github.com/dotnet/Silk.NET/blob/main/src/Windowing/Silk.NET.Windowing.Common/Interfaces/IView.cs). We need the latter because the swap chain stores pixels. A minimized window can have a zero-size framebuffer. Both callbacks skip their GPU work in that case. After a resize, the render pass will use the new attachment dimensions for its viewport and scissor.

We wait after every submission, so no earlier frame is still using the images when the resize handler replaces them. The current `Present()` implementation also waits on the graphics queue. This simple loop keeps one frame's work complete before the next begins; [Synchronization](concepts/synchronization.md#cpu-and-gpu) covers more advanced scheduling.

Run `dotnet run` again. The window should now have a uniform dark background that continues to fill it after resizing. Keep this callback: the triangle will be drawn inside its existing render pass.

<a id="resources"></a>
## 6. Give the GPU three vertices

A vertex is a point together with the data used to draw it. Our triangle has three vertices, one for each corner. Each contains a position and a color. The GPU will interpolate the colors, calculating intermediate values across the triangle.

### Define a vertex

Append this declaration **at the end of `Program.cs`, after the cleanup statements**:

```csharp
[StructLayout(LayoutKind.Sequential)]
file struct Vertex(Vector3 position, Vector4 color)
{
    public Vector3 Position = position;

    public Vector4 Color = color;
}
```

`file` limits this type to `Program.cs`, and the constructor initializes its position and color fields. `LayoutKind.Sequential` keeps the fields in declaration order. `Position` contains three 32-bit floats and `Color` contains four. In this struct, position begins at byte 0, color begins at byte 12, and the complete vertex occupies 28 bytes. We will describe that same layout to the graphics pipeline.

### Choose the corners

Add this array **before `window.Render += _ =>`**. The following resource-creation steps also belong before that callback, in the order shown.

```csharp
Vertex[] vertices =
[
    new(new(0.0f, 0.6f, 0.0f), new(1.0f, 0.2f, 0.15f, 1.0f)),
    new(new(0.6f, -0.5f, 0.0f), new(0.15f, 0.85f, 0.35f, 1.0f)),
    new(new(-0.6f, -0.5f, 0.0f), new(0.2f, 0.45f, 1.0f, 1.0f))
];
```

Each array entry creates a `Vertex`; the two inner `new` expressions create its `Vector3` position and `Vector4` color. These are the top, lower-right and lower-left corners. The vertex shader will give each position a fourth coordinate, `w = 1`, so x and y directly correspond to normalized device coordinates. -1 and +1 are the image edges, and 0 is the center. The coordinates do not depend on the number of pixels in the window.

Each color contains red, green, blue and alpha components. Alpha is 1 at all three corners. All three positions have z = 0, placing them in the same plane. These positions need no camera or transformation matrix because they already describe the coordinates we want to render. For this single flat triangle, we will also disable blending and depth testing and omit a depth buffer.

### Allocate a buffer and upload the array

The managed array is CPU data. A [`Buffer`](xref:Zenith.NET.Buffer) gives the GPU storage from which it can read the vertices. After the array, add:

```csharp
Buffer vertexBuffer;

unsafe
{
    vertexBuffer = context.CreateBuffer(new()
    {
        SizeInBytes = (uint)(sizeof(Vertex) * vertices.Length),
        StrideInBytes = (uint)sizeof(Vertex),
        Usages = BufferUsages.Vertex,
        Residency = MemoryResidency.CpuWriteOnly
    });

    fixed (Vertex* pointer = vertices)
    {
        vertexBuffer.Upload(0, new()
        {
            Pointer = (nint)pointer,
            SizeInBytes = (uint)(sizeof(Vertex) * vertices.Length)
        });
    }
}
```

The total size is `sizeof(Vertex) * vertices.Length`: 28 bytes per vertex, three vertices, 84 bytes in all. `BufferUsages.Vertex` permits vertex-input access. `CpuWriteOnly` lets the CPU initialize this small buffer directly. The descriptor's stride records the element size; the input layout we create below will tell vertex fetching how to read the individual attributes.

Creating a buffer does not populate it. `Upload` copies the array into that allocation starting at destination byte offset 0. Its source description supplies a pointer and the number of bytes to copy.

`fixed` prevents the garbage collector from moving the array while its address is used. For this CPU-writable buffer, `Upload` maps, copies and unmaps the memory before returning, so the array does not need to stay pinned afterward. The buffer must remain alive throughout rendering, and the upload only needs to happen once because the vertices do not change.

Add `vertexBuffer.Dispose();` immediately before `swapChain.Dispose();` in the cleanup at the bottom of `Program.cs`. For larger static geometry, [Resource Management](concepts/resource-management.md#memory-placement) explains when to choose GPU-only memory and transfer uploads instead.

<a id="shader"></a>
## 7. Write the two shader stages

A shader is a program executed by the GPU. This draw needs a vertex shader to position vertices and a fragment shader to produce the colors inside the triangle. We will write them in Slang, a shader language, and compile two entry points from the same file. An entry point is the function where a shader stage begins executing.

Create `Triangle.slang`, starting with these two structures:

```slang
struct VSInput
{
    float3 Position : POSITION0;

    float4 Color : COLOR0;
};

struct FSInput
{
    float4 Position : SV_POSITION;

    float4 Color : COLOR0;
};
```

`VSInput` describes what the vertex shader receives from the vertex buffer. The semantics `POSITION0` and `COLOR0` identify the inputs; C# field names alone do not establish that mapping.

`FSInput` connects the vertex shader's output to the fragment shader's input. When written by the vertex shader, `SV_POSITION` supplies a clip-space position: the four coordinates the GPU uses to determine what lies inside the visible image. `COLOR0` carries the color to interpolate between vertices. The two stages must agree on its type and semantic.

### Position each vertex

Append the vertex entry point to `Triangle.slang`:

```slang
[shader("vertex")]
FSInput VSMain(VSInput input)
{
    FSInput output;
    output.Position = float4(input.Position, 1.0);
    output.Color = input.Color;

    return output;
}
```

The GPU invokes this entry point for each input vertex. It adds the homogeneous coordinate `w = 1` to the position and forwards the color. After the vertex stage, position is divided by w; here that division leaves x, y and z unchanged. This is why the positions chosen above can be understood directly in normalized device coordinates.

### Color the fragments

Append the fragment entry point to the same file:

```slang
[shader("fragment")]
float4 FSMain(FSInput input) : SV_TARGET
{
    return input.Color;
}
```

Rasterization determines which image locations the triangle covers. It produces fragments, the data processed by the fragment shader, with vertex colors interpolated across the covered area. `FSMain` returns this interpolated color through `SV_TARGET`, which identifies the first color attachment.

Vertex-color interpolation is separate from attachment blending. Blending combines a fragment's output with the color already stored in the attachment. Our opaque blend state disables that combination, so the interpolated color replaces the background inside the triangle.

### Compile for the selected backend

In the file properties for `Triangle.slang`, set **Copy to Output Directory** to **Copy if newer**. This makes the shader available beside the executable.

Return to `Program.cs`. After the vertex upload, add these initialization statements before the render callback:

```csharp
string shaderPath = Path.Combine(AppContext.BaseDirectory, "Triangle.slang");

Shader vertexShader = context.CreateShader(ZenithCompiler.CompileFromFile(context.GraphicsApi, shaderPath, "VSMain"));
Shader fragmentShader = context.CreateShader(ZenithCompiler.CompileFromFile(context.GraphicsApi, shaderPath, "FSMain"));
```

[`ZenithCompiler`](xref:Zenith.NET.ZenithCompiler) returns a shader description containing code for the requested graphics API. `CreateShader` creates the corresponding backend object. Both the graphics API and entry-point names matter: use `context.GraphicsApi`, and spell `VSMain` and `FSMain` exactly as in the Slang file.

Compilation happens once during initialization. `AppContext.BaseDirectory` locates the copied shader beside the executable, independently of the current working directory. These shaders only consume vertex input, so there are no constant buffers, textures or resource handles to bind yet.

<a id="vertex-layout"></a>
## 8. Describe the vertex input layout

The GPU now has the bytes and shader code, but it still needs to know how to interpret each vertex. An attribute is one value carried by a vertex, such as its position or color. The layout identifies each attribute's format and byte offset, and the stride is the distance in bytes from one vertex to the next. Add the following initialization block after shader creation:

```csharp
InputLayout inputLayout = new();
inputLayout.Add(new() { Format = ElementFormat.Float3, Semantic = ElementSemantic.Position });
inputLayout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Color });
```

[`InputLayout.Add`](xref:Zenith.NET.InputLayout.Add(Zenith.NET.InputElement)) appends an element at the current stride and increases that stride by the element's size. It does not inspect the C# struct. Here it produces the following agreement between CPU memory and shader inputs:

| C# field | Byte offset | Format | Shader input |
| --- | --- | --- | --- |
| `Vertex.Position` | 0 | `Float3` (12 bytes) | `POSITION0` |
| `Vertex.Color` | 12 | `Float4` (16 bytes) | `COLOR0` |
| Next vertex | 28 | Total stride: 28 bytes | Next input record |

Changing the C# field order or padding without updating this description would make the GPU read the wrong bytes. The default `SemanticIndex` is zero, matching `POSITION0` and `COLOR0`. Both elements belong to the same buffer, so the pipeline will contain one input layout and we will bind that buffer to slot 0.

<a id="pipeline"></a>
## 9. Connect the shaders and vertex layout

We now have vertex data, an input layout and two shaders. The [`GraphicsPipeline`](xref:Zenith.NET.GraphicsPipeline) connects them and specifies how the GPU assembles vertices into primitives and writes the result to an attachment.

A pipeline combines programmable shader stages with fixed-function settings, which control GPU operations such as triangle assembly, face culling and blending. For this triangle, choose these settings:

- `TriangleList` assembles each consecutive group of three vertices into one triangle. We will supply three vertices and draw once, so no index buffer is needed.
- The color format comes from `swapChain.Desc.Format`. It must match the image used by the render pass. `Count1` means one sample per pixel.
- `CullNone()` fills the triangle without discarding either face orientation. The order of its vertices determines the face winding, so disabling culling prevents that order from hiding our first triangle.
- `DepthNone()` disables depth testing and writing, matching the pass without a depth attachment.
- `Opaque()` disables blending, so the fragment shader's color replaces the background inside the triangle.

After the input layout, add:

```csharp
GraphicsPipeline pipeline = context.CreateGraphicsPipeline(new()
{
    VertexShader = vertexShader,
    FragmentShader = fragmentShader,
    InputLayouts = [inputLayout],
    PrimitiveTopology = PrimitiveTopology.TriangleList,
    AttachmentFormats = new()
    {
        ColorFormats = [swapChain.Desc.Format],
        SampleCount = SampleCount.Count1
    },
    RenderState = new()
    {
        Rasterizer = RasterizerState.CullNone(),
        DepthStencil = DepthStencilState.DepthNone(),
        Blend = BlendState.Opaque()
    }
});

vertexShader.Dispose();
fragmentShader.Dispose();
```

The pipeline description declares the attachment configuration; it does not create another image. `InputLayouts` has one entry because position and color are stored together in one buffer.

The shader objects can be disposed after creating the pipeline, as they are in the sample renderer. Keep the pipeline itself alive for every frame that draws with it. Add `pipeline.Dispose();` before `vertexBuffer.Dispose();` in the cleanup. Pipeline creation is the final initialization step before the render callback.

<a id="draw"></a>
## 10. Draw the triangle inside the pass

In the `window.Render` callback, replace `// Add the triangle draw commands here later.` with these three commands, between `BeginRenderPass` and `EndRenderPass`:

```csharp
commandBuffer.SetPipeline(pipeline);
commandBuffer.SetVertexBuffer(vertexBuffer, 0, 0);

commandBuffer.Draw(3, 1, 0, 0);
```

`SetPipeline` selects the shaders and state we just created. Set it before binding vertex data: the backend uses the pipeline's input layout to interpret the binding.

`SetVertexBuffer(vertexBuffer, 0, 0)` binds the buffer starting at byte offset 0 to input slot 0. That slot corresponds to the single entry in `InputLayouts`. It does not copy or upload the data again.

The arguments to `Draw` are:

| Argument | Value | Meaning |
| --- | --- | --- |
| `vertexCount` | 3 | Read three vertices. |
| `instanceCount` | 1 | Draw one copy of the triangle. |
| `firstVertex` | 0 | Start with the first vertex in the buffer. |
| `firstInstance` | 0 | Start instance numbering at zero. |

The commands are still only recorded at this point. The `Submit().Wait()` and `Present()` calls already at the end of the render callback execute the work and display its result. Clearing remains necessary because the triangle covers only part of the image; the background must also receive a defined color each frame.

<a id="run"></a>
## 11. Run, resize and close

Build and run from the `FirstTriangle` directory:

```sh
dotnet build
dotnet run --no-build
```

The window should display a triangle with a red upper corner, green lower-right corner and blue lower-left corner against a dark background. The interior should show a smooth gradient between those colors.

Resize the window. The resize callback updates the framebuffer dimensions and resizes the swap chain. The render pass establishes a viewport matching that image. Because the vertex positions remain in normalized coordinates, the triangle keeps the same fraction of the window's width and height; changing the aspect ratio can stretch its shape. Preserving an object's aspect ratio requires a projection or viewport policy, which is beyond this first draw.

### Follow the lifetime of one frame

The finished application repeats this sequence:

```text
Get the current drawable
          ↓
Transition to ColorAttachment
          ↓
Clear and draw the triangle
          ↓
Transition to Present
          ↓
Submit and wait
          ↓
Present
```

Closing the window returns from `Run`. Each rendered frame has already waited for its GPU work to complete, so we can now dispose the resources it used. The cleanup after `window.Run();` should be:

```csharp
pipeline.Dispose();
vertexBuffer.Dispose();
swapChain.Dispose();
window.Dispose();

context.Dispose();
```

Release the swap chain before its native window and the context after all of its resources. The queue owns and recycles submitted command buffers, and the swap chain owns its drawable; neither needs a separate `Dispose()` call in this application.

### Check the stage that failed

Read any error in the console first, then use the earlier checkpoints to identify the stage that failed:

| Symptom | Check |
| --- | --- |
| No window opens | Confirm the .NET project builds and the window-only step runs in a desktop session. On Linux, check that X11 or XWayland is available. |
| Backend creation fails | Check that the selected backend is supported by the device and installed graphics driver. |
| The clear-color step works, but shader loading fails | Check the copied `Triangle.slang`, entry-point spelling and the compiler's reported diagnostic. |
| The background appears but the triangle does not | Check the upload size, input layout, pipeline selection and the placement of the draw commands inside the render pass. |
| Vertex positions or colors are corrupted | Check the 28-byte stride and the position/color offsets of 0 and 12. |

For comparison, the [sample renderer](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/HelloTriangleRenderer.cs) and [Slang shader](https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/HelloTriangle.slang) use the same vertex data, input layout and draw. The sample factors windowing and submission into its shared host; on this page, you have assembled those parts in `Program.cs` as well. Continue with [Spinning Cube](samples.md#spinning-cube) for indexed geometry, transformations and depth testing.
