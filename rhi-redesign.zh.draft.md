# RHI 重设计草案 — 需求规范（中文临时版）

> 仅用于本轮设计讨论，不进入提交。最终英文版同步在 `rhi-redesign.draft.md`。
> 本文档以 9 条需求为骨架；公共类型与 §0 公共约定共享。

## 0. 公共约定

### 0.1 命名与代码风格

- 所有公共值类型一律 `record struct`，公共字段可写
- 多元素入参一律 `ReadOnlySpan<T>` / `params ReadOnlySpan<T>`，不使用 `T[]` / `IEnumerable<T>`
- 方法体一律 `{ ... }`；**例外**：`ref` / `ref readonly` 返回的属性保留 `=> ref _field;`
- 字节单位字段与参数加 `*InBytes` 后缀
- 抽象后端钩子：仅当与同名非 `Core` 包装共存时才用 `*Core` 后缀（如 `Wait` / `WaitCore`）；其余一律自然名
- 抽象成员一律 `protected abstract`，同程序集内部调用走 `internal` 包装

### 0.2 公共面边界

- 公共面**无**全局内存屏障（VK pipeline barrier without resource）以外的特殊形态
- 公共面**无**驻留集（residency / `MakeResident` / `Evict`）相关原语
- 公共面**无**长生命周期 `View` 对象；view 信息为调用现场值
- 公共面**无**长生命周期 `FrameBuffer` 对象
- 公共同步结果只有一种：`CommandSubmission`
- Texture 的 image-layout 完全在后端内部维护，由 `BarrierAccess` + RenderPass attachment 元数据推导

### 0.3 资源句柄基类

```csharp
public abstract class GraphicsResource(GraphicsContext context) : DisposableObject, INativeObject
{
    public GraphicsContext Context { get; } = context;

    public abstract nint GetNativeObject(NativeObjectType type);
}

public abstract class Buffer(GraphicsContext context, BufferDesc desc) : GraphicsResource(context)
{
    private BufferDesc desc = desc;

    public ref readonly BufferDesc Desc => ref desc;

    /// <summary>
    /// 映射为 CPU 可访问区间。仅在 <c>Desc.Flags</c> 含 <see cref="BufferUsageFlags.MapRead"/>
    /// 或 <see cref="BufferUsageFlags.MapWrite"/> 时合法，必须与 <see cref="Unmap"/> 配对。
    /// </summary>
    public abstract MappedMemory Map();

    public abstract void Unmap();
}

public abstract class Texture(GraphicsContext context, TextureDesc desc) : GraphicsResource(context)
{
    private TextureDesc desc = desc;

    public ref readonly TextureDesc Desc => ref desc;

    public static implicit operator TextureSubresourceRange(Texture texture)
    {
        return new()
        {
            BaseMipLevel = 0,
            LevelCount = texture.Desc.MipLevels,
            BaseArrayLayer = 0,
            LayerCount = texture.Desc.ArrayLayers
        };
    }
}

public abstract class Sampler(GraphicsContext context, SamplerDesc desc) : GraphicsResource(context)
{
    private SamplerDesc desc = desc;

    public ref readonly SamplerDesc Desc => ref desc;
}
```

### 0.4 BufferDesc / 能力位

沿用现框架设计，不引入 `MemoryAccess` 枚举；CPU 可见性由 `BufferUsageFlags.MapRead` / `MapWrite` 在能力位集合里表达。

```csharp
[Flags]
public enum BufferUsageFlags
{
    None                  = 0,
    Vertex                = 1 << 0,
    Index                 = 1 << 1,
    Indirect              = 1 << 2,
    AccelerationStructure = 1 << 3,
    Constant              = 1 << 4,
    ShaderResource        = 1 << 5,
    UnorderedAccess       = 1 << 6,
    MapRead               = 1 << 7,
    MapWrite              = 1 << 8
}

public record struct BufferDesc
{
    public uint SizeInBytes;

    public uint StrideInBytes;

    public BufferUsageFlags Flags;
}
```

