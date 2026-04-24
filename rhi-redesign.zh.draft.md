# RHI 重设计草案 — 工作稿

> 下一版公共面的工作草案，不进入提交。英文同步版见 `rhi-redesign.draft.md`。
> 本稿依据当前 `Zenith.NET`、`Extensions` 与 `Views` 目录重新整理。验证层设计明确延后处理。
> 文档仍按 9 条需求组织，共享约定放在 §0。

## 0. 公共约定

### 0.1 命名与代码风格

- 所有公共值类型一律为 `record struct`，公共字段可写。
- 多元素输入使用 `ReadOnlySpan<T>` / `params ReadOnlySpan<T>`。低层公共面不使用 `T[]`、`IEnumerable<T>`、`IReadOnlyList<T>`。
- 所有字节单位字段与参数统一使用 `*InBytes` 后缀。
- 后端钩子保持普通 `protected abstract` 形式；其上的包装层只负责校验或便利调用。
- 只要不遮蔽低层模型，就可以保留便利包装，例如时间线提交接口之上的 `Submit(bool waitForCompletion = false)`。

### 0.2 公共面边界

- `BufferView` 继续移除，buffer 子范围统一使用 `BufferRange` 表达。
- `TextureView` 继续作为长生命周期 `GraphicsResource`，由 `GraphicsContext.CreateTextureView(...)` 创建。
- `ResourceTable` 继续作为长生命周期 `GraphicsResource`，通过显式类型化 `Write(...)` 重载写入资源。
- 公共面没有 `FrameBuffer` / `RenderPassInfo` 对象；RenderPass 保持内联表达。
- CPU 上传与回读统一由 `BufferData`、`TextureData`、`TextureDataLayout` 描述。
- Views 层统一向上层暴露 `Texture target`；具体平台 view 自己决定该 target 是 swapchain backbuffer 还是 CPU 回读纹理。
- 本轮仅补齐缺失的低层同步能力：显式 texture layout 转换、面向读写 hazard 的轻量 `PipelineBarrier`、queue 时间线，以及完整的 `NativeObjectType`。
- Residency、heap 管理、验证层重设计继续留在范围之外。

### 0.3 核心对象骨架

```csharp
public abstract class GraphicsContext : DisposableObject, INativeObject
{
    public Backend Backend { get; }

    public Capabilities Capabilities { get; }

    public CommandQueue Graphics { get; }

    public CommandQueue Compute { get; }

    public CommandQueue Copy { get; }

    public SwapChain CreateSwapChain(SwapChainDesc desc);

    public Buffer CreateBuffer(BufferDesc desc);

    public Texture CreateTexture(TextureDesc desc);

    public TextureView CreateTextureView(TextureViewDesc desc);

    public ResourceTable CreateResourceTable(ResourceTableDesc desc);

    public abstract nint GetNativeObject(NativeObjectType type);
}

public abstract class GraphicsResource(GraphicsContext context) : DisposableObject, INativeObject
{
    public GraphicsContext Context { get; } = context;

    public abstract nint GetNativeObject(NativeObjectType type);
}

public abstract class Buffer(GraphicsContext context, BufferDesc desc) : GraphicsResource(context)
{
    public ref readonly BufferDesc Desc => ref desc;

    public abstract MappedMemory Map();

    public abstract void Unmap();

    public void Upload(uint offsetInBytes, BufferData data);

    public void Download(uint offsetInBytes, BufferData data);
}

public abstract class Texture(GraphicsContext context, TextureDesc desc) : GraphicsResource(context)
{
    public ref readonly TextureDesc Desc => ref desc;

    public void Upload(TextureSubresource subresource, Offset3D offset, Extent3D extent, TextureData data);

    public void Download(TextureSubresource subresource, Offset3D offset, Extent3D extent, TextureData data);
}
```

### 0.4 显式 CPU 数据描述

```csharp
public record struct BufferData
{
    public nint Pointer;

    public uint SizeInBytes;
}

public record struct TextureData
{
    public nint Pointer;

    public TextureDataLayout Layout;
}

public record struct TextureDataLayout
{
    public uint SizeInBytes;

    public uint RowPitchInBytes;

    public uint SlicePitchInBytes;
}
```

