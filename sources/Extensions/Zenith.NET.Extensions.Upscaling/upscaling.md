# 超分扩展设计（DLSS / FSR / MetalFX）

`Zenith.NET.Extensions.Upscaling` 提供超分、光线重建（降噪 + 超分）、帧生成与帧生成交换链，采用与后端无关、按图形学语义定义且与核心 RHI 风格一致的公共 API。

## 1. 原则

1. **参数按语义定义，不按厂商定义。** 字段名来自图形学概念（颜色、深度、运动向量、法线、粗糙度、相机），不出现 `NGX`、`ffx`、`MTLFX` 等原生名字。映射到原生结构、推导衍生参数与布局转换属于后端实现。
2. **创建描述只声明输出格式、尺寸或交换链契约，不重复声明输入纹理格式。** 功能实例内部自管并公开 `Output` 纹理（自动内置平台所需的全部 `TextureUsages` 并在 `Dispatch` 结束后统一转为 `TextureLayout.Sampled`）；输入纹理自带 `Texture.Desc`，后端按实际传入纹理的格式完成惰性管线创建或缓存复用。
3. **能够由输入数学推导或合理默认的参数，不暴露冗余开关。** 近远平面、垂直视场角、宽高比、反向 Z 与无限远平面统一从投影矩阵自动推导；是否 HDR 与呈现传输函数从颜色纹理格式推导；不传曝光纹理即启用自动曝光。
4. **三种能力，统一单路场景输入。** 提供超分、光线重建、帧生成。光线重建始终接收同一套单路带噪线性颜色与几何材质引导图，不要求调用方为特定后端拆分多路辐射度信号；帧生成每次生成一帧。
5. **资源只传整体对象，风格严格对齐核心 RHI。** 只传完整 `Texture`，不划分子资源或子矩形；尺寸使用成对的 `uint Width` / `uint Height` 字段；结构体与参数列表不使用 `required`、`in`、`ref` 或元组；功能对象只公开 `Desc` 与 `Output`。

## 2. 公共 API

### 2.1 质量档

```csharp
public enum UpscalingQuality
{
    Native,

    Quality,

    Balanced,

    Performance,

    UltraPerformance
}
```

`UpscalingQuality` 用于通过描述符静态工厂（`SuperResolutionDesc.Quality`、`RayReconstructionDesc.Quality`）创建包含推荐渲染尺寸的描述。`Native` 表示原分辨率时域抗锯齿或原分辨率光线重建。

### 2.2 能力与入口

```csharp
public class UpscalingCapabilities
{
    public bool SuperResolutionSupported { get; }

    public bool RayReconstructionSupported { get; }

    public bool FrameGenerationSupported { get; }
}

public static class Extensions
{
    extension(GraphicsContext context)
    {
        public void InitializeUpscaling();

        public UpscalingCapabilities UpscalingCapabilities { get; }

        public SuperResolution CreateSuperResolution(SuperResolutionDesc desc);

        public RayReconstruction CreateRayReconstruction(RayReconstructionDesc desc);

        public FrameGeneration CreateFrameGeneration(FrameGenerationDesc desc);

        public FrameGenerationSwapChain CreateFrameGenerationSwapChain(FrameGenerationSwapChainDesc desc);
    }
}
```

`context.InitializeUpscaling()` 根据当前 `GraphicsContext` 自动为超分、光线重建、帧生成三种能力分别挑选最佳可用硬件后端，对调用方完全透明。`UpscalingCapabilities` 反映当前 `GraphicsContext` 下三种能力的实际支持情况。

### 2.3 采样抖动工具（`UpscalingHelper`）

