# 超分扩展设计（DLSS / MetalFX / FSR）

`Zenith.NET.Extensions.Upscaling` 提供超分、降噪 + 超分、帧生成与帧生成交换链，采用与后端无关、按语义定义参数的公共 API。

## 1. 原则

1. **参数按语义定义，不按厂商定义。** 字段名来自图形学概念（颜色、深度、运动向量、抖动），不出现 `NGX`、`MTLFX`、`ffx` 之类的名字。映射到原生结构的细节属于实现。
2. **少量参数只有个别后端读取，是可以接受的。** 不为了参数的整齐把工作推给后端。
3. **资源只传整体对象。** 只有 `Texture` 和整个资源的传入，没有子资源、层级、范围、矩形一类的再划分。
4. **三种能力，不做纯降噪。** 超分、降噪 + 超分、帧生成。帧生成只生成一帧。
5. **公共类型不含厂商类型。** 厂商差异通过 `UpscalingApi` 选择和内部实现吸收。

## 2. 公共 API

### 2.1 后端、质量档、尺寸

```csharp
public enum UpscalingApi
{
    DLSS,

    FSR,

    MetalFX
}

public enum UpscalingMode
{
    Native,

    Quality,

    Balanced,

    Performance,

    UltraPerformance
}

public readonly struct UpscalingInputSize(uint width, uint height)
{
    public readonly uint Width = width;

    public readonly uint Height = height;
}
```

`UpscalingMode` 是给调用方在还没建对象时算渲染分辨率的：`GetTemporalUpscalerInputSize(outputWidth, outputHeight, mode)`。真正决定行为的是 `Desc` 里的 `InputWidth/Height` 和 `OutputWidth/Height`，不是质量档。

### 2.2 输入约定

这些约定对所有后端一致，因此不进入 `Desc`，也不做成开关：

| 输入 | 约定 |
|---|---|
| 运动向量 | UV 空间，`PreviousUV - CurrentUV`，与渲染分辨率同尺寸 |
| 深度 | 硬件深度，非线性的视口深度 |
| 颜色 | 线性空间，尺寸由各类型的 `Desc` 指定；超分与降噪时含已合成的 UI，帧生成时不含（见 §2.6） |
| 反照度 | 线性空间，不含光照 |
| 抖动 | 像素单位，相对于渲染分辨率 |

深度、运动向量、颜色、反照度、抖动这五项由字段语义和格式完全确定，所以不进 `Desc`。`IsHdr` 是个例外：`PixelFormat` 分不出 sRGB 编码与 PQ、scRGB 两种 HDR 表示，而这决定后端的传输函数。约定由 `Output` 格式与 `IsHdr` 共同推出——不置位时按 sRGB 处理；置位时 `R10G10B10A2UNorm` 按 PQ、`R16G16B16A16Float` 按 scRGB。

### 2.3 输入标志

```csharp
[Flags]
public enum UpscalingInputFlags
{
    None = 0,

    DepthReversed = 1 << 0,

    AutoExposure = 1 << 1
}
```

- `DepthReversed`：深度为反向 Z。
- `AutoExposure`：`Exposure` 纹理有效，后端自行从曝光纹理读取曝光值。不置该位时使用 `PreExposure` 标量。

### 2.4 超分

```csharp
public struct TemporalUpscalerDesc
{
    public PixelFormat ColorFormat;

    public PixelFormat DepthFormat;

    public PixelFormat MotionVectorsFormat;

    public PixelFormat? ReactiveMaskFormat;

    public PixelFormat OutputFormat;

    public bool IsHdr;

    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public UpscalingInputFlags Flags;
}

public struct TemporalUpscalerArgs
{
    public Texture Color;

    public Texture Depth;

    public Texture MotionVectors;

    public Texture? ReactiveMask;
    
    public Texture? Exposure;

    public Texture Output;

    public Vector2 JitterOffset;

    public Matrix4x4 WorldToView;

    public Matrix4x4 ViewToClip;

    public float PreExposure;

    public bool Reset;
}
```

`ReactiveMask` 是可选的反遮罩，用于抑制动态物体上的拖影。`Exposure` 仅在 `Flags` 含 `AutoExposure` 时读取。

### 2.5 降噪 + 超分

