# 超分扩展设计（DLSS / MetalFX / FSR）

`Zenith.NET.Extensions.Upscaling` 提供超分、光线重建（降噪 + 超分）、帧生成与帧生成交换链，采用与后端无关、按语义定义参数的公共 API。

## 1. 原则

1. **参数按语义定义，不按厂商定义。** 字段名来自图形学概念（颜色、深度、运动向量、抖动），不出现 `NGX`、`MTLFX`、`ffx` 之类的名字。映射到原生结构的细节属于实现。
2. **少量参数只有个别后端读取，是可以接受的。** 不为了参数的整齐把工作推给后端。
3. **资源只传整体对象。** 只有 `Texture` 和整个资源的传入，没有子资源、层级、范围、矩形一类的再划分。
4. **三种能力，不做纯降噪。** 超分、光线重建、帧生成。光线重建包含降噪与超分，帧生成只生成一帧。
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

`UpscalingMode` 用于查询建议的渲染分辨率：`GetSuperResolutionInputSize(outputWidth, outputHeight, mode)`。超分对象的实际尺寸由 `Desc.InputWidth/Height` 与 `Desc.OutputWidth/Height` 指定。`Native` 表示原分辨率时域重建，只用于 `SuperResolution`；`GetRayReconstructionInputSize` 接受其余四个档位。

### 2.2 输入约定

这些约定对所有后端一致：

| 输入 | 约定 |
|---|---|
| 运动向量 | UV 空间，`PreviousUV - CurrentUV`，包含相机与物体运动，不包含抖动；与深度同尺寸 |
| 深度 | 硬件深度，非线性的视口深度；方向与远平面由 `Flags` 指定 |
| 超分、光线重建颜色 | 线性场景颜色，不含 UI；`Color` 为渲染尺寸，`Output` 为超分尺寸 |
| 帧生成颜色 | 已完成颜色处理的真实帧，编码见 §2.6；帧生成不执行超分 |
| 反照度 | 线性反射率，不含光照；漫反射与镜面反射分别提供 |
| 法线 | RGB 保存世界空间单位向量，不做打包或八面体编码 |
| 粗糙度 | R 通道保存 `[0, 1]` 的材质粗糙度 |
| 距离与矩阵 | 距离以米为单位；矩阵采用 `System.Numerics.Matrix4x4` 约定，投影矩阵不含抖动并与深度对应 |
| 抖动 | 像素单位，相对于深度和运动向量的尺寸 |
| 曝光 | `Exposure` 为 1×1 的 `R16Float` 纹理，保存当前曝光倍率；`PreExposure` 为已乘入线性颜色的预曝光倍率，未预曝光时传 `1.0f` |

超分与光线重建的 `IsHdr` 表示线性颜色包含 HDR 数值，不指定 sRGB、PQ 等传输函数；色调映射、呈现编码与 UI 合成在这些操作之后完成。帧生成的呈现颜色约定见 §2.6。

纹理尺寸必须与对应描述一致，整张纹理参与处理。带有可空格式的纹理以 `Desc` 为准：格式为 `null` 时对应参数为 `null`，指定格式时每帧提供匹配的完整纹理。格式组合和所选后端必需的参数在创建时验证；每帧纹理与数值在调用时验证。

### 2.3 输入标志

```csharp
[Flags]
public enum UpscalingInputFlags
{
    None = 0,

    DepthReversed = 1 << 0,

    AutoExposure = 1 << 1,

    DepthInfinite = 1 << 2
}
```

- `DepthReversed`：深度为反向 Z。
- `AutoExposure`：由后端根据线性场景颜色计算曝光，`Exposure` 为 `null`；不置该位时读取 `Exposure`，未提供纹理时使用曝光倍率 `1.0f`。`PreExposure` 独立描述颜色中已有的预曝光，不代替曝光纹理。
- `DepthInfinite`：投影使用无限远平面。

`AutoExposure` 用于超分与光线重建；帧生成及其交换链只接受两个深度标志。

### 2.4 超分

```csharp
public struct SuperResolutionDesc
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

public struct SuperResolutionArgs
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

    public float DeltaTimeInSeconds;

    public float PreExposure;

    public bool Reset;
}
```

`InputWidth/Height` 不大于 `OutputWidth/Height`，相等时执行原分辨率时域重建。除 1×1 的 `Exposure` 外，输入纹理均为渲染尺寸。

`ReactiveMask` 是可选的反应性遮罩，R 通道值为 `[0, 1]`，数值越大越减少对历史颜色的依赖。`DeltaTimeInSeconds` 是相邻真实帧的时间间隔；曝光遵循 §2.3。

