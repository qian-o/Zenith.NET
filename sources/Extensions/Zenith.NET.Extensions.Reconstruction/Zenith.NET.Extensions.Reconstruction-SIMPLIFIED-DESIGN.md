# Zenith.NET.Extensions.Reconstruction 设计

封板 · 2026-10-31 · 以 MetalFX 27 为基准

以 MetalFX 的对象模型定义时域超分、降噪超分和帧生成的公共 API，由 DLSS 与 MetalFX 两个后端实现，不修改 RHI。扩展只负责原生接入、参数映射和本文列出的必要状态；资源与参数的合法性由调用方保证。本扩展将取代 Upscaling 与 DLSS 两个扩展。

## 1. 范围与原则

| 公共对象 | MetalFX | DLSS |
| --- | --- | --- |
| `TemporalUpscaler` | Temporal Scaler | Super Resolution |
| `DenoisingUpscaler` | Temporal Denoised Scaler | Ray Reconstruction |
| `FrameGenerator` | Frame Interpolator（单插帧） | Frame Generation（单插帧） |

- 后端由图形 API 决定：DirectX 12、Vulkan（Windows、Linux）使用 DLSS，Metal 使用 MetalFX；其他组合不支持。MetalFX 以系统 27 的 API 为基准，不兼容 26。
- 公共能力集 = MetalFX 基准 ≤ DLSS 可承接能力；DLSS 的额外能力不进入公共 API。
- 两端在契约层面一致：输入解释、输出尺寸/格式/颜色表示、历史与命令生命周期相同；像素结果、画质、耗时和原生自主回退不要求相同。
- 不提供：空间超分、动态分辨率、输入子区域与偏移、多帧生成、畸变校正、降噪强度遮罩、HDR 显示输出、原生预设与质量模式、提供方专有选项。
- MetalFX 有而未纳入的能力：空间超分、降噪强度遮罩（DLSS 无对应）；降噪曝光控制（DLSS-RR 不支持曝光）；插帧器独立输出格式（DLSS-G 要求与 Backbuffer 同格式）；插帧器关联缩放器（DLSS 无对应）；畸变校正（Apple 未定义畸变场编码）；动态分辨率与子区域（输入尺寸改为上限后无法唯一确定 DLSS 模式，留待后续以新增标志与字段的方式加入；降噪两端都不支持）。
- 不做：中间输出、格式重解释、重采样、颜色或曝光补偿、参数校验、异常处理。原生返回码经 `.Success()` 写入 Debug 诊断，失败时不提供替代图像。

## 2. 公共 API

```csharp
public static class Extensions
{
    extension(GraphicsContext context)
    {
        public void InitializeReconstruction(string dataPath);

        public ReconstructionCapabilities ReconstructionCapabilities { get; }

        public UpscalerInputSize GetTemporalUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalerMode mode);

        public UpscalerInputSize GetDenoisingUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalerMode mode);

        public TemporalUpscaler CreateTemporalUpscaler(TemporalUpscalerDesc desc);

        public DenoisingUpscaler CreateDenoisingUpscaler(DenoisingUpscalerDesc desc);

        public FrameGenerator CreateFrameGenerator(FrameGeneratorDesc desc);
    }
}
```

```csharp
public class ReconstructionCapabilities
{
    public bool TemporalUpscalerSupported { get; }

    public bool DenoisingUpscalerSupported { get; }

    public bool FrameGeneratorSupported { get; }
}

public enum UpscalerMode
{
    Native,

    Quality,

    Balanced,

    Performance,

    UltraPerformance
}

public readonly struct UpscalerInputSize(uint width, uint height)
{
    public readonly uint Width = width;

    public readonly uint Height = height;
}

[Flags]
public enum ReconstructionFlags
{
    None = 0,

    DepthReversed = 1 << 0,

    AutoExposure = 1 << 1,

    JitteredMotionVectors = 1 << 2,

    OutputResolutionMotionVectors = 1 << 3
}
```

| 标志 | 适用 | 含义 |
| --- | --- | --- |
| `DepthReversed` | 全部 | 深度为反向 Z，近平面为 1 |
| `AutoExposure` | 时域超分 | 由原生计算曝光，Args 不提供 Exposure |
| `JitteredMotionVectors` | 时域超分 | 运动矢量包含抖动，由原生按 `JitterOffset` 扣除 |
| `OutputResolutionMotionVectors` | 时域超分 | MotionVectors 为输出尺寸 |