```csharp
public static class UpscalingHelper
{
    public static uint JitterPhaseCount(uint renderWidth, uint renderHeight, uint outputWidth, uint outputHeight);

    public static uint JitterPhaseCount(SuperResolutionDesc desc);

    public static uint JitterPhaseCount(RayReconstructionDesc desc);

    public static Vector2 JitterOffset(uint frameIndex, uint phaseCount);

    public static Vector2 JitterOffset(uint frameIndex, uint renderWidth, uint renderHeight, uint outputWidth, uint outputHeight);

    public static Vector2 JitterOffset(uint frameIndex, SuperResolutionDesc desc);

    public static Vector2 JitterOffset(uint frameIndex, RayReconstructionDesc desc);

    public static Matrix4x4 JitterProjection(Matrix4x4 projection, Vector2 jitterOffset, uint renderWidth, uint renderHeight);

    public static Matrix4x4 JitterProjection(Matrix4x4 projection, Vector2 jitterOffset, SuperResolutionDesc desc);

    public static Matrix4x4 JitterProjection(Matrix4x4 projection, Vector2 jitterOffset, RayReconstructionDesc desc);
}
```

- `JitterPhaseCount` 按渲染分辨率与输出分辨率的面积缩放比 $\max(\lceil 8 \times s_x \times s_y \rceil, 8)$ 计算 Halton 采样周期长度。
- `JitterOffset` 按周期对 `frameIndex` 循环取模并返回 `[-0.5, 0.5]` 像素偏移（+X 向右、+Y 向下），可直接用于光线追踪主射线偏移以及 `Args.JitterOffset`。
- `JitterProjection` 将 `JitterOffset` 注入不含抖动的 `Projection` 矩阵，返回供光栅化渲染使用的带抖动裁剪矩阵（自动适配左/右手坐标系、正/反向 Z、无限远平面与正交投影）；传给超分、光线重建与帧生成的 `Args.Projection` 始终保持不含抖动的原始投影矩阵。

### 2.4 输入与资源状态约定

这些约定对所有后端一致：

| 输入 | 约定 |
|---|---|
| 运动向量 | UV 空间，`PreviousUV - CurrentUV`，包含相机与物体运动，不包含抖动；尺寸为 `RenderWidth × RenderHeight` |
| 深度 | 硬件视口深度（`currentClip.z / currentClip.w`）；尺寸为 `RenderWidth × RenderHeight`；反向 Z 与无限远平面从 `Projection` 自动识别 |
| 抖动与矩阵 | `JitterOffset` 为渲染分辨率像素单位的采样抖动偏移（以渲染栅格左上角为原点、右下为正，表示本帧相机射线采样点相对像素中心的偏移，范围通常在 `[-0.5, 0.5]`，可通过 `UpscalingHelper.JitterOffset` 直接获取，内部自动按后端要求转换符号）；`View` 为世界到视空间矩阵，`Projection` 为不含抖动的视空间到裁剪空间矩阵；距离以米为单位，矩阵采用 `System.Numerics.Matrix4x4` 行主序行向量约定 |
| 超分、光线重建颜色 | 线性场景颜色，不含色调映射与 UI；浮点格式（如 `R16G16B16A16Float`、`R11G11B10Float`）按 HDR 线性辐射度处理；输入尺寸为 `RenderWidth × RenderHeight`，实例公开的 `Output` 尺寸为 `OutputWidth × OutputHeight` |
| 帧生成颜色 | 已完成色调映射与颜色编码的呈现帧，尺寸为 `OutputWidth × OutputHeight`；`R8G8B8A8*` / `B8G8R8A8*` 按 sRGB 处理，`R10G10B10A2UNorm` 按 HDR10 PQ 处理，`R16G16B16A16Float` 按线性 scRGB 处理 |
| 反照度 | 线性反射率，不含光照；`DiffuseAlbedo` 与 `SpecularAlbedo` 分别提供 |
| 法线 | RGB 保存世界空间单位向量，不做八面体编码或打包 |
| 粗糙度 | R 通道保存 `[0, 1]` 线性粗糙度 |
| 镜面命中距离 | 可选；提供时 R 通道保存镜面反射射线命中距离，为 `null` 时由后端无命中距离引导运行或按深度与粗糙度估算 |
| 曝光 | `Exposure` 为 `null` 时由后端自动计算曝光；提供时为 1×1 `R16Float` 曝光倍率纹理。`PreExposure` 为已乘入线性颜色的预曝光倍率，为 `null` 时按 `1.0f` 处理 |
| 资源布局与用途 | 所有输入纹理只需具备 `TextureUsages.Sampled`，并在 `Dispatch` / `Present` 调用前后保持 `TextureLayout.Sampled`（无需任何额外的传输或存储用途标志）；实例自管的 `Output` 纹理在 `Dispatch` 结束后自动处于 `TextureLayout.Sampled`；内部原生状态切换由后端自动完成并恢复 |

