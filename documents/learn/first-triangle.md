---
title: '@tutorial.title'
---

<h1 id="first-triangle">
    <resource key="tutorial.title"></resource>
</h1>
<p>
    <resource key="tutorial.description"></resource>
</p>
<p>
    <resource key="tutorial.details"></resource>
</p>
<p>
    <a id="prerequisites"></a>
</p>
<h2 id="1-before-you-begin">
    1. <resource key="tutorial.requirements.title"></resource>
</h2>
<p>
    <resource key="tutorial.requirements.description">
        <slot name="emphasis"><strong><resource key="tutorial.requirements.description.emphasis"></resource></strong></slot>
    </resource>
</p>
<table>
    <thead>
        <tr>
            <th>
                <resource key="tutorial.requirements.table.headings.platform"></resource>
            </th>
            <th>
                <resource key="tutorial.requirements.table.headings.graphicsBackend"></resource>
            </th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td>
                <resource key="tutorial.requirements.table.windows.platform"></resource>
            </td>
            <td>
                <resource key="tutorial.requirements.table.windows.graphicsBackend"></resource>
            </td>
        </tr>
        <tr>
            <td>
                <resource key="tutorial.requirements.table.macOS.platform"></resource>
            </td>
            <td>
                <resource key="tutorial.requirements.table.macOS.graphicsBackend"></resource>
            </td>
        </tr>
        <tr>
            <td>
                <resource key="tutorial.requirements.table.linux.platform"></resource>
            </td>
            <td>
                <resource key="tutorial.requirements.table.linux.graphicsBackend"></resource>
            </td>
        </tr>
    </tbody>
</table>
<p>
    <resource key="tutorial.requirements.guidance"></resource>
</p>
<p>
    <a id="project"></a>
</p>
<h2 id="2-create-a-console-project">
    2. <resource key="tutorial.project.title"></resource>
</h2>
<p>
    <resource key="tutorial.project.description">
        <slot name="firstTriangle"><code>FirstTriangle</code></slot>
    </resource>
</p>
<p>
    <resource key="tutorial.project.details"></resource>
</p>
<ul>
    <li>
        <code>Silk.NET.Windowing</code>
    </li>
    <li>
        <code>Zenith.NET</code>
    </li>
    <li>
        <code>Zenith.NET.Compiler</code>
    </li>
    <li>
        <code>Zenith.NET.DirectX12</code>
    </li>
    <li>
        <code>Zenith.NET.Metal</code>
    </li>
    <li>
        <code>Zenith.NET.Vulkan</code>
    </li>
</ul>
<p>
    <resource key="tutorial.project.guidance">
        <slot name="zenithNET"><code>Zenith.NET</code></slot>
        <slot name="zenithNETCompiler"><code>Zenith.NET.Compiler</code></slot>
    </resource>
</p>
<p>
    <resource key="tutorial.project.context">
        <slot name="emphasis"><strong><resource key="tutorial.project.context.emphasis"></resource></strong></slot>
        <slot name="allowUnsafeBlocks"><code>AllowUnsafeBlocks</code></slot>
    </resource>
</p>
<p>
    <a id="window"></a>
</p>
<h2 id="3-open-a-window">
    3. <resource key="tutorial.window.title"></resource>
</h2>
<p>
    <resource key="tutorial.window.details">
        <slot name="programCs"><code>Program.cs</code></slot>
    </resource>
</p>