各效果忽略不适用的标志：MetalFX 27 的降噪器与插帧器没有两项运动矢量选项；DLSS-RR 不支持曝光；帧生成的输入已是显示编码颜色，不需要曝光。

标志只表示行为开关。可选输入（反应遮罩、透明叠加层、UI）由 Desc 中对应的可空格式表示，为 null 时不启用，与 RHI 的 `AttachmentFormats.DepthStencilFormat` 一致。

```csharp
public abstract class TemporalUpscaler : DisposableObject
{
    public ref readonly TemporalUpscalerDesc Desc { get; }

    public void Dispatch(CommandBuffer commandBuffer, TemporalUpscalerArgs args);

    protected abstract void DispatchImpl(CommandBuffer commandBuffer, TemporalUpscalerArgs args);
}
```

`DenoisingUpscaler`、`FrameGenerator` 结构相同，使用各自的 Desc / Args。构造器均为 internal，`Dispatch` 转调 `DispatchImpl`。`Desc` 与 RHI 一致，以 `ref readonly` 返回内部保存的描述。

生命周期：

- 先调用 `InitializeReconstruction`，再读取能力、查询尺寸或创建对象。每个 GraphicsContext 一份状态，重复调用复用首次状态；`dataPath` 由调用方保证可写，仅 DLSS 使用。
- 原生初始化失败或 Metal 上系统低于 27 时，三项能力为 false、查询返回 0×0。能力只表示功能可用，不代表所有配置可用。
- 尺寸查询返回所选模式的固定输入尺寸；功能或模式不可用时返回 0×0，不改用其他模式。超分 Desc 的 `InputWidth` / `InputHeight` 必须取同一 GraphicsContext、同一输出尺寸的查询结果，时域与降噪各用各的查询。模式只出现在查询中，不进入 Desc。同一模式在不同平台上的结果可能略有不同。
- NGX 不保证线程安全，尺寸查询与创建都会使用共享参数：同一 GraphicsContext 的初始化、查询、创建、执行与释放由调用方串行化。
- GPU 完成后先释放效果对象，再释放 GraphicsContext。

## 3. 通用约定

| 项 | 约定 |
| --- | --- |
| 资源 | `Texture`：单采样 2D、单 mip、单 layer，与效果对象、CommandBuffer 属于同一 GraphicsContext；Desc 格式与实际资源一致，且为两端原生支持的格式 |
| 输入 | 用途含 `Sampled`，Dispatch 前后布局为 `Sampled` |
| 输出 | 用途含 `Storage \| ColorAttachment`，Dispatch 前后布局为 `Storage`；不与任何输入共享存储 |
| 尺寸 | 创建时固定，逐帧使用整幅图像。超分的 Output 为输出尺寸、Exposure 为 1×1，其余输入为输入尺寸，时域超分设置 `OutputResolutionMotionVectors` 时 MotionVectors 为输出尺寸；帧生成见 4.3。固定配置改变时重建对象并 Reset |
| 填写 | Desc / Args 为可变结构体，零值不代表有效参数；`MotionVectorScale`、`PreExposure`、矩阵与 `DeltaTimeInSeconds` 必须显式填写。可空格式为 null 时不启用对应输入，Args 中对应纹理为 null；非 null 时必须提供该纹理。Exposure 由 `AutoExposure` 决定（见 4.1） |
| 深度 | `[0, 1]` 硬件深度，方向由 `DepthReversed` 标志指定 |
| 运动矢量 | `MotionVectorScale × 纹理值` 为运动矢量纹理自身网格中的像素位移：从当前位置指向上一帧位置，X 向右、Y 向下；包含相机与物体运动，不含抖动（时域超分设置 `JitteredMotionVectors` 时包含抖动） |
| 抖动 | `JitterOffset` 为输入网格中回到参考帧的像素偏移，X 向右、Y 向下，等于采样位置偏移取负（r = −s）；后端不再取负 |
| 矩阵 | System.Numerics 行向量约定，不含抖动 |
| 预曝光 | `PreExposure` 为线性输入已乘的正数，未使用时填 1；输出保持该尺度 |
| 颜色 | Color 与 Output 只约定 RGB，alpha 未定义；TransparencyOverlay 为预乘 alpha 的 RGBA（见 4.2） |
| 历史 | 由原生维护，扩展不持有历史图像。首次执行、重建、镜头切换、世界坐标基准改变、放弃提交或执行失败后，由调用方设置 `Reset = true`；同一实例按真实帧顺序录制和提交，不并发录制，不跨相机复用 |
| 命令 | CommandBuffer 处于录制状态、在渲染通道外、来自非 Transfer 队列；Dispatch 只录制，不提交、不呈现、不等待。跨队列可见性由调用方负责；Dispatch 后需重新设置管线与绑定 |