设计要点：
- `TextureDataLayout` 是纯布局描述，不携带 offset。
- 字节偏移保留在 copy 接口上，例如 `CopyBufferToTexture(..., uint srcOffsetInBytes, TextureDataLayout srcLayout, ...)`。
- 紧凑布局可以通过 `ZenithHelper` 计算，但最终传入的 `SizeInBytes` / `RowPitchInBytes` / `SlicePitchInBytes` 仍由调用方负责。
- 后端特有的 copy 对齐继续下沉到各后端内部，不再抬回 `GraphicsContext`。

---

## 1. `ReadOnlySpan` 作为多元素边界

需求：所有多元素公共输入继续统一使用 `ReadOnlySpan<T>` / `params ReadOnlySpan<T>`。

代表性接口：

| 接口面 | 形状 |
|---|---|
| RenderPass 颜色附件 | `ReadOnlySpan<ColorAttachment>` |
| Viewport / Scissor | `ReadOnlySpan<Viewport>` / `ReadOnlySpan<Scissor>` |
| ResourceTable 数组写入 | `ReadOnlySpan<Buffer>` / `ReadOnlySpan<BufferRange>` / `ReadOnlySpan<Texture>` / `ReadOnlySpan<TextureView>` / `ReadOnlySpan<Sampler>` / `ReadOnlySpan<TopLevelAccelerationStructure>` |
| 时间线等待集 | `params ReadOnlySpan<CommandSubmission>` |
| 批量转换 | `ReadOnlySpan<TextureTransition>` |

理由：
- 同时接受栈上、数组以及单元素输入，调用形态统一。
- 与当前 `BeginRenderPass`、`SetScissors`、`SetViewports`、`ResourceTable.Write` 家族保持一致。
- 让未来的时间线接口和转换接口在调用现场保持零托管分配。

例外：
- 返回多个元素时，`ReadOnlySpan<T>` 仍然要求一个显式持有者。只要能避免隐藏生命周期耦合，返回具体类型依然可以接受。

---

## 2. 显式 Upload / Download / Copy 模型

需求：CPU 数据搬运继续保持描述符化，公共 texture 上传接口不回退到 `ReadOnlySpan<T>` 风格。

```csharp
public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    public void Upload(Buffer buffer, uint offsetInBytes, BufferData data);

    public void Download(Buffer buffer, uint offsetInBytes, BufferData data);

    public void Upload(Texture texture, TextureSubresource subresource, Offset3D offset, Extent3D extent, TextureData data);

    public void Download(Texture texture, TextureSubresource subresource, Offset3D offset, Extent3D extent, TextureData data);

    public void CopyBuffer(Buffer src, uint srcOffsetInBytes, Buffer dest, uint destOffsetInBytes, uint sizeInBytes);

    public void CopyBufferToTexture(Buffer src, uint srcOffsetInBytes, TextureDataLayout srcLayout, Texture dest, TextureSubresource destSubresource, Offset3D destOffset, Extent3D destExtent);

    public void CopyTexture(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Texture dest, TextureSubresource destSubresource, Offset3D destOffset, Extent3D extent);

    public void CopyTextureToBuffer(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Extent3D srcExtent, Buffer dest, uint destOffsetInBytes, TextureDataLayout destLayout);
}
```

设计要点：
- `Buffer.Upload` / `Buffer.Download` 在 buffer 可 CPU 访问时直接走 `Map()` / `Unmap()`；否则通过 `Context.Copy` staging。
- `Texture.Upload` / `Texture.Download` 始终通过 `CommandBuffer` 完成，以保持 copy 路径显式且可由后端控制。
- offset 与 pitch 故意分离。`TextureDataLayout` 只描述内存布局，`offsetInBytes` 决定 copy 在 staging / 目标 buffer 中从哪里开始。
- 当前 `Extensions.ImageSharp` 与 `Views.Avalonia.Surface` 已经在按这套模型使用。

---

## 3. 子资源、偏移、尺寸与 BufferRange 值类型

需求：子资源与范围相关输入保持为小型值类型，并与当前 copy / render 接口对齐。