### 2.5 光线重建（降噪 + 超分）

```csharp
public struct RayReconstructionDesc
{
    public PixelFormat ColorFormat;

    public PixelFormat DepthFormat;

    public PixelFormat MotionVectorsFormat;

    public PixelFormat DiffuseAlbedoFormat;

    public PixelFormat SpecularAlbedoFormat;

    public PixelFormat NormalsFormat;

    public PixelFormat RoughnessFormat;

    public PixelFormat SpecularHitDistanceFormat;

    public PixelFormat? LinearDepthDeltaFormat;

    public PixelFormat? DirectDiffuseRadianceFormat;

    public PixelFormat? DirectSpecularRadianceFormat;

    public PixelFormat? IndirectDiffuseRadianceFormat;

    public PixelFormat? IndirectSpecularRadianceFormat;

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

public struct RayReconstructionArgs
{
    public Texture Color;

    public Texture Depth;

    public Texture MotionVectors;

    public Texture DiffuseAlbedo;

    public Texture SpecularAlbedo;

    public Texture Normals;

    public Texture Roughness;

    public Texture SpecularHitDistance;

    public Texture? LinearDepthDelta;

    public Texture? DirectDiffuseRadiance;

    public Texture? DirectSpecularRadiance;

    public Texture? IndirectDiffuseRadiance;

    public Texture? IndirectSpecularRadiance;

    public Texture? ReactiveMask;

    public Texture? TransparencyOverlay;

    public Texture? Exposure;

    public Texture Output;

    public Vector2 JitterOffset;

    public Matrix4x4 WorldToView;

    public Matrix4x4 ViewToClip;

    public float DeltaTimeInSeconds;

    public float PreExposure;

    public bool Reset;
}
```

`InputWidth/Height` 必须分别小于 `OutputWidth/Height`；相等或更大时创建期抛 `InvalidOperationException`。除 1×1 的 `Exposure` 外，输入纹理均为渲染尺寸，`Output` 为超分尺寸。

`Color` 是待降噪的完整线性场景颜色。`SpecularHitDistance` 保存镜面反射射线的命中距离；`TransparencyOverlay` 是与场景对应的线性 RGBA 透明层，A 通道为不透明度。曝光与帧间隔的含义与 §2.4 相同。

以下可选输入由渲染流程生成，FSR 降噪路径读取，DLSS 与 MetalFX 不读取：

| 输入 | 语义 |
|---|---|
| `LinearDepthDelta` | 当前表面点在前后两帧的带符号视空间深度差：`PreviousLinearDepth - CurrentLinearDepth` |
| `DirectDiffuseRadiance` | 直接漫反射辐射度 |
| `DirectSpecularRadiance` | 直接镜面反射辐射度 |
| `IndirectDiffuseRadiance` | 间接漫反射辐射度；A 通道保存间接漫反射射线的命中距离 |
| `IndirectSpecularRadiance` | 间接镜面反射辐射度；A 通道保存镜面反射射线的命中距离 |

辐射度纹理的 RGB 不含首个反射点的反照度调制，预曝光倍率与 `Color` 一致。`Color` 包含这些信号乘以对应反照度后的贡献，也包含其余场景颜色。FSR 要求 `LinearDepthDelta` 和至少一种辐射度信号；所配置的信号每帧提供，未配置的场景贡献保持在 `Color` 中。

### 2.6 帧生成

```csharp
public struct FrameGenerationDesc
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

public struct FrameGenerationArgs
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

    public float MinLuminanceInNits;

    public float MaxLuminanceInNits;

    public bool Reset;
}
```

`Width/Height` 指定 `Color`、`Hudless` 与插值结果 `Output` 的帧尺寸；`AuxiliaryWidth/Height` 指定辅助纹理 `Depth`、`MotionVectors` 的共同尺寸。

`Color` 是最终呈现的真实帧，可以包含 UI；`Hudless` 是同一帧合成 UI 前的场景颜色。两者的非 UI 区域内容、曝光与编码一致。`HudlessFormat` 为 `null` 时只提供 `Color`。

帧生成使用呈现颜色：`IsHdr = false` 时为 sRGB 编码的 SDR；`IsHdr = true` 时，`R10G10B10A2UNorm` 使用 PQ，`R16G16B16A16Float` 使用线性 scRGB。`Color`、`Hudless` 与 `Output` 使用相同的颜色编码。HDR 帧的 `MinLuminanceInNits`、`MaxLuminanceInNits` 给出显示亮度范围，满足 `0 ≤ MinLuminanceInNits < MaxLuminanceInNits`；SDR 不读取这两个值。