输入网格指输入尺寸的像素网格。两项均为两端原生采用的单位，后端原样传入；运动矢量纹理为输入尺寸时（时域超分设置 `OutputResolutionMotionVectors` 除外），超分与帧生成填写相同的值。

## 4. 功能

### 4.1 时域超分

```csharp
public struct TemporalUpscalerDesc
{
    public PixelFormat ColorFormat;

    public PixelFormat DepthFormat;

    public PixelFormat MotionVectorsFormat;

    public PixelFormat? ReactiveMaskFormat;

    public PixelFormat OutputFormat;

    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public ReconstructionFlags Flags;
}

public struct TemporalUpscalerArgs
{
    public Texture Color;

    public Texture Depth;

    public Texture MotionVectors;

    public Texture? Exposure;

    public Texture? ReactiveMask;

    public Texture Output;

    public Vector2 JitterOffset;

    public Vector2 MotionVectorScale;

    public float PreExposure;

    public bool Reset;
}
```

- Color 为线性颜色（可为 HDR），Output 保持同一线性尺度。
- 设置 `AutoExposure` 时 `Exposure` 为 null；未设置时必须提供 1×1 `R16Float` 曝光纹理，单位曝光传值为 1 的纹理。
- 设置 `JitteredMotionVectors` 时，运动矢量包含抖动，由原生按 `JitterOffset` 扣除；未设置时运动矢量不得含抖动。
- 设置 `OutputResolutionMotionVectors` 时，运动矢量须已膨胀（dilated），`MotionVectorScale × 纹理值` 为输出网格像素；`JitterOffset` 仍为输入网格像素。
- `ReactiveMaskFormat` 非 null 时必须提供 ReactiveMask：输入尺寸，只读 R 通道，每像素取 0 或 1（DLSS 只定义二值）；1 表示该像素的历史不可靠（如 alpha 混合、运动信息不准），偏向当前帧。只用于有问题的内容，可能增加遮罩边缘的锯齿。

### 4.2 降噪超分

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

    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public ReconstructionFlags Flags;
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

    public Texture Output;

    public Vector2 JitterOffset;

    public Vector2 MotionVectorScale;

    public Matrix4x4 WorldToView;

    public Matrix4x4 ViewToClip;

    public float PreExposure;

    public bool Reset;
}
```

| 输入 | 含义 |
| --- | --- |
| Color | 含噪声的线性 HDR 颜色 |
| DiffuseAlbedo | 低噪声的线性漫反射率，不含光照、预曝光或显示编码；金属不使用完整 baseColor |
| SpecularAlbedo | 当前视角下的平均镜面反射率，含 Fresnel 与粗糙度影响，不是原始 F0 |
| Normals | 世界空间归一化 shading normal，有符号 XYZ，与 Depth、SpecularHitDistance 对应同一表面 |
| Roughness | 独立 R 通道的线性粗糙度，不与法线打包 |
| SpecularHitDistance | 主表面到镜面二次命中点的世界空间距离，单位与相机矩阵一致；无命中填所用格式的最大有限值（FP16 为 65504），不填 NaN 或 Inf |
| ReactiveMask | 可选，同时域超分 |
| TransparencyOverlay | 可选。粒子等透明效果单独渲染的输入尺寸线性 RGBA，RGB 已预乘 alpha，alpha 为不透明度；这些内容不再写入 Color，只放大并合成到输出，不参与降噪 |

不提供曝光控制（DLSS-RR 不支持曝光与自动曝光）；`PreExposure` 原样传入，输出保持该尺度。Flags 只使用 `DepthReversed`。

### 4.3 帧生成

```csharp
public struct FrameGeneratorDesc
{
    public PixelFormat ColorFormat;

    public PixelFormat DepthFormat;

    public PixelFormat MotionVectorsFormat;

    public PixelFormat? UIFormat;

    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public ReconstructionFlags Flags;
}

public struct FrameGeneratorArgs
{
    public Texture Color;

    public Texture Depth;

    public Texture MotionVectors;

    public Texture? UI;

    public Texture Output;

    public float DeltaTimeInSeconds;

    public Vector2 JitterOffset;