```csharp
public record struct TextureSubresource
{
    public uint MipLevel;

    public uint ArrayLayer;
}

public record struct TextureSubresourceLayers
{
    public uint MipLevel;

    public uint BaseArrayLayer;

    public uint LayerCount;
}

public record struct TextureSubresourceRange
{
    public uint BaseMipLevel;

    public uint LevelCount;

    public uint BaseArrayLayer;

    public uint LayerCount;
}

public record struct Offset3D
{
    public uint X;

    public uint Y;

    public uint Z;
}

public record struct Extent3D
{
    public uint Width;

    public uint Height;

    public uint Depth;
}

public record struct BufferRange
{
    public Buffer Buffer;

    public ulong OffsetInBytes;

    public ulong SizeInBytes;

    public uint StrideInBytes;

    public static implicit operator BufferRange(Buffer buffer)
    {
        return new()
        {
            Buffer = buffer,
            OffsetInBytes = 0,
            SizeInBytes = buffer.Desc.SizeInBytes,
            StrideInBytes = buffer.Desc.StrideInBytes
        };
    }
}
```

约定：
- 公共面不携带显式 aspect 标记，后端从 `Texture.Desc.Format` 推断 color / depth / stencil。
- Cube face 继续通过 `ArrayLayer = cubeIndex * 6 + face` 线性化。
- `BufferRange` 是唯一公共 buffer 子范围形态，不再恢复 `BufferView` 工厂。

---

## 4. TextureView 继续作为长生命周期资源

需求：`TextureView` 保持为独立持有的资源对象。旧草案里“仅作为调用现场值”的方向已经不再符合当前绑定模型和扩展层用法。

```csharp
public record struct TextureViewDesc
{
    public Texture Texture;

    public TextureType Type;

    public PixelFormat Format;

    public TextureSubresourceRange Range;
}

public abstract class TextureView(GraphicsContext context, TextureViewDesc desc) : GraphicsResource(context)
{
    public ref readonly TextureViewDesc Desc => ref desc;
}
```

设计要点：
- `GraphicsContext.CreateTextureView(...)` 继续保留在公共面上。
- 直接绑定 `Texture` 仍然是默认的整资源路径。
- 绑定 `TextureView` 只用于显式子资源、维度切换或格式重解释。
- 这与当前 `ImGui` 渲染器一致：它会长期持有 view 对象，并通过 `ResourceTable` 绑定。
- 后端内部可以让 `Texture` 自带默认 native view，但显式 `TextureView` 仍然是公共逃生口。

---

## 5. 平铺绑定模型：`ResourceBinding[]` + `ResourceTable`

需求：绑定模型保持扁平且具体：`ResourceBinding[]` 描述槽位，`ResourceTable` 保存资源值，`Write(...)` 继续按资源种类分类型重载。

```csharp
public enum ResourceType
{
    ConstantBuffer,
    StructuredBuffer,
    StructuredBufferReadWrite,
    Texture,
    TextureReadWrite,
    Sampler,
    AccelerationStructure
}

public record struct ResourceBinding
{
    public ResourceType Type;

    public uint Count;
}

public record struct ResourceTableDesc
{
    public ResourceBinding[] Bindings;
}

public abstract class ResourceTable(GraphicsContext context, ResourceTableDesc desc) : GraphicsResource(context)
{
    public ref readonly ResourceTableDesc Desc => ref desc;

    public abstract void Write(uint binding, Buffer buffer);

    public abstract void Write(uint binding, BufferRange bufferRange);

    public abstract void Write(uint binding, Texture texture);

    public abstract void Write(uint binding, TextureView textureView);

    public abstract void Write(uint binding, Sampler sampler);

    public abstract void Write(uint binding, TopLevelAccelerationStructure topLevelAccelerationStructure);

    public abstract void Write(uint binding, ReadOnlySpan<Buffer> buffers);

    public abstract void Write(uint binding, ReadOnlySpan<BufferRange> bufferRanges);

    public abstract void Write(uint binding, ReadOnlySpan<Texture> textures);

    public abstract void Write(uint binding, ReadOnlySpan<TextureView> textureViews);

    public abstract void Write(uint binding, ReadOnlySpan<Sampler> samplers);

    public abstract void Write(uint binding, ReadOnlySpan<TopLevelAccelerationStructure> topLevelAccelerationStructures);
}
```

CommandBuffer 侧入口继续保持简洁：