```csharp
using Silk.NET.Windowing;

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

<p>
    <resource key="tutorial.window.guidance">
        <slot name="graphicsAPINone"><code>GraphicsAPI.None</code></slot>
        <slot name="initialize"><code>Initialize()</code></slot>
        <slot name="center"><code>Center()</code></slot>
        <slot name="run"><code>Run()</code></slot>
        <slot name="dispose"><code>Dispose()</code></slot>
    </resource>
</p>
<p>
    <resource key="tutorial.window.notes"></resource>
</p>
<p>
    <a id="context"></a>
</p>
<h2 id="4-connect-the-gpu-to-the-window">
    4. <resource key="tutorial.presentation.title"></resource>
</h2>
<h3 id="create-the-graphics-context">
    <resource key="tutorial.context.title"></resource>
</h3>
<p>
    <resource key="tutorial.context.description">
        <slot name="graphicsContext"><a class="xref" href="xref:Zenith.NET.GraphicsContext"><code>GraphicsContext</code></a></slot>
    </resource>
</p>
<p>
    <resource key="tutorial.context.imports">
        <slot name="programCs"><code>Program.cs</code></slot>
    </resource>
</p>

```csharp
using Zenith.NET;
using Zenith.NET.DirectX12;
using Zenith.NET.Metal;
using Zenith.NET.Vulkan;
```

<p>
    <resource key="tutorial.context.details">
        <slot name="emphasis"><strong><resource key="tutorial.context.details.emphasis"><slot name="iWindowWindow"><code>IWindow window</code></slot></resource></strong></slot>
    </resource>
</p>

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

<p>
    <resource key="tutorial.context.guidance">
        <slot name="validationMessage"><a class="xref" href="xref:Zenith.NET.GraphicsContext.ValidationMessage"><code>ValidationMessage</code></a></slot>
    </resource>
</p>
<h3 id="describe-the-native-surface">
    <resource key="tutorial.surface.title"></resource>
</h3>
<p>
    <resource key="tutorial.window.description">
        <slot name="cocoaHelperCs"><a href="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/CocoaHelper.cs">CocoaHelper.cs</a></slot>
    </resource>
</p>
<p>
    <resource key="tutorial.surface.description">
        <slot name="emphasis"><strong><resource key="tutorial.surface.description.emphasis"><slot name="windowCenter"><code>window.Center()</code></slot>
        <slot name="windowRun"><code>window.Run()</code></slot></resource></strong></slot>
    </resource>
</p>

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

<p>
    <resource key="tutorial.surface.details">
        <slot name="surface"><a class="xref" href="xref:Zenith.NET.Surface"><code>Surface</code></a></slot>
        <slot name="framebufferSize"><code>FramebufferSize</code></slot>
    </resource>
</p>
<p>
    <resource key="tutorial.surface.guidance"></resource>
</p>
<h3 id="create-the-swap-chain">
    <resource key="tutorial.swapChain.title"></resource>
</h3>
<p>
    <resource key="tutorial.swapChain.description"></resource>
</p>

```csharp
SwapChain swapChain = context.CreateSwapChain(new()
{
    Surface = surface,
    Format = PixelFormat.B8G8R8A8UNorm
});
```

<p>
    <resource key="tutorial.swapChain.details">
        <slot name="swapChain"><a class="xref" href="xref:Zenith.NET.SwapChain"><code>SwapChain</code></a></slot>
        <slot name="drawable"><a class="xref" href="xref:Zenith.NET.SwapChain.Drawable"><code>Drawable</code></a></slot>
        <slot name="texture"><a class="xref" href="xref:Zenith.NET.Texture"><code>Texture</code></a></slot>
        <slot name="present"><a class="xref" href="xref:Zenith.NET.SwapChain.Present"><code>Present()</code></a></slot>
    </resource>
</p>
<p>
    <resource key="tutorial.swapChain.guidance">
        <slot name="b8G8R8A8UNorm"><a class="xref" href="xref:Zenith.NET.PixelFormat.B8G8R8A8UNorm"><code>B8G8R8A8UNorm</code></a></slot>
        <slot name="uNorm"><code>UNorm</code></slot>
    </resource>
</p>
<p>
    <resource key="tutorial.swapChain.context">
        <slot name="windowDispose"><code>window.Dispose();</code></slot>
        <slot name="programCs"><code>Program.cs</code></slot>
    </resource>
</p>

```csharp
swapChain.Dispose();
window.Dispose();