    public Vector2 MotionVectorScale;

    public Matrix4x4 WorldToView;

    public Matrix4x4 ViewToClip;

    public bool Reset;
}
```

以下 I、O 分别为 Desc 的输入、输出尺寸，按分量计算。

- 每次 Dispatch 对应一个真实渲染帧 Fₙ，所有 Args 都属于该帧；历史随 Dispatch 推进，Output 不作为后续输入。上一帧颜色由原生保存，调用方只提供当前帧，Dispatch 之后可继续改写 Color。
- 正常历史下，Output 是 Fₙ₋₁ 与 Fₙ 之间的单张插帧，目标时刻为 tₙ₋₁ + Δt/2；呈现顺序为 Fₙ₋₁ → Output → Fₙ，节奏由应用控制。原生可能自行放弃插值（如 DLSS 输出当前帧），两端不要求逐帧一致。
- Reset 帧只重建历史，Output 不可呈现，应用呈现 Color。
- Color 为已色调映射、sRGB 显示编码（SDR，数值范围 [0, 1]）的当前帧；Output 与之同格式、同编码。不支持 HDR 显示输出：RHI 没有 HDR 交换链与 10-bit 格式，MetalFX 插帧器也没有对应选项。
- `UIFormat` 为 null 时，Color 不含 UI，UI 由应用分别合成到真实帧与插帧上。非 null 时，Color 为不含 UI 的当前帧，`UI` 为同一帧合成 UI 后的最终画面（与 Color 同尺寸、同格式、同编码，`UIFormat` 等于 `ColorFormat`），两者在非 UI 像素上相同；Output 含 UI，应用依次呈现 Output 与 `UI`。
- Color、Output 为输出尺寸，Depth、MotionVectors 为输入尺寸，覆盖同一视野。运动矢量与抖动均为输入网格像素（第 3 节）：源渲染的采样偏移为 sᵢ 时 `JitterOffset = −sᵢ`，源帧无抖动时填 0。Flags 只使用 `DepthReversed`。
- `DeltaTimeInSeconds` 必填，为 Fₙ₋₁ 到 Fₙ 的场景时间间隔（Reset 帧填当前帧步长），不是 GPU 耗时或显示间隔；MetalFX 使用，DLSS 无对应输入。
- `WorldToView` 为可逆刚体变换，相邻帧使用同一世界坐标基准；`ViewToClip` 为不含抖动、居中对称、近远平面有限、深度 [0, 1] 且方向与 `DepthReversed` 一致的透视投影，手性由矩阵决定。

## 5. 原生映射

### 5.1 模式与尺寸

| UpscalerMode | DLSS 模式 | MetalFX 输入/输出比例 |
| --- | --- | --- |
| Native | DLAA | 1 |
| Quality | MaxQuality | 2/3 |
| Balanced | Balanced | 0.58 |
| Performance | MaxPerf | 1/2 |
| UltraPerformance | UltraPerformance | 1/3 |

- DLSS：时域、降噪分别调用 SR、RR 的最佳设置查询，返回该模式的推荐尺寸。
- MetalFX：各轴取 `round(输出尺寸 × 比例)`（半值向上）；超出该效果的原生缩放范围（`supportedInputContentMinScale` / `MaxScale`）时，在同一模式内取最近的合法尺寸；该模式的名义缩放倍数本身超出范围时返回 0×0。
- DLSS 创建时按 Native → UltraPerformance 依次查询最佳设置，取第一个推荐尺寸等于 Desc 输入尺寸的模式作为 `PerfQualityValue`；Desc 尺寸必须来自查询，因此映射是精确的。MetalFX 直接使用 Desc 的尺寸。

### 5.2 参数

| 项 | MetalFX | DLSS |
| --- | --- | --- |
| 能力 | 系统 27 及以上（tvOS 为 27.1），且各描述符的 `supportsMetal4FX` | `SuperSampling`、`SuperSamplingDenoising`、`FrameGeneration` 的 `Available` |
| 运动与抖动 | 三个效果均直接写入抖动与运动倍率 | 同左；帧生成不再乘 2 或翻转 Y |
| 超分通用 | 预曝光直接写入；Reset 写入 `reset`（时域）或 `shouldResetHistory`（降噪） | 同左（含 Reset）；创建标志固定含 `IsHdr`，`ExposureScale = 1`，`RenderSubrectDimensions` 为输入尺寸 |
| 时域 | `inputContentPropertiesEnabled = false`；`autoExposureEnabled`、`jitteredMotionVectorsEnabled`、`outputResolutionMotionVectorsEnabled` 取对应标志；未设置 `AutoExposure` 时绑定 Exposure | `AutoExposure`、`JitteredMotionVectors` 分别加 `AutoExposure`、`MvJittered` 创建标志；未设置 `OutputResolutionMotionVectors` 时加 `MvLowRes`；未设置 `AutoExposure` 时绑定 Exposure 并固定预设 K |
| 降噪 | 启用 SpecularHitDistance；`autoExposureEnabled = true`，不绑定曝光纹理；矩阵经 `Unsafe.BitCast<Matrix4x4, SimdFloat4x4>` 传入 | 加 `MvLowRes`；`DLUnified`、硬件深度、`Unpacked` 粗糙度，默认曝光路径；矩阵按行主序直接传入 |
| 深度方向 | 每次写入 `depthReversed` | 超分写入 `DepthInverted` 创建标志；帧生成每次写入 `DepthInverted` |
| 帧生成相机 | `worldToViewMatrix`、`viewToClipMatrix` 经 `Unsafe.BitCast` 写入；另由 ViewToClip 推导 near、far、垂直 FOV（度）与 aspect 一并写入 | 当前姿态、ViewToClip 及其逆、前后帧 clip 变换；FOV 为弧度 |
| 帧生成颜色 | Color、Output 按 ColorFormat 原样传入 | `ColorBuffersHDR = false` |
| 帧生成历史 | `requiresPrevColorTexture = false`，上一帧颜色由原生保存；Reset 写入 `shouldResetHistory` | 原生历史；Reset 写入 `Reset` |
| 反应遮罩 | `ReactiveMaskFormat` 非 null 时启用 `reactiveMaskTextureEnabled` 并写入格式；绑定 `reactiveMaskTexture` | SR、RR 绑定 `BiasCurrentColorMask` |
| 透明叠加层 | `TransparencyOverlayFormat` 非 null 时启用 `transparencyOverlayTextureEnabled` 并写入格式；绑定 `transparencyOverlayTexture` | RR 绑定四通道 `TransparencyLayer` |
| 帧生成 UI | `UIFormat` 非 null 时写入 `uiTextureFormat`，`isUITextureComposited = true`；`colorTexture` 绑定 Color（不含 UI），`uiTexture` 绑定 UI（含 UI） | Backbuffer 绑定 UI，Hudless 绑定 Color，不绑定 UI 纹理 |
| 帧间隔 | 写入 `deltaTime` | 无对应输入 |

DLSS 帧生成的相机推导：令 V、P 为当前矩阵，V′、P′ 为上一真实帧矩阵，`ClipToPrevClip = (V·P)⁻¹ · (V′·P′)`，`PrevClipToClip` 为其逆，Reset 帧为单位矩阵；相机位置、Right、Up 取 V⁻¹ 的第 4、1、2 行，Forward 取第 3 行（右手投影取负）。

### 5.3 固定值

DLSS 帧生成：

- 创建：`Width` / `Height` 为输出尺寸，`NativeBackbufferFormat` 为 ColorFormat 对应的原生格式，`RenderWidth` / `RenderHeight` 为输入尺寸，`DynamicResolutionScaling = false`。Vulkan helper 不转发后三项，需直接在参数块写入 `DLSSG.InternalWidth`、`DLSSG.InternalHeight` 和 `DLSSG.DynamicResolution = 0`。
- 求值：Depth、MVecs 子矩形为输入尺寸，Backbuffer、Hudless、Output 子矩形为输出尺寸，基点为 0；`MultiFrameCount = MultiFrameIndex = 1`；`CameraMotionIncluded = true`；`MvecJittered = false`（经参数键写入）；`ClipToLensClip` 为单位矩阵；`CameraPinholeOffset = 0`；`OrthoProjection`、`MotionVectorsDilated`、`MenuDetectionEnabled`、`AutomodeOverrideReset`、`NotRenderingGameFrames` 均为 false；`MinRelativeLinearDepthObjectSeparation = 40`；`MotionVectorsInvalidValue = 0`；不绑定插值状态缓冲。
- 单位以 FG 指南正文（运动纹理分辨率的像素）和 NVIDIA RTX Remix 的接入为准，不采用头文件结构注释中的 normalized / clip 描述。

MetalFX：

- 超分：关闭 denoise strength mask；reactive mask、transparency overlay 由 Desc 的可空格式决定，为 null 时关闭；`requiresSynchronousInitialization = false`；时域超分的 `inputContentWidth` / `inputContentHeight` 为输入尺寸，各 content / output offset 为 0。
- 帧生成：`requiresPrevColorTexture = false`、`prevColorTexture = null`；`isDistortionTextureEnabled = false`；`UIFormat` 为 null 时 `uiTexture = null`、`uiTextureFormat = Invalid`、`isUITextureComposited = false`；`scaler = null`；`contentWidth` / `contentHeight` 为输入尺寸，各 offset 为 0。
- 三个效果的 `fence` 均为 null：它要求后续编码器显式等待，RHI 不感知，同步改由 5.4 的屏障完成。

### 5.4 状态与命令

- 原生命令在 RHI 的 `BeginExternalCommands` / `EndExternalCommands` 区间内录制；Metal 通过 `GetNativeObject(NativeObjectType.MTL4CommandBuffer)` 取得命令缓冲。调试事件放在区间内：Metal 的调试组属于当前编码器，而进入区间会结束该编码器，放在区间外会跨编码器失衡；区间内在 Metal 上为空操作。
- DLSS：NGX 初始化成功后才获取能力参数。Create 在 GraphicsQueue 录制、提交并等待，Evaluate 录入调用方的命令缓冲。D3D12 要求非 Copy 队列与匹配的节点；Vulkan 的 image view 缓存与空描述符集从现有 DLSS 扩展移植（该扩展将被删除）。每个效果对象持有一个 NGXParameter（`FreeMemOnReleaseFeature = 1`），Create 与 Evaluate 复用且不 Reset；先释放特性句柄再释放参数块，效果对象先于 `DLSSContext` 释放。
- MetalFX：`MetalFXContext` 借用设备，自建并持有 `MTL4Compiler`。三个效果都不持有额外纹理：帧生成以 `requiresPrevColorTexture = false` 创建，上一帧颜色由 MetalFX 内部保存，与 DLSS 一致，两端都不复制。应用纹理由 RHI 在 GraphicsContext 上维护的 residency set 保证驻留，MetalFX 内部资源由其自行管理。

MetalFX 的同步：Metal 4 不做冒险跟踪。进入外部命令区间时，RHI 结束当前编码器并以生产者屏障收尾，MetalFX 因此等待之前的全部工作；但 MetalFX 在 `Encode` 内自行结束其编码器，不为后续编码器建立依赖，RHI 在 Metal 上结束外部命令区间时也不等待之前的工作。因此三个 MetalFX 效果在 `Encode` 之后、`EndExternalCommands` 之前，都在同一命令缓冲编码一个同步计算编码器，以生产者屏障结束（与 RHI 编码器的收尾方式相同），使之后的全部工作等待 MetalFX 完成；区间结束后不再录制任何命令，与 DLSS 一致。这段收尾由 `MetalFXContext` 提供，三个效果共用：

```csharp
effect.Encode(commandBuffer);