`DeltaTimeInSeconds` 是相邻真实帧的实际时间间隔。每次 `Dispatch` 生成一张插值帧，前一帧状态由对象保存；首次调用或 `Reset` 为真时初始化历史，该次插值输出不用于呈现。

### 2.7 能力与入口

```csharp
public class UpscalingCapabilities
{
    public bool SuperResolutionSupported { get; }

    public bool RayReconstructionSupported { get; }

    public bool FrameGenerationSupported { get; }
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

        public UpscalingInputSize GetSuperResolutionInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode);

        public UpscalingInputSize GetRayReconstructionInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode);

        public SuperResolution CreateSuperResolution(SuperResolutionDesc desc);

        public RayReconstruction CreateRayReconstruction(RayReconstructionDesc desc);

        public FrameGeneration CreateFrameGeneration(FrameGenerationDesc desc);

        public FrameGenerationSwapChain CreateFrameGenerationSwapChain(FrameGenerationSwapChainDesc desc);
    }
}
```

初始化按传入的 `UpscalingApi` 顺序分别选择三种能力的后端；能力查询反映当前 `GraphicsContext` 的实际支持情况。帧生成交换链使用所选帧生成后端。

## 3. 帧生成交换链

使用者可以直接调用 `FrameGeneration`，自行安排插值帧的提交与呈现；也可以使用 `FrameGenerationSwapChain`，写入输入纹理并每帧调用一次 `Present(args)`。

`FrameGenerationSwapChain` 与 `SwapChain` 是两套独立的交换链。它根据自己的 `Desc` 创建呈现资源，内部创建并持有帧生成对象；创建时只需要 `GraphicsContext` 和描述。

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

    public float MinLuminanceInNits;

    public float MaxLuminanceInNits;

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

创建入口是 §2.7 的 `context.CreateFrameGenerationSwapChain(desc)`。`Surface` 指定呈现目标，`IsVerticalSyncEnabled` 控制垂直同步；颜色尺寸取 `Surface.Width/Height`，`ColorFormat` 用于 `Color` 与插值输出。其余格式、辅助纹理尺寸、`IsHdr` 与 `Flags` 用于内部帧生成对象。`Args` 中的帧间隔与 HDR 亮度范围遵循 §2.6。

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