### 2.5 超分（`SuperResolution`）

```csharp
public struct SuperResolutionDesc
{
    public PixelFormat Format;

    public uint RenderWidth;

    public uint RenderHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public static SuperResolutionDesc Quality(GraphicsContext context, PixelFormat format, uint outputWidth, uint outputHeight, UpscalingQuality quality);
}

public struct SuperResolutionArgs
{
    public Texture Color;

    public Texture Depth;

    public Texture MotionVectors;

    public Texture? ReactiveMask;

    public Texture? Exposure;

    public Vector2 JitterOffset;

    public Matrix4x4 Projection;

    public float DeltaTimeInSeconds;

    public float? PreExposure;

    public bool Reset;
}

public abstract class SuperResolution : DisposableObject
{
    public SuperResolutionDesc Desc { get; }

    public Texture Output { get; }

    public void Dispatch(CommandBuffer commandBuffer, SuperResolutionArgs args);
}
```

`RenderWidth/Height` 不大于 `OutputWidth/Height`，相等时执行原分辨率时域抗锯齿。`SuperResolutionDesc.Quality(...)` 根据当前激活的超分后端查询指定 `UpscalingQuality` 对应的最佳渲染尺寸并返回完整描述；调用方创建 `SuperResolution` 后可直接通过 `superResolution.Desc.RenderWidth/Height` 创建渲染目标，并在 `Dispatch` 后直接读取已处于 `TextureLayout.Sampled` 的 `superResolution.Output`。

`ReactiveMask` 为可选反应性遮罩，R 通道值为 `[0, 1]`，数值越大越减少对历史帧的依赖。`DeltaTimeInSeconds` 为相邻真实帧的时间间隔（秒）。首帧与内部配置重建时自动初始化历史，调用方仅需在切镜头或场景突变时将 `Reset` 置为 `true`。

### 2.6 光线重建（`RayReconstruction`）

```csharp
public struct RayReconstructionDesc
{
    public PixelFormat Format;

    public uint RenderWidth;

    public uint RenderHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public static RayReconstructionDesc Quality(GraphicsContext context, PixelFormat format, uint outputWidth, uint outputHeight, UpscalingQuality quality);
}

public struct RayReconstructionArgs
{
    public Texture Color;

    public Texture Depth;

    public Texture MotionVectors;

    public Texture DiffuseAlbedo;

    public Texture SpecularAlbedo;

    public Texture Normals;

    public Texture Roughness;

    public Texture? SpecularHitDistance;

    public Texture? ReactiveMask;

    public Texture? TransparencyOverlay;

    public Texture? Exposure;

    public Vector2 JitterOffset;

    public Matrix4x4 View;

    public Matrix4x4 Projection;

    public float DeltaTimeInSeconds;

    public float? PreExposure;

    public bool Reset;
}

public abstract class RayReconstruction : DisposableObject
{
    public RayReconstructionDesc Desc { get; }

    public Texture Output { get; }

    public void Dispatch(CommandBuffer commandBuffer, RayReconstructionArgs args);
}
```

`RayReconstruction` 在一次 `Dispatch` 中完成光线追踪降噪与超分，并将结果写入实例持有的 `Output`（尺寸为 `OutputWidth × OutputHeight`，`Dispatch` 结束后处于 `TextureLayout.Sampled`）。除可选 1×1 `Exposure` 外，输入纹理尺寸均为 `RenderWidth × RenderHeight`。