```csharp
public struct DenoisingUpscalerDesc
{
    public PixelFormat ColorFormat;

    public PixelFormat DepthFormat;

    public PixelFormat MotionVectorsFormat;

    public PixelFormat DiffuseAlbedoFormat;

    public PixelFormat SpecularAlbedoFormat;

    public PixelFormat NormalsFormat;

    public PixelFormat RoughnessFormat;

    public PixelFormat SpecularHitDistanceFormat;

    public PixelFormat? ReactiveMaskFormat;

    public PixelFormat? TransparencyOverlayFormat;

    public PixelFormat OutputFormat;

    public bool IsHdr;

    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public UpscalingInputFlags Flags;
}

public struct DenoisingUpscalerArgs
{
    public Texture Color;

    public Texture Depth;

    public Texture MotionVectors;

    public Texture DiffuseAlbedo;

    public Texture SpecularAlbedo;

    public Texture Normals;

    public Texture Roughness;

    public Texture SpecularHitDistance;

    public Texture? ReactiveMask;

    public Texture? TransparencyOverlay;

    public Texture? Exposure;

    public Texture Output;

    public Vector2 JitterOffset;

    public Matrix4x4 WorldToView;

    public Matrix4x4 ViewToClip;

    public float PreExposure;

    public bool Reset;
}
```

`InputWidth/Height` 必须小于 `OutputWidth/Height`；相等时创建期抛 `InvalidOperationException`——该类型是降噪 **加** 超分，不做纯降噪。

### 2.6 帧生成

```csharp
public struct FrameGeneratorDesc
{
    public PixelFormat ColorFormat;

    public PixelFormat DepthFormat;

    public PixelFormat MotionVectorsFormat;

    public PixelFormat? HudlessFormat;

    public PixelFormat OutputFormat;

    public bool IsHdr;

    public uint Width;

    public uint Height;

    public uint AuxiliaryWidth;

    public uint AuxiliaryHeight;

    public UpscalingInputFlags Flags;
}

public struct FrameGeneratorArgs
{
    public Texture Color;

    public Texture Depth;

    public Texture MotionVectors;

    public Texture? Hudless;

    public Texture Output;

    public Vector2 JitterOffset;

    public Matrix4x4 WorldToView;

    public Matrix4x4 ViewToClip;

    public float DeltaTimeInSeconds;

    public float PreExposure;

    public bool Reset;
}
```

`Width/Height` 指定 `Color`、`Hudless` 与插值结果 `Output` 的帧尺寸；`AuxiliaryWidth/Height` 指定辅助纹理 `Depth`、`MotionVectors` 的共同尺寸。

`Color` 是不含 UI 的场景颜色，`Hudless` 是含 UI 的合成结果；`HudlessFormat` 为 `null` 时后端把 `Color` 当作已含 UI 的图像。`DeltaTimeInSeconds` 是两帧之间的实际间隔，帧生成需要它把插值帧放到正确的时间点。

### 2.7 能力与入口

```csharp
public class UpscalingCapabilities
{
    public bool TemporalUpscalerSupported { get; }

    public bool DenoisingUpscalerSupported { get; }

    public bool FrameGeneratorSupported { get; }
}
```

通过 `GraphicsContext` 扩展方法提供入口：

```csharp
public static class Extensions
{
    extension(GraphicsContext context)
    {
        public void InitializeUpscaling(params ReadOnlySpan<UpscalingApi> apis);

        public UpscalingCapabilities UpscalingCapabilities { get; }

        public UpscalingInputSize GetTemporalUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode);

        public UpscalingInputSize GetDenoisingUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode);

        public TemporalUpscaler CreateTemporalUpscaler(TemporalUpscalerDesc desc);

        public DenoisingUpscaler CreateDenoisingUpscaler(DenoisingUpscalerDesc desc);

        public FrameGenerator CreateFrameGenerator(FrameGeneratorDesc desc);

        public FrameGenerationSwapChain CreateFrameGenerationSwapChain(FrameGenerationSwapChainDesc desc);
    }
}
```

## 3. 帧生成交换链

使用者可以直接调用 `FrameGenerator`，自行安排插值帧的提交与呈现；也可以使用 `FrameGenerationSwapChain`，写入输入纹理并每帧调用一次 `Present(args)`。