**尺寸与释放。** `Resize` 与 `Refresh` 分别更新呈现尺寸与 Surface，并由内部更新相关资源、重置历史；辅助纹理尺寸保持为 `Desc.AuxiliaryWidth/Height`。辅助纹理尺寸或格式变化时重新创建交换链。`Dispose` 负责结束后台任务并释放内部资源。

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
    Reset = reset
});
```

## 4. 后端映射

公共参数与 DLSS、MetalFX、FSR 的映射如下。下表描述后端可提供的能力，实际可用性由当前设备与图形 API 的能力查询确定。

### 4.1 能力

| | DLSS | MetalFX | FSR |
|---|---|---|---|
| 超分 | 是 | 是 | 是 |
| 光线重建 | 是 | 是 | 是（DirectX 12，降噪后超分） |
| 帧生成 | 是 | 是 | 是 |

### 4.2 超分

| 公共 | DLSS | MetalFX | FSR |
|---|---|---|---|
| `Desc` 尺寸 | `Width/Height` = 输入，`TargetWidth/Height` = 输出 | 输入/输出宽度 | `MaxRenderSize` / `MaxUpscaleSize` |
| `Color` | `Color` | `ColorTexture` | `Color` |
| `Depth` | `Depth` | `DepthTexture` | `Depth` |
| `MotionVectors` | `MotionVectors` | `MotionTexture` | `MotionVectors` |
| `ReactiveMask` | `BiasCurrentColorMask` | `ReactiveMaskTexture` | `Reactive` |
| `Exposure` | `ExposureTexture` | `ExposureTexture` | `Exposure` |
| `PreExposure` | `PreExposure` | `PreExposure` | `PreExposure` |
| `AutoExposure` | 自动曝光创建标志 | `IsAutoExposureEnabled` | 自动曝光创建标志 |
| `IsHdr` | HDR 创建标志 | 读取线性颜色，无对应开关 | HDR 创建标志 |
| `DepthInfinite` | 无对应创建标志 | 无对应创建标志 | 无限远深度创建标志 |
| `JitterOffset` | `JitterOffsetX/Y` | `JitterOffsetX/Y` | `JitterOffset` |
| `DeltaTimeInSeconds` | `FrameTimeDeltaInMsec`，秒转换为毫秒 | 不读取 | `FrameTimeDelta`，秒转换为毫秒 |
| 质量档 | 查询建议尺寸并映射 `PerfQualityValue` | 根据支持的缩放范围计算尺寸 | 查询建议尺寸，由尺寸配置上下文 |

运动向量按 §2.2 的 UV 约定提供，后端转换为对应接口需要的单位。曝光纹理保持同一倍率语义。[自动曝光与手动曝光](https://developer.apple.com/documentation/metalfx/mtlfxtemporalscalerdescriptor/isautoexposureenabled)互斥，`PreExposure` 始终描述线性颜色已有的倍率。

### 4.3 光线重建

DLSS Ray Reconstruction 与 MetalFX Denoised Scaler 在一次原生调用中完成降噪与超分，读取 `Color` 与对应的材质、深度、运动向量等输入。`TransparencyOverlay` 分别映射到透明层输入；曝光语义与 §2.3 一致。

FSR 使用独立的降噪与超分上下文，在同一个调用方命令缓冲中完成：

1. 使用 `LinearDepthDelta` 和已配置的直接、间接漫反射与镜面反射信号执行降噪。
2. 将 `Color` 中这些信号的贡献替换为降噪结果乘以对应反照度后的贡献，其余场景颜色保持不变。
3. 对合成后的线性颜色执行超分，写入 `Output`。

`Color` 在三个后端都表示完整场景颜色；分离的辐射度信号由调用方提供，其含义遵循 [FSR Ray Regeneration 的信号输入约定](https://github.com/GPUOpen-LibrariesAndSDKs/FidelityFX-SDK/blob/main/Kits/FidelityFX/docs/techniques/denoising.md#12-signals)。FSR 后端将硬件深度转换为带符号线性深度，将 `MotionVectors` 与 `LinearDepthDelta` 合并为原生运动数据，将法线与粗糙度打包为原生表示。反照度使用线性输入模式，材质分类采用默认值；间接漫反射与间接镜面信号的命中距离分别保存在各自辐射度纹理的 A 通道中。

调用方使用一次 `RayReconstruction.Dispatch`，输出始终是完成降噪与超分的颜色纹理。

### 4.4 帧生成

本扩展每次生成一张插值帧。后端按需读取当前帧的相机参数和帧间隔，保存必要的历史；`Reset` 同时重置相机与图像历史。

| 公共 | DLSS | MetalFX | FSR |
|---|---|---|---|
| `Width/Height` | `Width/Height` | `OutputWidth/Height` | `DisplaySize` |
| `AuxiliaryWidth/Height` | `RenderWidth/Height` 及深度、运动向量尺寸 | `InputWidth/Height` | `MaxRenderSize` 与当前 `RenderSize` |
| `Color` | `Backbuffer` | 有 `Hudless` 时为已合成的 `UiTexture`，否则为 `ColorTexture` | `PresentColor` |
| `Hudless` | `Hudless` | `ColorTexture`，启用 `IsUITextureComposited` | `HUDLessColor` |
| `Output` | `OutputInterpFrame` | `OutputTexture` | 唯一的生成帧输出纹理 |
| `IsHdr` | `ColorBuffersHDR` | 无对应开关 | HDR 创建标志与颜色传输函数 |
| `DeltaTimeInSeconds` | 不直接读取 | `DeltaTime` | `FrameTimeDelta`，秒转换为毫秒 |
| HDR 亮度范围 | 不读取 | 不读取 | `MinMaxLuminance` |

不提供 `Hudless` 时，MetalFX 的 `UiTexture` 为空，`IsUITextureComposited` 为假；颜色与 UI 的映射遵循其[已合成 UI 纹理约定](https://developer.apple.com/documentation/metalfx/mtlfxframeinterpolatorbase/isuitexturecomposited)。FSR 的传输函数由 §2.6 的颜色约定确定，HDR 转换读取调用方提供的亮度范围。

`FrameGeneration` 负责命令录制；`FrameGenerationSwapChain` 负责输入纹理、后台提交与呈现。交换链由扩展独立实现，三个帧生成后端使用同一套公共呈现流程。

## 5. 取舍

1. **不做纯降噪。** `RayReconstruction` 始终输出完成降噪与超分的图像。
2. **帧生成只做单帧。** 每次调用生成一张插值帧。
3. **运动向量固定 UV 空间。** 单位与原生表示的转换由后端完成。
4. **必要的降噪信号由调用方提供。** 公共参数包含语义明确的辐射度与深度变化纹理，后端负责读取所需输入并完成格式适配。
5. **两套独立交换链。** `FrameGenerationSwapChain` 从 Surface 创建，拥有整组输入纹理和后台提交任务；调用方完成渲染后每帧调用一次 `Present`，由扩展负责插值帧和真实帧的呈现。