using (NSAutoreleasePool _ = new())
{
    MTL4ComputeCommandEncoder encoder = commandBuffer.MakeComputeCommandEncoder();

    encoder.BarrierAfterStages(MTLStages.All, MTLStages.All, MTL4VisibilityOptions.Device);
    encoder.EndEncoding();
}
```

该编码器是 autorelease 对象，由自动释放池释放，不调用 `Dispose`。

## 6. 用法

```csharp
context.InitializeReconstruction(dataPath);

const uint OutputWidth = 1920;
const uint OutputHeight = 1080;

UpscalerInputSize inputSize = context.GetTemporalUpscalerInputSize(OutputWidth, OutputHeight, UpscalerMode.Quality);

TemporalUpscaler upscaler = context.CreateTemporalUpscaler(new()
{
    ColorFormat = PixelFormat.R16G16B16A16Float,
    DepthFormat = PixelFormat.D32Float,
    MotionVectorsFormat = PixelFormat.R16G16Float,
    OutputFormat = PixelFormat.R16G16B16A16Float,
    InputWidth = inputSize.Width,
    InputHeight = inputSize.Height,
    OutputWidth = OutputWidth,
    OutputHeight = OutputHeight,
    Flags = ReconstructionFlags.DepthReversed | ReconstructionFlags.AutoExposure
});