`BufferUsageFlags` 是**能力位集合**，资源生命周期内不可变；它与 §6 的 `BarrierAccess` 是两层：`Flags` 决定"允许的访问集合"，`BarrierAccess` 描述"现在以哪种方式访问"。同一块 buffer 同时声明 `Vertex | ShaderResource` 完全合法，用作顶点输入直接 `SetVertexBuffer` 即可，无任何"格式转换"动作；barrier 只负责"上个用法 → 下个用法"的可见性。

CPU 上传路径二选一：
- buffer 声明 `MapRead` / `MapWrite` 时，应用侧 `Map()` / `Unmap()` 直接写
- 否则沉入 `CommandBuffer.Upload` / `CopyBuffer` 走 staging

---

## 1. ReadOnlySpan 替代数组

需求：所有"多元素入参"在公共 API 上一律改为 `ReadOnlySpan<T>` / `params ReadOnlySpan<T>`，不出现 `T[]`、`IList<T>`、`IEnumerable<T>`、`IReadOnlyList<T>`。

理由：
- 接 .NET 10 的 `params ReadOnlySpan<T>` 与集合表达式 `[ ... ]`，调用现场零托管分配
- 同一签名同时接受栈分配 / 数组 / 单元素，调用方写法统一
- 后端可直接拷贝到原生数组，无需先 enumerate

适用面（不完全列表）：

| 入参 | 旧形态 | 新形态 |
|---|---|---|
| Submit 等待集 | `Fence[]` | `ReadOnlySpan<CommandSubmission>` |
| 顶点缓冲槽 | `Buffer[]` + `ulong[]` | `ReadOnlySpan<Buffer>` + `ReadOnlySpan<ulong>` |
| 视口 / 裁剪 | `Viewport[]` | `ReadOnlySpan<Viewport>` |
| RenderPass 颜色附件 | `ColorAttachment[]` | `ReadOnlySpan<ColorAttachment>` |
| ResourceTable 数组绑定 | `IBindableResource[]` | `ReadOnlySpan<BufferRange>` / `ReadOnlySpan<TextureView>` / `ReadOnlySpan<Sampler>` |
| 上传数据 | `byte[]` + offset/size | `ReadOnlySpan<T> where T : unmanaged` |

例外：返回多个元素时使用 `ReadOnlySpan<T>` 的成本（必须有持有者）大于价值 —— 单元素返回保留具体类型；批量返回保留 `T[]` 或暴露索引访问。

---

## 2. 跨队列同步

需求：跨队列依赖只暴露**一个**值类型 `CommandSubmission`，对齐三端时间线模型。

```csharp
public enum CommandQueueType
{
    Graphics,
    Compute,
    Copy
}

/// <summary>
/// 一次 Submit / Present 产生的一个时间线点。
/// <c>default</c> 是合法的"空"值，后端会从 <c>waits</c> 中过滤掉；其上 <see cref="Wait"/> 为空操作。
/// </summary>
public readonly struct CommandSubmission(CommandQueue? queue, ulong value)
{
    public CommandQueue? Queue = queue;

    public ulong Value = value;

    public void Wait()
    {
        Queue?.Wait(Value);
    }
}

public abstract class CommandQueue(GraphicsContext context, CommandQueueType type) : GraphicsResource(context)
{
    public CommandQueueType Type { get; } = type;

    /// <summary>获取一个可录制的 CommandBuffer，必要时复用已退役实例。</summary>
    public CommandBuffer CommandBuffer();

    /// <summary>等待此 queue 上所有已提交命令完成。幂等。</summary>
    public void WaitForIdle();

    /// <summary>等待该 queue 时间线推进到 <paramref name="value"/>。</summary>
    public void Wait(ulong value);

    protected abstract ulong GetCompletedValue();

    protected abstract void WaitCore(ulong value);

    protected abstract void SubmitCore(CommandBuffer commandBuffer,
                                       ReadOnlySpan<CommandSubmission> waits,
                                       ulong signalValue);

    protected abstract CommandBuffer CreateCommandBuffer();
}

public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    public CommandQueue Queue { get; } = queue;

    /// <summary>结束录制并在所属 queue 上入队。</summary>
    public CommandSubmission Submit(params ReadOnlySpan<CommandSubmission> waits);
}
```