`Color` 始终是完整的带噪线性场景颜色；`DiffuseAlbedo`、`SpecularAlbedo`、`Normals`、`Roughness` 为必填物理引导图；`SpecularHitDistance`、`ReactiveMask` 与 `TransparencyOverlay`（线性 RGBA 透明层叠加，A 通道为不透明度）均为可选引导图。调用方无需拆分多路直接/间接辐射度或计算帧间线性深度差。

### 2.7 帧生成（`FrameGeneration`）

```csharp
public struct FrameGenerationDesc
{
    public PixelFormat Format;

    public uint RenderWidth;

    public uint RenderHeight;

    public uint OutputWidth;

    public uint OutputHeight;
}

public struct FrameGenerationArgs
{
    public Texture Color;

    public Texture Depth;

    public Texture MotionVectors;

    public Texture? Hudless;

    public Vector2 JitterOffset;

    public Matrix4x4 View;

    public Matrix4x4 Projection;

    public float DeltaTimeInSeconds;

    public bool Reset;
}

public abstract class FrameGeneration : DisposableObject
{
    public FrameGenerationDesc Desc { get; }

    public Texture Output { get; }

    public void Dispatch(CommandBuffer commandBuffer, FrameGenerationArgs args);
}
```

`RenderWidth/Height` 指定 `Depth` 与 `MotionVectors` 的渲染尺寸；`OutputWidth/Height` 指定 `Color`、可选 `Hudless` 与插值输出 `Output` 的呈现尺寸。

`Color` 是最终呈现的真实帧（可包含 UI）；`Hudless` 是同一帧合成 UI 前的场景颜色，两者在非 UI 区域的内容、曝光与编码一致。

每次 `Dispatch` 生成上一真实帧与当前真实帧之间的一张插值帧并写入 `Output`（处于 `TextureLayout.Sampled`）；首次调用或 `Reset` 为 `true` 时仅初始化历史，该次 `Output` 不用于呈现。

## 3. 帧生成交换链（`FrameGenerationSwapChain`）

使用者可以直接调用 `FrameGeneration` 自行编排插值帧的录制与呈现，也可以使用与核心 `SwapChain` 对齐的 `FrameGenerationSwapChain`。

```csharp
public struct FrameGenerationSwapChainDesc
{
    public Surface Surface;

    public PixelFormat Format;

    public bool IsVerticalSyncEnabled;
}

public struct FrameGenerationSwapChainArgs
{
    public Texture Depth;

    public Texture MotionVectors;

    public Texture? Hudless;

    public Vector2 JitterOffset;

    public Matrix4x4 View;

    public Matrix4x4 Projection;

    public float DeltaTimeInSeconds;

    public bool Reset;
}

public class FrameGenerationSwapChain : DisposableObject
{
    public FrameGenerationSwapChainDesc Desc { get; }

    public Texture Drawable { get; }

    public void Present(FrameGenerationSwapChainArgs args);

    public void Resize(uint width, uint height);

    public void Refresh(Surface surface);
}
```

### 3.1 与核心 `SwapChain` 对齐的使用模型