FrameGenerator frameGenerator = context.CreateFrameGenerator(new()
{
    ColorFormat = PixelFormat.R8G8B8A8UNorm,
    DepthFormat = PixelFormat.D32Float,
    MotionVectorsFormat = PixelFormat.R16G16Float,
    InputWidth = inputSize.Width,
    InputHeight = inputSize.Height,
    OutputWidth = OutputWidth,
    OutputHeight = OutputHeight,
    Flags = ReconstructionFlags.DepthReversed
});
```

每帧执行；`samplingOffset` 为本帧输入网格的采样位置偏移：

```csharp
TemporalUpscalerArgs args = new()
{
    Color = color,
    Depth = depth,
    MotionVectors = motionVectors,
    Output = output,
    JitterOffset = -samplingOffset,
    MotionVectorScale = Vector2.One,
    PreExposure = 1.0f,
    Reset = firstFrame || cameraCut
};

upscaler.Dispatch(commandBuffer, args);

FrameGeneratorArgs frameArgs = new()
{
    Color = displayColor,
    Depth = depth,
    MotionVectors = motionVectors,
    Output = generatedColor,
    DeltaTimeInSeconds = deltaTimeInSeconds,
    JitterOffset = args.JitterOffset,
    MotionVectorScale = args.MotionVectorScale,
    WorldToView = worldToView,
    ViewToClip = viewToClip,
    Reset = firstFrame || cameraCut
};