`FrameGenerationSwapChain` 与 `SwapChain` 是两套独立的交换链。它根据自己的 `Desc` 创建呈现资源，内部创建并持有帧生成器；创建时只需要 `GraphicsContext` 和描述。

```csharp
public struct FrameGenerationSwapChainDesc
{
    public Surface Surface;

    public bool IsVerticalSyncEnabled;

    public PixelFormat ColorFormat;

    public PixelFormat DepthFormat;

    public PixelFormat MotionVectorsFormat;

    public PixelFormat? HudlessFormat;

    public bool IsHdr;

    public uint AuxiliaryWidth;

    public uint AuxiliaryHeight;

    public UpscalingInputFlags Flags;
}

public struct FrameGenerationSwapChainArgs
{
    public Vector2 JitterOffset;

    public Matrix4x4 WorldToView;

    public Matrix4x4 ViewToClip;

    public float DeltaTimeInSeconds;

    public float PreExposure;

    public bool Reset;
}

public class FrameGenerationSwapChain(GraphicsContext context, FrameGenerationSwapChainDesc desc) : DisposableObject
{
    public GraphicsContext Context { get; }

    public ref readonly FrameGenerationSwapChainDesc Desc { get; }

    public Texture Color { get; }

    public Texture Depth { get; }

    public Texture MotionVectors { get; }

    public Texture? Hudless { get; }

    public void Present(FrameGenerationSwapChainArgs args);

    public void Resize(uint width, uint height);

    public void Refresh(Surface surface);
}
```

创建入口是 §2.7 的 `context.CreateFrameGenerationSwapChain(desc)`。`Surface` 指定呈现目标，`IsVerticalSyncEnabled` 控制垂直同步；颜色尺寸取 `Surface.Width/Height`，`ColorFormat` 用于 `Color` 与插值输出。其余格式、辅助纹理尺寸、`IsHdr` 与 `Flags` 用于内部帧生成器。

**输入纹理。** 交换链创建并拥有当前帧的整组纹理，调用方每帧获取并写入它们。对应关系如下，颜色与 UI 的语义见 §2.6：

| 属性 | 格式 | 尺寸 | 对应帧生成输入 |
|---|---|---|---|
| `Color` | `ColorFormat` | `Surface.Width/Height` | `Color` |
| `Depth` | `DepthFormat` | `AuxiliaryWidth/Height` | `Depth` |
| `MotionVectors` | `MotionVectorsFormat` | `AuxiliaryWidth/Height` | `MotionVectors` |
| `Hudless` | `HudlessFormat` | `Surface.Width/Height` | `Hudless`；格式为 `null` 时属性也为 `null` |

同一次 `Present` 前取得的属性属于同一帧。调用方可以直接渲染到这些纹理，也可以从已有渲染目标填入；`Args` 只包含本帧的数值参数。移交后由内部保证纹理在后台使用结束前不会被下一帧覆盖，调用方每帧重新获取纹理，不单独释放它们。

**后台呈现。** 每个交换链持有一个运行在独立线程上的长期 `Task`，负责帧生成、提交和呈现。调用方完成本帧渲染后调用 `Present(args)`，将这一帧的纹理与参数交给后台；`Present` 返回不表示已经上屏。

后台生成上一真实帧与当前真实帧之间的一张插值帧，按“上一真实帧 → 插值帧 → 当前真实帧”的顺序呈现，并管理呈现节奏。首次提交或 `Reset` 为真时重置历史，只呈现当前真实帧。调用方负责渲染与命令提交，插帧与呈现调度由交换链内部完成。

**尺寸与释放。** `Resize` 与 `Refresh` 分别更新呈现尺寸与 Surface，并由内部更新相关资源、重置历史。`Dispose` 负责结束后台任务并释放内部资源。

使用示例：