1. **创建与提交边界一致。** `FrameGenerationSwapChainDesc` 只声明 `Surface`、`Format` 与 `IsVerticalSyncEnabled`（字段顺序对齐核心 `SwapChainDesc`），不耦合渲染分辨率与辅助纹理格式；调用方与使用核心 `SwapChain` 一样自主调用 `commandBuffer.Submit(...)` 提交渲染工作（完整保留 `TimelineValue` 跨队列等待与多命令缓冲区组织能力），随后调用 `swapChain.Present(args)`。切换超分档位导致 `Depth` / `MotionVectors` 尺寸变化时，交换链内部自动重建 `FrameGeneration` 实例，无需销毁重建窗口交换链。创建前要求设备支持帧生成（`UpscalingCapabilities.FrameGenerationSupported` 为 `true`），不启用帧生成时使用核心 `SwapChain`。
2. **统一 `Drawable` 画布。** 调用方每帧将最终呈现画面（含 UI）渲染到 `swapChain.Drawable`（用法与 `SwapChain.Drawable` 一致，绘制完成后转换到 `TextureLayout.Present`）。
3. **直接传入已有辅助纹理，零隐式用途约束。** 调用方提交本帧命令缓冲区后，直接把本帧产出的 `Depth`、`MotionVectors` 及可选 `Hudless`（均只需 `TextureUsages.Sampled` 并保持 `TextureLayout.Sampled`）传给 `Present(args)`。交换链内部在 `GraphicsQueue` 上通过着色器只读采样捕获帧槽快照，随后唤醒后台呈现线程完成插帧与平滑节奏调度上屏。

### 3.2 后台呈现节奏与生命周期

每个 `FrameGenerationSwapChain` 内部管理环形帧槽与独立后台呈现线程：
- **插帧呈现**：在 GPU 完成本帧渲染与帧槽捕获后，后台生成上一真实帧与当前真实帧之间的插值帧，并根据真实帧提交间隔平滑调度“插值帧 → 当前真实帧”等距上屏（开启垂直同步时自动预留 VBlank 安全裕量）；首帧、尺寸重建或 `Reset` 为 `true` 时重置历史并在单次提交中直接呈现当前真实帧。
- **尺寸更新与释放**：`Resize` 与 `Refresh` 等待在途帧完成后更新呈现尺寸或 `Surface` 并重置历史；`Dispose` 安全停止后台线程并释放内部资源。

### 3.3 使用示例

```csharp
context.InitializeUpscaling();

using SuperResolution upscaler = context.CreateSuperResolution(
    SuperResolutionDesc.Quality(context, PixelFormat.R16G16B16A16Float, surface.Width, surface.Height, UpscalingQuality.Quality));

using FrameGenerationSwapChain swapChain = context.CreateFrameGenerationSwapChain(new()
{
    Surface = surface,
    Format = PixelFormat.B8G8R8A8UNorm,
    IsVerticalSyncEnabled = true
});

// 1. 计算当前帧采样抖动，按 upscaler.Desc.RenderWidth / RenderHeight 渲染场景（光栅化使用 jitteredProjection，超分仍传入原始 projection）：
Vector2 jitter = UpscalingHelper.JitterOffset(frameIndex, upscaler.Desc);
Matrix4x4 jitteredProjection = UpscalingHelper.JitterProjection(projection, jitter, upscaler.Desc);

CommandBuffer commandBuffer = context.GraphicsQueue.CommandBuffer();

upscaler.Dispatch(commandBuffer, new()
{
    Color = sceneColor,
    Depth = sceneDepth,
    MotionVectors = motionVectors,
    JitterOffset = jitter,
    Projection = projection,
    DeltaTimeInSeconds = deltaTime
});

// 2. 采样 upscaler.Output 色调映射至 postColor（用作 Hudless），再将最终画面与 UI 绘制到 swapChain.Drawable 并转入 Present 布局：
commandBuffer.Submit();

// 3. 插帧呈现：
swapChain.Present(new()
{
    Depth = sceneDepth,
    MotionVectors = motionVectors,
    Hudless = postColor,
    JitterOffset = jitter,
    View = view,
    Projection = projection,
    DeltaTimeInSeconds = deltaTime
});
```

## 4. 后端映射

公共参数与 DLSS、FSR、MetalFX 的映射如下。下表描述后端可提供的能力，实际可用性由当前设备与图形 API 的能力查询确定。

### 4.1 后端自动选型