设计要点：
- 每个 queue 持一条单调递增完成时间线；`Submit` 推进 `Value`，并以 `CommandSubmission(queue, value)` 公开
- 跨队列依赖 = 把上游 `CommandSubmission` 传入下游 `Submit(waits)`
- `GetCompletedValue()` 是方法不是属性：三端原生 API 都是调用语义
- `CommandBuffer` 池化是 queue 内部实现，公共面只暴露 `CommandBuffer()` + `Submit(...)`
- `GraphicsContext.Graphics` / `Compute` / `Copy` 三条队列；`Present` 始终在 `Graphics`

后端时间线对照：

| 后端 | 时间线对象 | API |
|---|---|---|
| Metal 4 | 每 queue 一个 `MTLSharedEvent` | `commandBuffer.EncodeSignalEvent(event, value)` / `event.SignaledValue` |
| Vulkan 1.4 | 每 queue 一个 timeline `VkSemaphore` | `vkQueueSubmit2` 的 `pSignalSemaphoreInfos[].value` / `vkGetSemaphoreCounterValue` |
| DX12 | 每 queue 一个 `ID3D12Fence` | `queue.Signal(fence, value)` / `fence.GetCompletedValue()` |

---

## 3. 子资源 / 偏移 / 范围的值类型化

需求：用 `TextureSubresource` / `TextureSubresourceLayers` / `TextureSubresourceRange` / `Offset3D` / `Extent3D` 替代旧版 `TextureSlice` / `TextureOffset` / `TextureExtent`，统一命名并明确"单点 / 单 mip 多 layer / 多 mip 多 layer"三档。

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
```

| 类型 | 表达 | 用途 |
|---|---|---|
| `TextureSubresource` | 单 mip × 单 layer | RTV / DSV / Resolve 端点 |
| `TextureSubresourceLayers` | 单 mip × 连续 layer 段 | Copy / Upload |
| `TextureSubresourceRange` | 连续 mip 段 × 连续 layer 段 | Barrier / `TextureView.Range` |

约定：
- 公共面不暴露 `aspect`；后端从 `Texture.Format` 推断 color / depth / stencil
- Cube face 以 `ArrayLayer = cubeIndex * 6 + face` 表达，无独立 face 轴
- `Texture` 隐式转 `TextureSubresourceRange`（覆盖整张纹理）让常见调用一行内写完
- 命名后缀对齐 VK：`Subresource`（点）/ `SubresourceLayers`（面）/ `SubresourceRange`（体）

---

## 4. 删除 BufferView，统一为 BufferRange

需求：移除 `BufferView` 类型与所有 `CreateBufferView` 入口，buffer 子范围一律用调用现场的 `BufferRange` 值表达。

```csharp
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

理由：
- buffer 不需要"格式族重解释"；offset/size/stride 三元组就是描述子范围所需的全部信息
- DX12 `D3D12_*_VIEW_DESC`、VK `VkDescriptorBufferInfo`、Metal `setBuffer:offset:` 都接受调用现场的 offset/size，不存在长生命周期 view 对象的必要
- shader 端的解释维度（CBV / structured-SRV / byteaddress-SRV / typed-SRV / UAV）由槽位的 `ResourceLayout` 决定，与 buffer 本身无关
- 隐式转换让 `Write(0, vbo)`、`SetVertexBuffer(0, vbo, 0)` 这类常见单资源绑定保持一行

---

## 5. TextureView 重构（Format / ViewType）

需求：`TextureView` 重新定位为**调用现场值**，5 元组 → 4 元组（去掉 swizzle），加入 `Format` / `ViewType` 用于格式族重解释与维度切换。

```csharp
public enum TextureViewType
{
    Texture1D,
    Texture2D,
    Texture3D,
    TextureCube,
    Texture1DArray,
    Texture2DArray,
    TextureCubeArray
}

public record struct TextureView
{
    public Texture Texture;

    public TextureSubresourceRange Range;

    /// <summary><c>null</c> 表示按 <c>Texture.Desc</c> + <c>Range</c> 推导。</summary>
    public TextureViewType? ViewType;

    /// <summary><c>null</c> 表示沿用 <c>Texture.Desc.Format</c>；非 null 时按同一兼容族重解释。</summary>
    public PixelFormat? Format;

    public static implicit operator TextureView(Texture texture)
    {
        return new()
        {
            Texture = texture,
            Range = texture,
            ViewType = null,
            Format = null
        };
    }
}
```