context.Dispose();
```

<p>
    <a id="frame-loop"></a>
</p>
<h2 id="5-display-a-background-color">
    5. <resource key="tutorial.frame.title"></resource>
</h2>
<p>
    <resource key="tutorial.frame.description"></resource>
</p>
<p>
    <resource key="tutorial.frame.details">
        <slot name="emphasis"><strong><resource key="tutorial.frame.details.emphasis"><slot name="windowRun"><code>window.Run()</code></slot></resource></strong></slot>
    </resource>
</p>

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

<h3 id="prepare-the-drawable">
    <resource key="tutorial.frame.target.title"></resource>
</h3>
<p>
    <resource key="tutorial.frame.target.description">
        <slot name="graphicsQueueCommandBuffer"><code><a class="code-reference" href="xref:Zenith.NET.GraphicsContext.GraphicsQueue">GraphicsQueue</a>.<a class="code-reference" href="xref:Zenith.NET.CommandQueue.CommandBuffer">CommandBuffer</a>()</code></slot>
    </resource>
</p>
<p>
    <resource key="tutorial.frame.target.details">
        <slot name="transition"><a class="xref" href="xref:Zenith.NET.CommandBuffer.Transition(Zenith.NET.Texture,Zenith.NET.TextureSubresource,Zenith.NET.TextureLayout,Zenith.NET.TextureLayout)"><code>Transition</code></a></slot>
        <slot name="default"><code>default</code></slot>
        <slot name="undefined"><a class="xref" href="xref:Zenith.NET.TextureLayout.Undefined"><code>Undefined</code></a></slot>
    </resource>
</p>
<p>
    <resource key="tutorial.frame.target.guidance"></resource>
</p>
<h3 id="clear-submit-and-present">
    <resource key="tutorial.frame.commands.title"></resource>
</h3>
<p>
    <resource key="tutorial.frame.commands.description">
        <slot name="colorAttachmentClear"><a class="xref" href="xref:Zenith.NET.ColorAttachment.Clear(Zenith.NET.Texture,System.Numerics.Vector4)"><code>ColorAttachment.Clear</code></a></slot>
        <slot name="beginRenderPass"><a class="xref" href="xref:Zenith.NET.CommandBuffer.BeginRenderPass(System.ReadOnlySpan{Zenith.NET.ColorAttachment},System.Nullable{Zenith.NET.DepthStencilAttachment})"><code>BeginRenderPass</code></a></slot>
    </resource>
</p>
<p>
    <resource key="tutorial.frame.commands.details">
        <slot name="null"><code>null</code></slot>
    </resource>
</p>
<p>
    <resource key="tutorial.frame.commands.guidance">
        <slot name="beginRenderPass"><a class="xref" href="xref:Zenith.NET.CommandBuffer.BeginRenderPass(System.ReadOnlySpan{Zenith.NET.ColorAttachment},System.Nullable{Zenith.NET.DepthStencilAttachment})"><code>BeginRenderPass</code></a></slot>
    </resource>
</p>
<p>
    <resource key="tutorial.frame.commands.context">
        <slot name="endRenderPass"><a class="xref" href="xref:Zenith.NET.CommandBuffer.EndRenderPass"><code>EndRenderPass</code></a></slot>
        <slot name="submit"><a class="xref" href="xref:Zenith.NET.CommandBuffer.Submit(System.ReadOnlySpan{Zenith.NET.TimelineValue})"><code>Submit()</code></a></slot>
        <slot name="wait"><a class="xref" href="xref:Zenith.NET.TimelineValue.Wait"><code>Wait()</code></a></slot>
        <slot name="present"><a class="xref" href="xref:Zenith.NET.SwapChain.Present"><code>Present()</code></a></slot>
    </resource>
</p>
<h3 id="respond-to-resizing">
    <resource key="tutorial.resize.title"></resource>
</h3>
<p>
    <resource key="tutorial.resize.description">
        <slot name="framebufferResize"><code>FramebufferResize</code></slot>
        <slot name="windowRun"><code>window.Run()</code></slot>
    </resource>
</p>

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

<p>
    <resource key="tutorial.resize.details">
        <slot name="link"><a href="https://github.com/dotnet/Silk.NET/blob/main/src/Windowing/Silk.NET.Windowing.Common/Interfaces/IView.cs"><resource key="tutorial.resize.details.link"></resource></a></slot>
    </resource>
</p>
<p>
    <resource key="tutorial.resize.guidance">
        <slot name="present"><a class="xref" href="xref:Zenith.NET.SwapChain.Present"><code>Present()</code></a></slot>
        <slot name="link"><a href="~/learn/concepts/synchronization.md#cpu-and-gpu"><resource key="tutorial.resize.guidance.link"></resource></a></slot>
    </resource>
</p>
<p>
    <resource key="tutorial.resize.context"></resource>
</p>
<p>
    <a id="resources"></a>
</p>
<h2 id="6-give-the-gpu-three-vertices">
    6. <resource key="tutorial.geometry.title"></resource>
</h2>
<p>
    <resource key="tutorial.geometry.description"></resource>
</p>
<h3 id="define-a-vertex">
    <resource key="tutorial.geometry.format.title"></resource>
</h3>
<p>
    <resource key="tutorial.geometry.format.imports">
        <slot name="programCs"><code>Program.cs</code></slot>
    </resource>
</p>

```csharp
using System.Numerics;
using System.Runtime.InteropServices;
```

<p>
    <resource key="tutorial.geometry.format.description">
        <slot name="emphasis"><strong><resource key="tutorial.geometry.format.description.emphasis"><slot name="programCs"><code>Program.cs</code></slot></resource></strong></slot>
    </resource>
</p>

```csharp
[StructLayout(LayoutKind.Sequential)]
file struct Vertex(Vector3 position, Vector4 color)
{
    public Vector3 Position = position;

    public Vector4 Color = color;
}
```

<p>
    <resource key="tutorial.geometry.format.details">
        <slot name="layoutKindSequential"><code>LayoutKind.Sequential</code></slot>
        <slot name="position"><code>Position</code></slot>
        <slot name="color"><code>Color</code></slot>
    </resource>
</p>
<h3 id="choose-the-corners">
    <resource key="tutorial.geometry.data.title"></resource>
</h3>
<p>
    <resource key="tutorial.geometry.data.description">
        <slot name="emphasis"><strong><resource key="tutorial.geometry.data.description.emphasis"><slot name="windowRender"><code>window.Render += _ =&gt;</code></slot></resource></strong></slot>
    </resource>
</p>

```csharp
Vertex[] vertices =
[
    new(new(0.0f, 0.6f, 0.0f), new(1.0f, 0.2f, 0.15f, 1.0f)),
    new(new(0.6f, -0.5f, 0.0f), new(0.15f, 0.85f, 0.35f, 1.0f)),
    new(new(-0.6f, -0.5f, 0.0f), new(0.2f, 0.45f, 1.0f, 1.0f))
];
```

<p>
    <resource key="tutorial.geometry.data.details"></resource>
</p>
<p>
    <resource key="tutorial.geometry.data.guidance"></resource>
</p>
<h3 id="allocate-a-buffer-and-upload-the-array">
    <resource key="tutorial.geometry.upload.title"></resource>
</h3>
<p>
    <resource key="tutorial.geometry.upload.imports">
        <slot name="programCs"><code>Program.cs</code></slot>
        <slot name="bufferAlias"><code>using Buffer = <a class="code-reference" href="xref:Zenith.NET.Buffer">Zenith.NET.Buffer</a>;</code></slot>
        <slot name="systemBuffer"><code>System.Buffer</code></slot>
    </resource>
</p>
<p>
    <resource key="tutorial.geometry.upload.description">
        <slot name="buffer"><a class="xref" href="xref:Zenith.NET.Buffer"><code>Buffer</code></a></slot>
    </resource>
</p>

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

<p>
    <resource key="tutorial.geometry.upload.details">
        <slot name="sizeofVertexVerticesLength"><code>sizeof(Vertex) * vertices.Length</code></slot>
        <slot name="bufferUsagesVertex"><a class="xref" href="xref:Zenith.NET.BufferUsages.Vertex"><code>BufferUsages.Vertex</code></a></slot>
        <slot name="cpuWriteOnly"><a class="xref" href="xref:Zenith.NET.MemoryResidency.CpuWriteOnly"><code>CpuWriteOnly</code></a></slot>
    </resource>
</p>
<p>
    <resource key="tutorial.geometry.upload.guidance">
        <slot name="upload"><a class="xref" href="xref:Zenith.NET.Buffer.Upload(System.UInt32,Zenith.NET.BufferData)"><code>Upload</code></a></slot>
    </resource>
</p>
<p>
    <resource key="tutorial.geometry.upload.context">
        <slot name="fixed"><code>fixed</code></slot>
        <slot name="upload"><a class="xref" href="xref:Zenith.NET.Buffer.Upload(System.UInt32,Zenith.NET.BufferData)"><code>Upload</code></a></slot>
    </resource>
</p>
<p>
    <resource key="tutorial.geometry.upload.notes">
        <slot name="vertexBufferDispose"><code>vertexBuffer.<a class="code-reference" href="xref:Zenith.NET.DisposableObject.Dispose">Dispose</a>();</code></slot>
        <slot name="swapChainDispose"><code>swapChain.<a class="code-reference" href="xref:Zenith.NET.DisposableObject.Dispose">Dispose</a>();</code></slot>
        <slot name="programCs"><code>Program.cs</code></slot>
    </resource>
</p>
<p>
    <a id="shader"></a>
</p>
<h2 id="7-write-the-two-shader-stages">
    7. <resource key="tutorial.shaders.title"></resource>
</h2>
<p>
    <resource key="tutorial.shaders.description"></resource>
</p>
<p>
    <resource key="tutorial.shaders.details">
        <slot name="triangleSlang"><code>Triangle.slang</code></slot>
    </resource>
</p>

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

<p>
    <resource key="tutorial.shaders.guidance">
        <slot name="vSInput"><code>VSInput</code></slot>
        <slot name="pOSITION0"><code>POSITION0</code></slot>
        <slot name="cOLOR0"><code>COLOR0</code></slot>
    </resource>
</p>
<p>
    <resource key="tutorial.shaders.context">
        <slot name="fSInput"><code>FSInput</code></slot>
        <slot name="sVPOSITION"><code>SV_POSITION</code></slot>
        <slot name="cOLOR0"><code>COLOR0</code></slot>
    </resource>
</p>
<h3 id="position-each-vertex">
    <resource key="tutorial.shaders.vertex.title"></resource>
</h3>
<p>
    <resource key="tutorial.shaders.vertex.description">
        <slot name="triangleSlang"><code>Triangle.slang</code></slot>
    </resource>
</p>

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

<p>
    <resource key="tutorial.shaders.vertex.details">
        <slot name="w"><code>w = 1</code></slot>
    </resource>
</p>
<h3 id="color-the-fragments">
    <resource key="tutorial.shaders.fragment.title"></resource>
</h3>
<p>
    <resource key="tutorial.shaders.fragment.description"></resource>
</p>

```slang
[shader("fragment")]
float4 FSMain(FSInput input) : SV_TARGET
{
    return input.Color;
}
```

<p>
    <resource key="tutorial.shaders.fragment.details">
        <slot name="fSMain"><code>FSMain</code></slot>
        <slot name="sVTARGET"><code>SV_TARGET</code></slot>
    </resource>
</p>
<h3 id="compile-for-the-selected-backend">
    <resource key="tutorial.shaders.compilation.title"></resource>
</h3>
<p>
    <resource key="tutorial.shaders.compilation.description">
        <slot name="triangleSlang"><code>Triangle.slang</code></slot>
        <slot name="emphasis"><strong><resource key="tutorial.shaders.compilation.description.emphasis"></resource></strong></slot>
        <slot name="detail"><strong><resource key="tutorial.shaders.compilation.description.detail"></resource></strong></slot>
    </resource>
</p>
<p>
    <resource key="tutorial.shaders.compilation.details">
        <slot name="programCs"><code>Program.cs</code></slot>
    </resource>
</p>

```csharp
string shaderPath = Path.Combine(AppContext.BaseDirectory, "Triangle.slang");