内部按图形 API 与硬件能力自动挑选各能力的最优后端：
- DirectX 12 / Vulkan：优先启用 `DLSS`，当前设备不支持的能力自动回退至 `FSR`（例如在仅支持超分与光线重建而不支持原生帧生成的显卡上，自动组合 `DLSS` 超分/光线重建与 `FSR` 帧生成）。
- Metal：启用 `MetalFX`。

| 能力 | DLSS | FSR | MetalFX |
|---|---|---|---|
| 超分 | 是（DX12 / Vulkan） | 是（DX12 / Vulkan） | 是（Metal 4） |
| 光线重建 | 是（DX12 / Vulkan） | 是（DX12，内部解调降噪 + 超分） | 是（Metal 4） |
| 帧生成 | 是（DX12 / Vulkan） | 是（DX12 / Vulkan） | 是（Metal 4） |

### 4.2 投影与颜色参数的自动推导

1. **相机投影分解**：从 `Projection`（行主序行向量透视矩阵）计算 `z = 0` 与 `z = 1` 对应的视空间距离 $d_0, d_1$：
   - 若 $d_0 > d_1$，则判定为反向 Z（`DepthReversed = true`），近平面为 $d_1$，远平面为 $d_0$；否则为正向 Z，近平面为 $d_0$，远平面为 $d_1$。
   - 若远平面距离为非有限值或矩阵分母趋零（$|M_{34} - M_{33}| < \varepsilon$ 或 $|M_{33}| < \varepsilon$），判定为无限远平面（`DepthInfinite = true`）。
   - 同时由 $M_{22}$ 与 $M_{11}$ 提取垂直视场角与宽高比。
2. **曝光与 HDR**：
   - `Exposure is null` 时启用后端 `AutoExposure`；非空时禁用 `AutoExposure` 并绑定该纹理。`PreExposure` 为 `null` 时取 `1.0f`。
   - 超分与光线重建根据 `Color.Desc.Format` 是否为 HDR 格式自动设置底层 HDR 标志。
   - 帧生成根据 `Color.Desc.Format` 自动选择传输函数：`R16G16B16A16Float` 映射为线性 scRGB，`R10G10B10A2UNorm` 映射为 PQ，其余 8 位格式映射为 sRGB。
3. **惰性原生实例与格式缓存**：
   - 输出纹理在构造实例时按 `Desc.Format` 创建并持有；输入纹理格式在首次 `Dispatch`（或输入格式组合/曝光模式变化时）按 `Desc.Format` 读取以创建并预热原生上下文，后续帧直接复用。

### 4.3 超分

| 公共 | DLSS | FSR | MetalFX |
|---|---|---|---|
| `Desc.Format` | `Output` 纹理格式 | `Output` 纹理格式 | `OutputTextureFormat` |
| `Desc` 尺寸 | `Width/Height` = 渲染，`TargetWidth/Height` = 输出 | `MaxRenderSize` / `MaxUpscaleSize` | `InputWidth/Height` / `OutputWidth/Height` |
| `Color` | `Color` | `Color` | `ColorTexture` |
| `Depth` | `Depth` | `Depth` | `DepthTexture` |
| `MotionVectors` | `MotionVectors` | `MotionVectors` | `MotionTexture` |
| `ReactiveMask` | `BiasCurrentColorMask` | `Reactive` | `ReactiveMaskTexture` |
| `Exposure` | `ExposureTexture`（空则 `AutoExposure`） | `Exposure`（空则 `AutoExposure`） | `ExposureTexture`（空则 `IsAutoExposureEnabled`） |
| `Output` | `Output` | `Output` | `OutputTexture` |
| `PreExposure` | `PreExposure`（默认 `1.0f`） | `PreExposure`（默认 `1.0f`） | `PreExposure`（默认 `1.0f`） |
| `JitterOffset` | `JitterOffsetX/Y` | `JitterOffset` | `JitterOffsetX/Y` |
| `DeltaTimeInSeconds` | `FrameTimeDeltaInMsec`（秒转毫秒） | `FrameTimeDelta`（秒转毫秒） | 不读取 |

### 4.4 光线重建