- 对齐 VK `VkImageViewCreateInfo` 与 Metal `newTextureViewWithPixelFormat:textureType:levels:slices:` 的参数集
- 后端按 `(Texture, Range, ViewType, Format)` 为 key 懒加载并缓存原生 view，纯实现细节
- 通道 swizzle **不在公共面暴露**：单通道→灰度类约定在 shader 侧解决；BGRA ↔ RGBA 一类靠 `Format` 在同兼容族内重解释覆盖。如未来出现刚需，可作为可选 `ComponentMapping?` 字段加回，向后兼容
- `Texture` 隐式转 `TextureView`（全范围、原 format、原维度）让常见单资源绑定一行写完

---

## 6. Barrier — 1:1 同步原语

需求：移除所有"隐式布局转换"。所有可见性 / 执行依赖 / 布局切换由调用方通过 `(stage, access)` 二元组显式声明，对齐 Metal 4 / VK 1.4 / DX12 Enhanced Barriers。

```csharp
[Flags]
public enum BarrierStage : uint
{
    None                       = 0,
    All                        = ~0u,
    Draw                       = 1u << 0,
    VertexInput                = 1u << 1,
    VertexShader               = 1u << 2,
    PixelShader                = 1u << 3,
    EarlyDepthStencil          = 1u << 4,
    LateDepthStencil           = 1u << 5,
    RenderTarget               = 1u << 6,
    ComputeShader              = 1u << 7,
    RayTracing                 = 1u << 8,
    Copy                       = 1u << 9,
    Resolve                    = 1u << 10,
    IndirectArgument           = 1u << 11,
    AccelerationStructureBuild = 1u << 12,
}

[Flags]
public enum BarrierAccess : uint
{
    None                       = 0,
    VertexBuffer               = 1u << 0,
    IndexBuffer                = 1u << 1,
    ConstantBuffer             = 1u << 2,
    ShaderRead                 = 1u << 3,
    UnorderedAccessRead        = 1u << 4,
    UnorderedAccessWrite       = 1u << 5,
    RenderTarget               = 1u << 6,
    DepthStencilRead           = 1u << 7,
    DepthStencilWrite          = 1u << 8,
    CopySource                 = 1u << 9,
    CopyDestination            = 1u << 10,
    ResolveSource              = 1u << 11,
    ResolveDestination         = 1u << 12,
    IndirectArgument           = 1u << 13,
    Present                    = 1u << 14,
    AccelerationStructureRead  = 1u << 15,
    AccelerationStructureWrite = 1u << 16,
}
```

CommandBuffer 上的 barrier 入口三种形态：

```csharp
public void Barrier(BarrierStage afterStages, BarrierAccess afterAccess,
                    BarrierStage beforeStages, BarrierAccess beforeAccess);

public void BufferBarrier(BufferRange range,
                          BarrierStage afterStages, BarrierAccess afterAccess,
                          BarrierStage beforeStages, BarrierAccess beforeAccess);

public void TextureBarrier(TextureView view,
                           BarrierStage afterStages, BarrierAccess afterAccess,
                           BarrierStage beforeStages, BarrierAccess beforeAccess);
```

设计要点：
- `BarrierStage` 描述访问发生在管线**何处**（when）；`BarrierAccess` 描述访问的**性质**（what）。两者对齐 Metal 4 `MTL4RenderStages` + `MTL4VisibilityOptions`、VK 1.4 `VkPipelineStageFlags2` + `VkAccessFlags2`、DX12 `D3D12_BARRIER_SYNC` + `D3D12_BARRIER_ACCESS`
- 三种形态参数序一致：`(afterStages, afterAccess) → (beforeStages, beforeAccess)`
- texture image-layout：完全在后端内部维护，由 `BarrierAccess` + RenderPass attachment 元数据推导。公共面无 layout 枚举
- 公共面**没有**全局内存屏障的"快捷形式"以外的简化入口；调用方必须显式给出 stage + access
- 队列内部的"同 stage 同资源连续访问"由后端折叠为 noop，不强求调用方手动去重

---

## 7. 删除 FrameBuffer — 内联 RenderPass + SwapChain 公开当前目标