Shader vertexShader = context.CreateShader(ZenithCompiler.CompileFromFile(context.GraphicsApi, shaderPath, "VSMain"));
Shader fragmentShader = context.CreateShader(ZenithCompiler.CompileFromFile(context.GraphicsApi, shaderPath, "FSMain"));
```

<p>
    <resource key="tutorial.shaders.compilation.guidance">
        <slot name="zenithCompiler"><a class="xref" href="xref:Zenith.NET.ZenithCompiler"><code>ZenithCompiler</code></a></slot>
        <slot name="createShader"><a class="xref" href="xref:Zenith.NET.GraphicsContext.CreateShader(Zenith.NET.ShaderDesc)"><code>CreateShader</code></a></slot>
        <slot name="contextGraphicsApi"><code>context.<a class="code-reference" href="xref:Zenith.NET.GraphicsContext.GraphicsApi">GraphicsApi</a></code></slot>
        <slot name="vSMain"><code>VSMain</code></slot>
        <slot name="fSMain"><code>FSMain</code></slot>
    </resource>
</p>
<p>
    <resource key="tutorial.shaders.compilation.context">
        <slot name="appContextBaseDirectory"><code>AppContext.BaseDirectory</code></slot>
    </resource>
</p>
<p>
    <a id="vertex-layout"></a>
</p>
<h2 id="8-describe-the-vertex-input-layout">
    8. <resource key="tutorial.input.title"></resource>
</h2>
<p>
    <resource key="tutorial.input.description"></resource>
</p>

```csharp
InputLayout inputLayout = new();
inputLayout.Add(new() { Format = ElementFormat.Float3, Semantic = ElementSemantic.Position });
inputLayout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Color });
```

<p>
    <resource key="tutorial.input.details">
        <slot name="inputLayoutAdd"><a class="xref" href="xref:Zenith.NET.InputLayout.Add(Zenith.NET.InputElement)"><code>InputLayout.Add</code></a></slot>
    </resource>
</p>
<table>
    <thead>
        <tr>
            <th>
                <resource key="tutorial.input.table.headings.cField"></resource>
            </th>
            <th>
                <resource key="tutorial.input.table.headings.byteOffset"></resource>
            </th>
            <th>
                <resource key="tutorial.input.table.headings.format"></resource>
            </th>
            <th>
                <resource key="tutorial.input.table.headings.shaderInput"></resource>
            </th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td>
                <code>Vertex.Position</code>
            </td>
            <td>0</td>
            <td>
                <resource key="tutorial.input.table.vertexPosition.format">
                    <slot name="float3"><a class="xref" href="xref:Zenith.NET.ElementFormat.Float3"><code>Float3</code></a></slot>
                </resource>
            </td>
            <td>
                <code>POSITION0</code>
            </td>
        </tr>
        <tr>
            <td>
                <code>Vertex.Color</code>
            </td>
            <td>12</td>
            <td>
                <resource key="tutorial.input.table.vertexColor.format">
                    <slot name="float4"><a class="xref" href="xref:Zenith.NET.ElementFormat.Float4"><code>Float4</code></a></slot>
                </resource>
            </td>
            <td>
                <code>COLOR0</code>
            </td>
        </tr>
        <tr>
            <td>
                <resource key="tutorial.input.table.nextVertex.cField"></resource>
            </td>
            <td>28</td>
            <td>
                <resource key="tutorial.input.table.nextVertex.format"></resource>
            </td>
            <td>
                <resource key="tutorial.input.table.nextVertex.shaderInput"></resource>
            </td>
        </tr>
    </tbody>
</table>
<p>
    <resource key="tutorial.input.guidance">
        <slot name="semanticIndex"><a class="xref" href="xref:Zenith.NET.InputElement.SemanticIndex"><code>SemanticIndex</code></a></slot>
        <slot name="pOSITION0"><code>POSITION0</code></slot>
        <slot name="cOLOR0"><code>COLOR0</code></slot>
    </resource>
</p>
<p>
    <a id="pipeline"></a>
</p>
<h2 id="9-connect-the-shaders-and-vertex-layout">
    9. <resource key="tutorial.pipeline.title"></resource>
</h2>
<p>
    <resource key="tutorial.pipeline.description">
        <slot name="graphicsPipeline"><a class="xref" href="xref:Zenith.NET.GraphicsPipeline"><code>GraphicsPipeline</code></a></slot>
    </resource>
</p>
<ul>
    <li>
        <resource key="tutorial.pipeline.item">
            <slot name="triangleList"><a class="xref" href="xref:Zenith.NET.PrimitiveTopology.TriangleList"><code>TriangleList</code></a></slot>
        </resource>
    </li>
    <li>
        <resource key="tutorial.pipeline.guidance">
            <slot name="swapChainDescFormat"><code>swapChain.<a class="code-reference" href="xref:Zenith.NET.SwapChain.Desc">Desc</a>.<a class="code-reference" href="xref:Zenith.NET.SwapChainDesc.Format">Format</a></code></slot>
            <slot name="count1"><a class="xref" href="xref:Zenith.NET.SampleCount.Count1"><code>Count1</code></a></slot>
        </resource>
    </li>
    <li>
        <resource key="tutorial.pipeline.context">
            <slot name="cullNone"><a class="xref" href="xref:Zenith.NET.RasterizerState.CullNone"><code>CullNone()</code></a></slot>
        </resource>
    </li>
    <li>
        <resource key="tutorial.pipeline.notes">
            <slot name="depthNone"><a class="xref" href="xref:Zenith.NET.DepthStencilState.DepthNone"><code>DepthNone()</code></a></slot>
        </resource>
    </li>
    <li>
        <resource key="tutorial.pipeline.reference">
            <slot name="opaque"><a class="xref" href="xref:Zenith.NET.BlendState.Opaque"><code>Opaque()</code></a></slot>
        </resource>
    </li>
</ul>
<p>
    <resource key="tutorial.pipeline.behavior"></resource>
</p>

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

<p>
    <resource key="tutorial.pipeline.usage">
        <slot name="pipelineDispose"><code>pipeline.<a class="code-reference" href="xref:Zenith.NET.DisposableObject.Dispose">Dispose</a>();</code></slot>
        <slot name="vertexBufferDispose"><code>vertexBuffer.<a class="code-reference" href="xref:Zenith.NET.DisposableObject.Dispose">Dispose</a>();</code></slot>
    </resource>
</p>
<p>
    <a id="draw"></a>
</p>
<h2 id="10-draw-the-triangle-inside-the-pass">
    10. <resource key="tutorial.draw.title"></resource>
</h2>
<p>
    <resource key="tutorial.draw.description">
        <slot name="windowRender"><code>window.Render</code></slot>
        <slot name="addTheTriangleDrawCommandsHereLater"><code>// Add the triangle draw commands here later.</code></slot>
        <slot name="beginRenderPass"><a class="xref" href="xref:Zenith.NET.CommandBuffer.BeginRenderPass(System.ReadOnlySpan{Zenith.NET.ColorAttachment},System.Nullable{Zenith.NET.DepthStencilAttachment})"><code>BeginRenderPass</code></a></slot>
        <slot name="endRenderPass"><a class="xref" href="xref:Zenith.NET.CommandBuffer.EndRenderPass"><code>EndRenderPass</code></a></slot>
    </resource>
</p>

```csharp
commandBuffer.SetPipeline(pipeline);
commandBuffer.SetVertexBuffer(vertexBuffer, 0, 0);