frameGenerator.Dispatch(commandBuffer, frameArgs);
```

`displayColor` 为 sRGB 显示编码、不含 UI 的当前帧，用途含 `Sampled`；`generatedColor` 用途含 `Storage | ColorAttachment`。正常帧依次呈现 `generatedColor`、`displayColor`；Reset 帧只呈现 `displayColor`。`displayColor` 每帧可复用同一张纹理。

## 7. 项目结构

```text
Zenith.NET.Extensions.Reconstruction/
├── Zenith.NET.Extensions.Reconstruction.csproj
├── Extensions.cs
├── ReconstructionBackend.cs
├── ReconstructionCapabilities.cs
├── ReconstructionFlags.cs
├── UpscalerMode.cs
├── UpscalerInputSize.cs
├── TemporalUpscaler.cs
├── TemporalUpscalerDesc.cs
├── TemporalUpscalerArgs.cs
├── DenoisingUpscaler.cs
├── DenoisingUpscalerDesc.cs
├── DenoisingUpscalerArgs.cs
├── FrameGenerator.cs
├── FrameGeneratorDesc.cs
├── FrameGeneratorArgs.cs
├── DLSS/
│   ├── DLSSContext.cs
│   ├── DLSSContext.DirectX12.cs
│   ├── DLSSContext.Vulkan.cs
│   ├── DLSSFeature.cs
│   ├── DLSSFormats.cs
│   ├── DLSSTemporalUpscaler.cs
│   ├── DLSSDenoisingUpscaler.cs
│   └── DLSSFrameGenerator.cs
└── MetalFX/
    ├── MetalFXContext.cs
    ├── MetalFXFormats.cs
    ├── MetalFXTemporalUpscaler.cs
    ├── MetalFXDenoisingUpscaler.cs
    └── MetalFXFrameGenerator.cs