需求：移除 `FrameBuffer` / `RenderPassInfo` 等长生命周期对象。RenderPass 以 `BeginRenderPass` / `EndRenderPass` 内联表达；`SwapChain` 直接暴露 `CurrentColorTarget` / `CurrentDepthStencilTarget`，使 backbuffer 与普通 `Texture` 同形。

```csharp
public enum LoadAction
{
    Load,
    Clear,
    DontCare
}

public enum StoreAction
{
    Store,
    DontCare,
    Resolve
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

// CommandBuffer
public void BeginRenderPass(ReadOnlySpan<ColorAttachment> colorAttachments,
                            DepthStencilAttachment? depthStencilAttachment);

public void EndRenderPass();
```

```csharp
public abstract class SwapChain(GraphicsContext context, SwapChainDesc desc) : GraphicsResource(context)
{
    private SwapChainDesc desc = desc;

    public ref readonly SwapChainDesc Desc => ref desc;

    public abstract uint Width { get; }

    public abstract uint Height { get; }

    public abstract Texture CurrentColorTarget { get; }

    public abstract Texture? CurrentDepthStencilTarget { get; }

    /// <summary>
    /// 在 GraphicsQueue 上提交 present，等待 <paramref name="waits"/>；
    /// 返回的 <see cref="CommandSubmission"/> 表示下一张 backbuffer 可写。
    /// </summary>
    public abstract CommandSubmission Present(params ReadOnlySpan<CommandSubmission> waits);

    public void Resize(uint width, uint height);

    public void Refresh(Surface surface);
}
```

设计要点：
- backbuffer 以普通 `Texture` 公开（颜色 + 可选深度模板）；它们既能进 `BeginRenderPass`，也能进 `TextureBarrier` / `CopyTexture` / `Write(...)`
- `BeginRenderPass` 接受 attachment span，无深度时显式传 `null`；按 attachment 尺寸自动填默认 `SetViewports` / `SetScissors`，调用方可在其后再调一次覆盖
- `Present(...)` 与 `Submit(...)` 形态对称：吃 waits、产 `CommandSubmission`
- 当前 image index、acquire / present 同步原语都是后端内部事务

---

## 8. ResourceTable 多重载 + 删除 IBindableResource

需求：`ResourceTable.Write` 直接按资源类型重载（`BufferRange` / `TextureView` / `Sampler`），不再走 `IBindableResource` 多态入口。

```csharp
public abstract class ResourceTable(GraphicsContext context, ResourceLayout layout) : GraphicsResource(context)
{
    public ResourceLayout Layout { get; } = layout;

    public void Write(uint binding, BufferRange range);

    public void Write(uint binding, TextureView view);

    public void Write(uint binding, Sampler sampler);

    public void Write(uint binding, ReadOnlySpan<BufferRange> ranges);

    public void Write(uint binding, ReadOnlySpan<TextureView> views);

    public void Write(uint binding, ReadOnlySpan<Sampler> samplers);

    protected abstract void WriteCore(uint binding, ReadOnlySpan<BufferRange> ranges);

    protected abstract void WriteCore(uint binding, ReadOnlySpan<TextureView> views);

    protected abstract void WriteCore(uint binding, ReadOnlySpan<Sampler> samplers);
}
```

CommandBuffer 上对应入口：

```csharp
public void SetPipeline(Pipeline pipeline);

public void PushResourceTable(ResourceTable table);
```

设计要点：
- `Buffer` / `Texture` 经 §4 / §5 的隐式转换让单资源绑定一行写完：`table.Write(0, vbo)` / `table.Write(1, tex)`
- buffer 解释（CBV / SRV-structured / SRV-byteaddress / UAV / typed）由槽位的 `ResourceLayout` 决定，与传入的 `BufferRange` 无关
- 每个 pipeline 仅支持一张 table（对齐 Metal 4 argument-table 模型）
- `PushResourceTable` 不接受 stages 参数：stages 信息由 layout 在每个 binding 上自带
- **Push-snapshot 语义**：`PushResourceTable` 在调用现场把 `table` 的当前内容快照进 cmd buffer；之后对该 `table` 的 `Write` 不影响已 push 的绑定，所以同一个 `ResourceTable` 可以在帧内反复 `Write` + `Push`
- 校验：UAV 槽位要求资源声明对应 `UnorderedAccess` 能力位，shape / layout / 格式族不匹配在 `Write` 时报错

