# 超分扩展设计（DLSS / MetalFX / FSR）

`Zenith.NET.Extensions.Upscaling` 在现有三个类型（`TemporalUpscaler`、`DenoisingUpscaler`、`FrameGenerator`）上重新定义 `Desc` / `Args`，并新增一个帧生成交换链。目标是：一套与后端无关、按语义定义参数、面向使用者的 API。

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

    MetalFX,

    FSR
}

// 仅用于创建前的尺寸查询，不进入 Desc。
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
| 运动向量 | UV 空间，`PreviousUV - CurrentUV`，与输入分辨率同尺寸 |
| 深度 | 硬件深度，非线性的视口深度 |
| 颜色 | 线性空间，输出分辨率；超分与降噪时含已合成的 UI，帧生成时不含（见 §2.6） |
| 反照度 | 线性空间，不含光照 |
| 抖动 | 像素单位，相对于输入分辨率 |

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

    public uint InputWidth;
    public uint InputHeight;
    public uint OutputWidth;
    public uint OutputHeight;

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

入口沿用现有形式：

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
    }
}
```

## 3. 帧生成交换链

需要帧生成的使用者有两个选择：直接用 `FrameGenerator`，自己在渲染循环里注入插值帧；或者把交换链换成 `FrameGenerationSwapChain`，只提供两帧之间的参数。后者是并行的第二种用法，不是 `FrameGenerator` 里的模式开关。

```csharp
public struct FrameGenerationSwapChainDesc
{
    public PixelFormat DepthFormat;
    public PixelFormat MotionVectorsFormat;
    public PixelFormat? HudlessFormat;

    public uint InputWidth;
    public uint InputHeight;

    public bool IsHdr;
}

public struct FrameGenerationSwapChainArgs
{
    public Texture Depth;
    public Texture MotionVectors;
    public Texture? Hudless;

    public Matrix4x4 WorldToView;
    public Matrix4x4 ViewToClip;

    public Vector2 JitterOffset;

    public float DeltaTimeInSeconds;

    public bool Reset;
}

public class FrameGenerationSwapChain(GraphicsContext context, FrameGenerationSwapChainDesc desc, SwapChain swapChain, FrameGenerator frameGenerator) : DisposableObject
{
    public GraphicsContext Context { get; }
    public ref readonly FrameGenerationSwapChainDesc Desc { get; }
    public CommandQueue Queue { get; }

    public Texture Drawable { get; }
    public SwapChain SwapChain { get; }

    public void Present(FrameGenerationSwapChainArgs args);
    public void Resize(uint width, uint height);
    public void Refresh(Surface surface);
}
```

交换链接管调用方已经创建的 `SwapChain`，不另建一个，因此 `Desc` 里没有 `SwapChainDesc`，创建入口是 `CreateFrameGenerationSwapChain(swapChain, desc)`。深度、运动向量与 UI 层都随每帧参数传入：它们由调用方的渲染流程产生，交换链不自建，也就不要求调用方把渲染目标改成交换链的内部纹理。

使用者的代码与原来几乎一致：渲染到 `Drawable`、调用 `Present`。交换链自己只持有两张输出尺寸的纹理，一张保存不带插值的真实帧，一张接收生成的插值帧。

**呈现顺序。** `Present` 依次做四件事：

1. 把 `Drawable` 拷贝到内部真实帧纹理，然后调用底层 `swapChain.Present()`，真实帧上屏；
2. 用真实帧纹理、`Depth`、`MotionVectors`、`Hudless` 与两帧矩阵调用 `FrameGenerator`，结果写入内部插值纹理；
3. 把插值纹理拷贝到下一位后缓冲，再调用一次 `swapChain.Present()`，插值帧上屏。

两次上屏都由底层交换链负责，垂直同步就是节拍源。`Reset` 为真时跳过第 2、3 步，因为此时还没有可插值的历史。

## 4. 后端映射

当前扩展实现 DLSS 与 MetalFX 两端，FSR 只在下表中给出映射口径，尚未实现。`UpscalingApi` 也只包含这两项——加入 FSR 后端时再扩展枚举，避免出现选了却没有实现的取值。

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

这一差异是实现的复杂度，不是 API 的差异：三个 `DenoisingUpscalerArgs` 的字段集完全相同。

### 4.4 帧生成

DLSS-G、MetalFX Frame Interpolator、FSR Frame Generation 都是单帧插值，公共字段一一对应：前两帧的视图矩阵决定相机运动，`JitterOffset` 与 `DeltaTimeInSeconds` 决定插值时刻，`Reset` 用于重新初始化历史。

三家的帧生成都不要求接管交换链。FSR 提供了交换链接管形式，本扩展不使用它——`FrameGenerationSwapChain` 由扩展自己实现，三个后端共用同一套呈现逻辑。

## 5. 取舍

1. **不做纯降噪。** 与 MetalFX 的能力面对齐，`DenoisingUpscaler` 始终包含超分。
2. **帧生成只做单帧。** 多帧生成会把 `Args` 变成数组语义，收益不抵复杂度。
3. **运动向量固定 UV 空间。** 移除了公共的 `MotionVectorScale`；换算在后端完成。
4. **FSR 的信号拆分放在后端内部。** 公共参数不暴露 G-buffer 的信号结构，代价是 FSR 降噪路径多两次全屏 pass。
5. **帧生成交换链由扩展实现。** 不使用 FSR 自带的交换链接管，三家共用同一套呈现节奏，行为一致。