```

- 公开类型位于根目录，命名空间为 `Zenith.NET.Extensions.Reconstruction`；`DLSS/`、`MetalFX/` 下的内部类型分别使用 `Zenith.NET.Extensions.Reconstruction.DLSS`、`Zenith.NET.Extensions.Reconstruction.MetalFX`，保持仓库已启用的 IDE0130。
- 命名：后端入口为 `ReconstructionBackend`，与其他扩展一致（`SkiaBackend`、`ImGuiBackend`）；后端内部实现为 `DLSSContext`、`MetalFXContext`。效果实现为“后端前缀 + 公共基类名”（如 `DLSSTemporalUpscaler`），格式映射为 `DLSSFormats`、`MetalFXFormats`（同 RHI 的 `MTLFormats`）；能力属性为“功能 + Supported”（同 `RayTracingSupported`）；可选输入用可空类型（同 `AttachmentFormats.DepthStencilFormat`）；单位写入名称（同 `SizeInBytes`）。
- `Extensions.cs`：公共入口，按 GraphicsContext 缓存 `ReconstructionBackend`，随 GraphicsContext 的 `Disposing` 释放；另以 internal 扩展成员提供 `.Success()`、CommandBuffer 与 Texture 到 Metal.NET 对象的 `Metal()` 转换，以及从 ViewToClip 推导 near、far、FOV、aspect（两端帧生成共用）。
- `ReconstructionBackend.cs`：internal 抽象类，声明能力、两个尺寸查询与三个 Create，由 `DLSSContext`、`MetalFXContext` 实现；按图形 API 只创建其一。
- `DLSSContext*.cs`：NGX 会话、能力、尺寸查询、创建时的模式反查及参数块与特性的释放，DirectX 12 与 Vulkan 为同一类的 partial；`DLSSFeature.cs` 持有特性句柄与参数块。DLSS 帧生成的前后帧 clip 变换与相机基向量在 `DLSSFrameGenerator` 内推导。
- `MetalFXContext.cs`：设备、编译器、能力、尺寸计算与三个效果共用的同步收尾。
- `*Formats.cs`：格式、标志与原生值映射；六个提供方实现分别继承对应的公共类型。
- 依赖：Zenith.NET、NGX.NET 310.9.16、Metal.NET 2.7.0；不含 shader 或原生桥。

## 8. 验证

MetalFX 已在 Apple M4、macOS 27.0.1、Metal.NET 2.7.0 上以合成场景与 CornellBox 实测（开启 Metal API 校验，无报错）：

- 抖动 r = −s、运动矢量方向、`JitteredMotionVectors`（运动矢量含 s 当前 − s 上一帧，由原生扣除）、反应遮罩全 0 时等同未启用，均符合契约。
- 原生按运动矢量纹理网格解释运动倍率，与第 3 节一致：输出尺寸运动矢量取 `MotionVectorScale = 1` 时与输入尺寸基线误差相同；帧生成取 1 时插帧落在中点，×0.5、×2 均偏离；Apple 的 MetalFX 插帧接入示例同样以渲染分辨率像素填写运动倍率。
- 帧生成以 `requiresPrevColorTexture = false` 正常插帧，插帧落在两真实帧中点；Reset 后第一帧输出当前帧。插帧器默认 `contentWidth` / `contentHeight` 即输入尺寸。
- UI 合成模式下 `colorTexture` 为不含 UI 的画面、`uiTexture` 为含 UI 的画面；TransparencyOverlay 按预乘 alpha 合成。
- 同一命令缓冲在 Dispatch 之后录制的拷贝读到完整结果，5.4 的生产者屏障生效；效果要求的纹理用途与第 3 节一致。

以下仍待在 DLSS（DirectX 12、Vulkan）上实测：

- 创建时的模式反查、手动曝光固定预设 K。
- 帧生成的运动与抖动单位、相机推导、`DLSSG.MvecJittered` 与 Vulkan 直接写入的参数键。
- `OutputResolutionMotionVectors`、`JitteredMotionVectors` 与 MetalFX 结果一致。
- UI 以 Backbuffer + Hudless 接入后的插帧与 UI 保真；帧生成抖动单位在 MetalFX 上按输入网格推断，尚无可测场景。

## 参考

- [NVIDIA DLSS SDK 310.9.16](https://github.com/NVIDIA/DLSS/tree/9981c6350dc58c0ce21f024fd65e31e851c31689)：SR、RR、FG 指南与头文件
- [Apple MetalFX](https://developer.apple.com/documentation/metalfx)；27 新增：[requiresPrevColorTexture](https://developer.apple.com/documentation/metalfx/mtlfxframeinterpolatordescriptor/requiresprevcolortexture)、[worldToViewMatrix](https://developer.apple.com/documentation/metalfx/mtlfxframeinterpolatorbase/worldtoviewmatrix)、[viewToClipMatrix](https://developer.apple.com/documentation/metalfx/mtlfxframeinterpolatorbase/viewtoclipmatrix)、[isJitteredMotionVectorsEnabled](https://developer.apple.com/documentation/metalfx/mtlfxtemporalscalerdescriptor/isjitteredmotionvectorsenabled)、[isOutputResolutionMotionVectorsEnabled](https://developer.apple.com/documentation/metalfx/mtlfxtemporalscalerdescriptor/isoutputresolutionmotionvectorsenabled)
- [Metal.NET 2.7.0](https://github.com/qian-o/Metal.NET/tree/64248534e4b479c6bb3e99b51aa14e2736bd06ca)：已绑定上述 27 API
- [RTX Remix 的 DLSS 帧生成接入](https://github.com/NVIDIAGameWorks/dxvk-remix/blob/0ba9973fd856e29fe9cf8dd2f2bc754bcbb2037a/src/dxvk/rtx_render/rtx_ngx_wrapper.cpp#L1028-L1077)