```csharp
public void SetPipeline(GraphicsPipeline pipeline);

public void SetPipeline(ComputePipeline pipeline);

public void SetPipeline(MeshShadingPipeline pipeline);

public void PushResourceTable(ResourceTable resourceTable);
```

设计要点：
- 没有单独的 `ResourceLayout` 对象。
- 没有 `IBindableResource` 多态入口。
- Pipeline 描述里的 `ResourceBindings` 与 `ResourceTableDesc` 里的 `Bindings` 故意保持同一扁平形状。
- `Texture` 与 `TextureView` 都继续是一等写入目标，因为“默认整资源绑定”和“显式 view 绑定”在当前代码里都是真实存在的需求。
- 先完成所有 `Write(...)`，再调用 `PushResourceTable(...)`；后端在 push 时按当前 pipeline 绑定这张表的当前内容。
- 该模型可以自然映射到 DX12 descriptor table、Vulkan push descriptor，以及 Metal encoder 侧的资源表绑定。

---

## 6. 内联 RenderPass、Output、SwapChain 目标与 Views 集成

需求：render target 继续保持内联表达，Views 层继续保持“渲染到一个 `Texture target`”的契约。

```csharp
public record struct Output
{
    public PixelFormat[] ColorAttachments;

    public PixelFormat? DepthStencilAttachment;

    public SampleCount SampleCount;
}

public record struct ColorAttachment
{
    public Texture Texture;

    public TextureSubresource Subresource;

    public Texture? ResolveTexture;

    public TextureSubresource ResolveSubresource;

    public LoadAction LoadAction;

    public StoreAction StoreAction;

    public Vector4 ClearColor;
}

public record struct DepthStencilAttachment
{
    public Texture Texture;

    public TextureSubresource Subresource;

    public LoadAction DepthLoadAction;

    public StoreAction DepthStoreAction;

    public LoadAction StencilLoadAction;

    public StoreAction StencilStoreAction;

    public float ClearDepth;

    public byte ClearStencil;
}

public abstract class SwapChain(GraphicsContext context, SwapChainDesc desc) : GraphicsResource(context)
{
    public ref readonly SwapChainDesc Desc => ref desc;

    public abstract Texture CurrentColorTarget { get; }

    public abstract Texture? CurrentDepthStencilTarget { get; }

    public void Resize(uint width, uint height);

    public void Refresh(Surface surface);
}

public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    public void BeginRenderPass(ReadOnlySpan<ColorAttachment> colorAttachments, DepthStencilAttachment? depthStencilAttachment);

    public void EndRenderPass();

    public void SetScissors(ReadOnlySpan<Scissor> scissors);

    public void SetViewports(ReadOnlySpan<Viewport> viewports);
}
```

设计要点：
- 不恢复 `FrameBuffer` 对象。
- `Output` 继续挂在 pipeline 描述上，负责描述附件兼容性，而不是承载运行时 render target 对象。
- `BeginRenderPass(...)` 会先按附件尺寸写入默认 scissors / viewports，调用方后续仍可覆盖。
- `SwapChain.CurrentColorTarget` 与 `CurrentDepthStencilTarget` 按普通 `Texture` 使用。
- `RenderEventArgs` 继续以 `Texture` 为中心：view 代码把 target texture 交给用户渲染，而不是把平台 swapchain 细节泄漏进回调。
- 当前仓库里已经存在两类 view 路径。
- `WinForms` / `WPF` / `WinUI` 风格 view 直接渲染到 swapchain 支撑的 target。
- `Avalonia` 风格 view 渲染到离屏 target，并通过 `Texture.Download(...)` 完成 present。

---

## 7. 显式 Texture Layout 转换 + 轻量 `PipelineBarrier`

需求：补上缺失的显式 texture layout 转换接口，以及一个最小化的、面向读写 shader hazard 的 pipeline barrier；不再把旧的完整 `(stage, access)` 矩阵搬回公共面。