为什么不用 `IBindableResource`：
- 三种资源在原生 API 上的写入路径完全不同（descriptor / argument buffer slot / sampler heap），多态接口反而需要在运行时 dispatch
- 重载形式让编译期就能选中正确的 `Write` 路径，避免装箱与运行时 type test
- 隐式转换 `Buffer → BufferRange` / `Texture → TextureView` 已经覆盖"我就想直接传句柄"的便利性

---

## 9. INativeObject — 原生句柄统一入口

需求：所有持有原生对象的公共类型实现 `INativeObject`，通过 `GetNativeObject(NativeObjectType)` 暴露后端句柄；公共面不挂任何平台条件属性。

```csharp
public enum NativeObjectType
{
    // DirectX 12
    DxgiFactory,
    DxgiAdapter,
    D3D12Device,
    D3D12CommandQueue,
    D3D12GraphicsCommandList,
    D3D12Resource,
    D3D12CpuDescriptorHandleSampler,
    D3D12CpuDescriptorHandleRtv,
    D3D12CpuDescriptorHandleDsv,
    D3D12CpuDescriptorHandleSrv,
    D3D12CpuDescriptorHandleUav,

    // Metal 4
    MtlDevice,
    Mtl4CommandQueue,
    Mtl4CommandBuffer,
    MtlTexture,
    MtlBuffer,
    MtlSamplerState,

    // Vulkan
    VkInstance,
    VkPhysicalDevice,
    VkDevice,
    VkQueue,
    VkQueueFamilyIndex,
    VkCommandBuffer,
    VkImage,
    VkImageView,
    VkBuffer,
    VkSampler
}

public interface INativeObject
{
    /// <summary>枚举不匹配本对象 / 当前后端时返回 0。</summary>
    nint GetNativeObject(NativeObjectType type);
}

public abstract class GraphicsContext : DisposableObject, INativeObject
{
    public abstract nint GetNativeObject(NativeObjectType type);
}

// GraphicsResource 已在 §0.3 实现 INativeObject
```

设计要点：
- 命名沿用各原生 API 既有前缀：DX12 体系按 `Dxgi` / `D3D12` 区分；Metal 4 与旧 Metal 共存时用 `Mtl` / `Mtl4` 区分；Vulkan 一律 `Vk`
- `D3D12CpuDescriptorHandle*` 按 view 类型拆开，避免单入口语义模糊
- 非句柄的标量信息（如 `VkQueueFamilyIndex`）也走同一入口，以 `nint` 承载 `uint`
- 所有 `GraphicsResource` 子类一律实现该接口；不感兴趣的子类对所有类型返回 0
- 取该接口的代码必须自己处理"返回 0"的退化路径，公共面不抛异常

---

## 附录 A：典型帧循环（综合 §2 / §6 / §7）

```csharp
CommandSubmission imageReady = default;

while (running)
{
    CommandBuffer cmd = ctx.Graphics.CommandBuffer();

    ReadOnlySpan<ColorAttachment> colors =
    [
        new()
        {
            Texture = swapChain.CurrentColorTarget,
            Subresource = new() { MipLevel = 0, ArrayLayer = 0 },
            LoadAction = LoadAction.Clear,
            StoreAction = StoreAction.Store,
            ClearColor = new(0, 0, 0, 1)
        }
    ];

    DepthStencilAttachment? depth = swapChain.CurrentDepthStencilTarget is { } ds
        ? new()
        {
            Texture = ds,
            Subresource = new(),
            DepthLoadAction = LoadAction.Clear,
            DepthStoreAction = StoreAction.DontCare,
            ClearDepth = 1.0f
        }
        : null;

    cmd.BeginRenderPass(colors, depth);
    // draws
    cmd.EndRenderPass();

    CommandSubmission frame = cmd.Submit(imageReady);

    imageReady = swapChain.Present(frame);
}
```

每一步形态一致：拿一组 `CommandSubmission` waits，返回一个 `CommandSubmission`。`Submit` 与 `Present` 在 API 表面对称。