DLSS Ray Reconstruction 与 MetalFX Temporal Denoised Scaler 在一次原生调用中直接读取单路 `Color` 与 `DiffuseAlbedo`、`SpecularAlbedo`、`Normals`、`Roughness`、`SpecularHitDistance` 等引导纹理完成降噪与超分。

FSR 后端在同一个调用方 `CommandBuffer` 中内部串联 **准备（Prepare）→ 降噪（FSR Denoiser）→ 合成（Compose）→ 超分（FSR Upscaler）** 四个阶段，完全在内部吸收信号差异：

1. **Prepare 计算阶段**：
   - 将硬件 `Depth` 通过 `Projection` 还原为线性深度，并结合内部保存的上一帧线性深度（带边缘深度保护的双线性重投影）与 `MotionVectors` 自动计算帧间线性深度差（打包至运动向量 Z 分量），重置帧时深度差置零。
   - 将世界空间 `Normals` 编码为八面体法线并与 `Roughness` 打包。
   - 按 `DiffuseAlbedo` 与 `SpecularAlbedo` 的亮度权重将输入单路带噪 `Color` 自动解调分解为漫反射辐射度与镜面反射辐射度两路信号（A 通道分别写入漫反射估算命中距离与 `SpecularHitDistance` 或粗糙度估算命中距离），并保留自发光/残差能量。
2. **Denoise 阶段**：调用原生 `FSRDenoiser` 对解调出的漫反射与镜面信号执行时域与空域降噪。
3. **Compose 计算阶段**：将降噪后的漫反射与镜面辐射度重新乘以 `DiffuseAlbedo`、`SpecularAlbedo` 并加回残差项得到干净的线性场景颜色；若提供了 `TransparencyOverlay`，将其按 Alpha 合成叠加到颜色上并提取透明遮罩。
4. **Upscale 阶段**：将合成后的干净颜色送入内部 `FSRSuperResolution`，输出最终的 `Output`。

### 4.5 帧生成

| 公共 | DLSS | FSR | MetalFX |
|---|---|---|---|
| `Desc.Format` | `NativeBackbufferFormat` 及 `Output` 纹理格式 | `BackBufferFormat` 及 `Output` 纹理格式 | `OutputTextureFormat` |
| `RenderWidth/Height` | `RenderWidth/Height` 及深度、运动向量尺寸 | `MaxRenderSize` 与当前 `RenderSize` | `InputWidth/Height` 及 `ContentWidth/Height` |
| `OutputWidth/Height` | `Width/Height` | `DisplaySize` 与 `GenerationRect` | `OutputWidth/Height` |
| `Color` | `Backbuffer` | `PresentColor` | 有 `Hudless` 时为 `UiTexture`，否则为 `ColorTexture` |
| `Hudless` | `Hudless` | `HUDLessColor` | `ColorTexture`（启用 `IsUITextureComposited`） |
| `Output` | `OutputInterpFrame` | `Outputs[0]` | `OutputTexture` |
| `DeltaTimeInSeconds` | 不直接读取 | `FrameTimeDelta`（秒转毫秒） | `DeltaTime` |

## 5. 取舍

1. **不做纯降噪。** `RayReconstruction` 始终输出同时完成降噪与超分的图像。
2. **光线重建不向公共层暴露分路辐射度。** 单路 `Color` + 几何材质引导图是三家后端的最大公约物理模型，FSR 分通道降噪所需的线性深度差与辐射度解调完全由后端 Compute Shader 内部完成。
3. **帧生成只做单帧插值。** 每次调用生成一张中间帧。
4. **`FrameGenerationSwapChain` 与核心 `SwapChain` 职责正交。** `FrameGenerationSwapChain` 专用于插帧呈现，调用方自主提交渲染 `CommandBuffer` 后调用 `Present(args)`，内部自动在帧槽中只读采样辅助纹理并调度后台插帧上屏，不再绑定渲染尺寸或辅助纹理格式。