```csharp
public enum TextureLayout
{
    Undefined,
    Common,
    ShaderResource,
    UnorderedAccess,
    RenderTarget,
    DepthStencilRead,
    DepthStencilWrite,
    CopySource,
    CopyDestination,
    ResolveSource,
    ResolveDestination,
    Present
}

public record struct TextureTransition
{
    public Texture Texture;

    public TextureSubresourceRange Range;

    public TextureLayout Before;

    public TextureLayout After;
}

public enum PipelineBarrierScope
{
    All,
    Draw,
    Compute,
    MeshShading,
    RayTracing,
    Copy,
    AccelerationStructureBuild
}

public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    public void Transition(Texture texture, TextureSubresourceRange range, TextureLayout before, TextureLayout after);

    public void Transition(TextureView textureView, TextureLayout before, TextureLayout after);

    public void Transition(ReadOnlySpan<TextureTransition> transitions);

    public void PipelineBarrier(PipelineBarrierScope before, PipelineBarrierScope after);
}
```

设计要点：
- `Transition(...)` 是显式 texture 状态 / layout 接口。Copy、采样、RenderPass、Present 流程里不再保留隐式 layout 变化。
- `TextureView` 重载复用 `textureView.Desc.Range`；`textureView.Desc.Format` 不参与 layout 选择。
- `PipelineBarrier(...)` 是故意做小的 UAV 风格顺序原语，用于“dispatch A 写 `RWTexture` / `RWBuffer`，dispatch B 又访问它”这类场景。它不改变 layout，也不跨 queue。
- 本轮不增加公共 `BufferBarrier`。Buffer 的 copy / bind 顺序继续遵从命令语义，显式用户同步仅保留给 layout 转换和 shader 读写 hazard 隔离。
- 后端映射很直接：
- DX12：resource state transition + UAV barrier
- Vulkan：image memory barrier + 轻量 memory barrier
- Metal：texture usage-state transition + encoder memory barrier / fence

---

## 8. 基于时间线的跨 Queue 同步

需求：加入规范化的时间线提交模型，同时不删除当前已经在用的便利包装。

```csharp
public enum CommandQueueType
{
    Graphics,
    Compute,
    Copy
}

public readonly record struct CommandSubmission(CommandQueue? Queue, ulong Value)
{
    public bool IsEmpty => Queue is null;

    public void Wait()
    {
        Queue?.Wait(Value);
    }
}

public abstract class CommandQueue(GraphicsContext context, CommandQueueType type) : GraphicsResource(context)
{
    public CommandQueueType Type { get; } = type;

    public CommandBuffer CommandBuffer();

    public void WaitIdle();

    public void Wait(ulong value);

    public abstract ulong GetCompletedValue();
}

public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    public CommandQueue Queue { get; } = queue;

    public CommandSubmission Submit(params ReadOnlySpan<CommandSubmission> waits);

    public void Submit(bool waitForCompletion = false);
}

public abstract class SwapChain(GraphicsContext context, SwapChainDesc desc) : GraphicsResource(context)
{
    public abstract CommandSubmission Present(params ReadOnlySpan<CommandSubmission> waits);

    public abstract void Present();
}
```

设计要点：
- 时间线路径是规范低层模型：`Submit(waits)` 与 `Present(waits)` 都返回一个 `CommandSubmission`。
- `Submit(bool waitForCompletion = false)` 与无参 `Present()` 继续保留，服务当前 `Extensions` 与 `Views` 中已经存在的单 queue、单帧循环用法。
- 每条 queue 持有一条单调递增完成时间线。
- `default` / 空 `CommandSubmission` 会在等待集里被忽略。
- `Present(waits)` 返回“当前呈现 backbuffer 重新可写”的那个时间线点。
- 后端映射直接对应各 API 的自然原语：
- DX12：每条 queue 一条 `ID3D12Fence`
- Vulkan：每条 queue 一条 timeline `VkSemaphore`
- Metal 4：每条 queue 一条 `MTLSharedEvent`

---

## 9. 统一 Native Handle 与完整 `NativeObjectType`

需求：所有封装 native 对象的公共类型继续统一走 `INativeObject`，而 `NativeObjectType` 则按稳定 native 角色补齐，不再依赖后端特化 cast helper。