```csharp
using FrameGenerationSwapChain swapChain = context.CreateFrameGenerationSwapChain(new()
{
    Surface = surface,
    IsVerticalSyncEnabled = true,
    ColorFormat = colorFormat,
    DepthFormat = depthFormat,
    MotionVectorsFormat = motionVectorsFormat,
    AuxiliaryWidth = renderWidth,
    AuxiliaryHeight = renderHeight,
    Flags = UpscalingInputFlags.DepthReversed
});

// 每帧获取并写入这些纹理，然后完成本帧的命令提交。
Texture color = swapChain.Color;
Texture depth = swapChain.Depth;
Texture motionVectors = swapChain.MotionVectors;

swapChain.Present(new()
{
    JitterOffset = jitterOffset,
    WorldToView = worldToView,
    ViewToClip = viewToClip,
    DeltaTimeInSeconds = deltaTimeInSeconds,
    PreExposure = 1.0f,
    Reset = reset
});
```

## 4. 后端映射

公共参数与 DLSS、MetalFX、FSR 的映射如下：

### 4.1 能力

| | DLSS | MetalFX | FSR |
|---|---|---|---|
| 超分 | 是 | 是 | 是 |
| 降噪 + 超分 | 是 | 是 | 是（两段 pass） |
| 帧生成 | 是 | 是 | 是 |

### 4.2 超分

| 公共 | DLSS | MetalFX | FSR |
|---|---|---|---|
| `Desc` 尺寸 | `Width/Height` = 输出，`RenderWidth/Height` = 输入 | 输入/输出宽度 | `MaxRenderSize` / `MaxUpscaleSize` |
| `Color` | `Color` | `ColorTexture` | `Color` |
| `Depth` | `Depth` | `DepthTexture` | `Depth` |
| `MotionVectors` | `MotionVectors` | `MotionTexture` | `MotionVectors` |
| `ReactiveMask` | `ReactiveMask` | `ReactiveMaskTexture` | `ReactiveMask` |
| `Exposure` | `Exposure` | `ExposureTexture` | —— |
| `PreExposure` | —— | `PreExposure` | `PreExposure`（曝光缩放） |
| 抖动 | `JitterOffset` | `JitterOffsetX/Y` | `JitterOffset` |
| 质量档 | `NGXPerfQualityValue` | `MTLFXScalingMode` | 由尺寸决定 |

运动向量统一是 UV 空间，三家后端各自乘以输入或输出尺寸，公共层不做换算。

### 4.3 降噪 + 超分

DLSS 的 RR 与 MetalFX 的 Denoised Scaler 是一次调用完成降噪与超分，输入直接对应公共字段。

FSR 的降噪器（Ray Regeneration）与超分器是两个独立上下文，且降噪器只做降噪。因此 FSR 后端在内部按两段执行：

1. 降噪上下文处理 G-buffer，输出与输入同尺寸的降噪结果；
2. 超分上下文把降噪结果放大到输出尺寸。

两段都在调用方的命令缓冲里录制，对使用者表现为一次 `Dispatch`。FSR 降噪器需要按信号拆分的输入（漫反射、镜面反射等），后端内部用两到三次全屏 pass 把公共输入拆开与合回；公共 `Color` 始终是合成辐射度，与另外两家同义。

`DenoisingUpscalerArgs` 的字段集在三个后端完全相同。

### 4.4 帧生成

DLSS-G、MetalFX Frame Interpolator、FSR Frame Generation 都是单帧插值，公共字段一一对应：前两帧的视图矩阵决定相机运动，`JitterOffset` 与 `DeltaTimeInSeconds` 决定插值时刻，`Reset` 用于重新初始化历史。

三家的帧生成都不要求接管交换链。FSR 提供了交换链接管形式，本扩展不使用它——`FrameGenerationSwapChain` 由扩展自己实现，三个后端共用同一套呈现逻辑。

## 5. 取舍

1. **不做纯降噪。** 与 MetalFX 的能力面对齐，`DenoisingUpscaler` 始终包含超分。
2. **帧生成只做单帧。** 多帧生成会把 `Args` 变成数组语义，收益不抵复杂度。
3. **运动向量固定 UV 空间。** 尺寸换算由后端完成。
4. **FSR 的信号拆分放在后端内部。** 公共参数不暴露 G-buffer 的信号结构，代价是 FSR 降噪路径多两次全屏 pass。
5. **两套独立交换链。** `FrameGenerationSwapChain` 从 Surface 创建，拥有整组输入纹理和后台提交任务；调用方完成渲染后每帧调用一次 `Present`，由扩展负责插值帧和真实帧的呈现。