commandBuffer.Draw(3, 1, 0, 0);
```

<p>
    <resource key="tutorial.draw.details">
        <slot name="setPipeline"><a class="xref" href="xref:Zenith.NET.CommandBuffer.SetPipeline(Zenith.NET.GraphicsPipeline)"><code>SetPipeline</code></a></slot>
    </resource>
</p>
<p>
    <resource key="tutorial.draw.guidance">
        <slot name="setVertexBufferVertexBuffer"><code><a class="code-reference" href="xref:Zenith.NET.CommandBuffer.SetVertexBuffer(Zenith.NET.Buffer,System.UInt32,System.UInt32)">SetVertexBuffer</a>(vertexBuffer, 0, 0)</code></slot>
        <slot name="inputLayouts"><a class="xref" href="xref:Zenith.NET.GraphicsPipelineDesc.InputLayouts"><code>InputLayouts</code></a></slot>
    </resource>
</p>
<p>
    <resource key="tutorial.draw.context">
        <slot name="draw"><a class="xref" href="xref:Zenith.NET.CommandBuffer.Draw(System.UInt32,System.UInt32,System.UInt32,System.UInt32)"><code>Draw</code></a></slot>
    </resource>
</p>
<table>
    <thead>
        <tr>
            <th>
                <resource key="tutorial.draw.table.headings.argument"></resource>
            </th>
            <th>
                <resource key="tutorial.draw.table.headings.description"></resource>
            </th>
            <th>
                <resource key="tutorial.draw.table.headings.meaning"></resource>
            </th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td>
                <code>vertexCount</code>
            </td>
            <td>3</td>
            <td>
                <resource key="tutorial.draw.table.vertexCount.meaning"></resource>
            </td>
        </tr>
        <tr>
            <td>
                <code>instanceCount</code>
            </td>
            <td>1</td>
            <td>
                <resource key="tutorial.draw.table.instanceCount.meaning"></resource>
            </td>
        </tr>
        <tr>
            <td>
                <code>firstVertex</code>
            </td>
            <td>0</td>
            <td>
                <resource key="tutorial.draw.table.firstVertex.meaning"></resource>
            </td>
        </tr>
        <tr>
            <td>
                <code>firstInstance</code>
            </td>
            <td>0</td>
            <td>
                <resource key="tutorial.draw.table.firstInstance.meaning"></resource>
            </td>
        </tr>
    </tbody>
</table>
<p>
    <resource key="tutorial.draw.notes"></resource>
</p>
<p>
    <a id="run"></a>
</p>
<h2 id="11-run-resize-and-close">
    11. <resource key="tutorial.execution.title"></resource>
</h2>
<p>
    <resource key="tutorial.execution.description">
        <slot name="firstTriangle"><code>FirstTriangle</code></slot>
    </resource>
</p>

<p>
    <resource key="tutorial.execution.details"></resource>
</p>
<p>
    <resource key="tutorial.execution.guidance"></resource>
</p>
<p id="follow-the-lifetime-of-one-frame">
    <resource key="tutorial.lifecycle.description"></resource>
</p>

<p>
    <resource key="tutorial.lifecycle.details">
        <slot name="run"><code>Run</code></slot>
        <slot name="windowRun"><code>window.Run()</code></slot>
    </resource>
</p>

```csharp
pipeline.Dispose();
vertexBuffer.Dispose();
swapChain.Dispose();
window.Dispose();

context.Dispose();
```

<p>
    <resource key="tutorial.lifecycle.guidance">
        <slot name="dispose"><a class="xref" href="xref:Zenith.NET.DisposableObject.Dispose"><code>Dispose()</code></a></slot>
    </resource>
</p>