```csharp
public enum NativeObjectType
{
    // DirectX 12 / DXGI
    DxgiFactory,
    DxgiAdapter,
    DxgiSwapChain,
    D3D12Device,
    D3D12CommandQueue,
    D3D12Fence,
    D3D12GraphicsCommandList,
    D3D12PipelineState,
    D3D12RootSignature,
    D3D12Resource,
    D3D12DescriptorHeap,
    D3D12CpuDescriptorHandleRtv,
    D3D12CpuDescriptorHandleDsv,
    D3D12CpuDescriptorHandleSrv,
    D3D12CpuDescriptorHandleUav,
    D3D12CpuDescriptorHandleSampler,
    D3D12QueryHeap,

    // Metal / Metal 4
    MtlDevice,
    Mtl4CommandQueue,
    Mtl4CommandBuffer,
    MtlSharedEvent,
    MtlTexture,
    MtlBuffer,
    MtlSamplerState,
    MtlRenderPipelineState,
    MtlComputePipelineState,
    MtlDepthStencilState,
    MtlHeap,
    MtlAccelerationStructure,
    CaMetalLayer,
    CaMetalDrawable,

    // Vulkan
    VkInstance,
    VkPhysicalDevice,
    VkSurfaceKHR,
    VkSwapchainKHR,
    VkDevice,
    VkQueue,
    VkQueueFamilyIndex,
    VkSemaphore,
    VkCommandBuffer,
    VkPipeline,
    VkPipelineLayout,
    VkShaderModule,
    VkImage,
    VkImageView,
    VkBuffer,
    VkSampler,
    VkDescriptorSet,
    VkQueryPool,
    VkAccelerationStructureKHR
}

public interface INativeObject
{
    /// <summary>当请求的 native 角色与当前对象或当前后端不匹配时，返回 0。</summary>
    nint GetNativeObject(NativeObjectType type);
}
```

设计要点：
- 该枚举围绕稳定的 native 角色组织，能够自然映射到当前 Zenith 的对象类别。
- 一个公共对象可以暴露多个 native 角色。例如 DX12 pipeline 可以同时暴露 `D3D12PipelineState` 与 `D3D12RootSignature`。
- 多个 Zenith 对象类别也可以复用同一个 native 角色。例如 DX12 buffer、texture、acceleration structure 都可通过 `D3D12Resource` 暴露。
- `VkQueueFamilyIndex` 这类非指针标量也继续通过同一个 `nint` 入口承载。
- `GetNativeObject(...)` 不增加引用计数，也不转移所有权；返回句柄只在 Zenith 对象存活期间有效。
- 返回 `0` 是唯一退化路径，不支持的组合不抛异常。

---

## 附录 A：当前 Upload 路径

```csharp
CommandBuffer commandBuffer = context.Copy.CommandBuffer();

commandBuffer.Upload(texture,
                     default,
                     default,
                     new() { Width = width, Height = height, Depth = 1 },
                     new()
                     {
                         Pointer = pixels,
                         Layout = new()
                         {
                             SizeInBytes = sizeInBytes,
                             RowPitchInBytes = rowPitchInBytes,
                             SlicePitchInBytes = slicePitchInBytes
                         }
                     });

commandBuffer.Submit(true);
```

这与当前 `ImageSharp` 扩展，以及当前离屏 view 的 present 路径一致。

## 附录 B：当前绑定路径

```csharp
ResourceBinding[] bindings =
[
    new() { Type = ResourceType.ConstantBuffer, Count = 1 },
    new() { Type = ResourceType.Texture, Count = 1 },
    new() { Type = ResourceType.Sampler, Count = 1 }
];

ResourceTable table = context.CreateResourceTable(new() { Bindings = bindings });
table.Write(0, constants);
table.Write(1, textureView);
table.Write(2, sampler);

commandBuffer.SetPipeline(pipeline);
commandBuffer.PushResourceTable(table);
```

这与当前 `ImGui` 扩展一致：长期持有 `ResourceTable`，显式 `Write(...)`，然后在当前 pipeline 下 `PushResourceTable(...)`。

## 附录 C：时间线形状的多 Queue 流程

```csharp
CommandSubmission copyDone = copyCommandBuffer.Submit();

CommandSubmission graphicsDone = graphicsCommandBuffer.Submit(copyDone);

CommandSubmission presentDone = swapChain.Present(graphicsDone);
```

现有 `Submit(true)` / `Present()` 的便利调用在简单单 queue 路径下仍然有效；只有当工作跨 queue 或跨帧传播时，`CommandSubmission` 才成为必需。