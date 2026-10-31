# Zenith.NET.Extensions.Reconstruction 设计

定稿 · 2026-10-31 · 以 MetalFX 27 为基准

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
- 不提供：空间超分、动态分辨率、输入子区域与偏移、多帧生成、UI 输入、畸变校正、HDR 显示输出、原生预设与质量模式、提供方专有选项。
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

```csharp
public abstract class TemporalUpscaler : DisposableObject
{
    public TemporalUpscalerDesc Desc { get; }

    public void Dispatch(CommandBuffer commandBuffer, TemporalUpscalerArgs args);

    protected abstract void DispatchImpl(CommandBuffer commandBuffer, TemporalUpscalerArgs args);
}
```

`DenoisingUpscaler`、`FrameGenerator` 结构相同，使用各自的 Desc / Args。构造器均为 internal，`Dispatch` 转调 `DispatchImpl`。

生命周期：

- 先调用 `InitializeReconstruction`，再读取能力、查询尺寸或创建对象。每个 GraphicsContext 一份状态，重复调用复用首次状态；`dataPath` 由调用方保证可写，仅 DLSS 使用。
- 原生初始化失败或 Metal 上系统低于 27 时，三项能力为 false、查询返回 0×0。能力只表示功能可用，不代表所有配置可用。
- 尺寸查询返回所选模式的固定输入尺寸；功能或模式不可用时返回 0×0，不改用其他模式。超分 Desc 的 `InputWidth` / `InputHeight` 必须取同一 Context、同一输出尺寸的查询结果，时域与降噪各用各的查询。模式只出现在查询中，不进入 Desc。同一模式在不同平台上的结果可能略有不同。
- NGX 不保证线程安全，尺寸查询与创建都会使用共享参数：同一 Context 的初始化、查询、创建、执行与释放由调用方串行化。
- GPU 完成后先释放功能对象，再释放上下文。

## 3. 通用约定

| 项 | 约定 |
| --- | --- |
| 资源 | `Texture`：单采样 2D、单 mip、单 layer，与功能对象、CommandBuffer 属于同一 Context；Desc 格式与实际资源一致，且为两端原生支持的格式 |
| 输入 | 用途含 `Sampled`，Dispatch 前后布局为 `Sampled` |
| 输出 | 用途含 `Storage \| ColorAttachment`，Dispatch 前后布局为 `Storage`；不与任何输入共享存储 |
| 尺寸 | 创建时固定，逐帧使用整幅图像。超分的 Output 为输出尺寸、Exposure 为 1×1，其余输入为输入尺寸，时域超分设置 `OutputResolutionMotionVectors` 时 MotionVectors 为输出尺寸；帧生成见 4.3。固定配置改变时重建对象并 Reset |
| 填写 | Desc / Args 为可变结构体，零值不代表有效参数；`MotionVectorScale`、`PreExposure`、矩阵与 `DeltaTimeInSeconds` 必须显式填写 |
| 深度 | `[0, 1]` 硬件深度，方向由 `DepthReversed` 标志指定 |
| 运动矢量 | `MotionVectorScale × 纹理值` 为 Color 网格中的像素位移，与运动矢量纹理的分辨率无关：从当前位置指向上一帧位置，X 向右、Y 向下；包含相机与物体运动，不含抖动（时域超分设置 `JitteredMotionVectors` 时包含抖动） |
| 抖动 | `JitterOffset` 为 Color 网格中回到参考帧的像素偏移，X 向右、Y 向下，等于采样位置偏移取负（r = −s）；后端不再取负 |
| 矩阵 | System.Numerics 行向量约定，不含抖动 |
| 预曝光 | `PreExposure` 为线性输入已乘的正数，未使用时填 1；输出保持该尺度 |
| 颜色 | 只约定 RGB，alpha 未定义 |
| 历史 | 由原生维护，扩展不持有历史图像。首次执行、重建、镜头切换、世界坐标基准改变、放弃提交或执行失败后，由调用方设置 `Reset = true`；同一实例按真实帧顺序录制和提交，不并发录制，不跨相机复用 |
| 命令 | CommandBuffer 处于录制状态、在渲染通道外、来自非 Transfer 队列；Dispatch 只录制，不提交、不呈现、不等待。跨队列可见性由调用方负责；Dispatch 后需重新设置管线与绑定 |

Color 网格指 Color 纹理的像素网格：超分为输入尺寸，帧生成为输出尺寸。

## 4. 功能

### 4.1 时域超分

```csharp
public struct TemporalUpscalerDesc
{
    public PixelFormat ColorFormat;

    public PixelFormat DepthFormat;

    public PixelFormat MotionVectorsFormat;

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
- 设置 `OutputResolutionMotionVectors` 时，运动矢量须已膨胀（dilated），数值仍按 Color（输入）网格解释：纹理值为输出网格像素时，`MotionVectorScale` 取 I / O。

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

不提供曝光控制（DLSS-RR 不支持曝光与自动曝光）；`PreExposure` 原样传入，输出保持该尺度。Flags 只使用 `DepthReversed`。

### 4.3 帧生成

```csharp
public struct FrameGeneratorDesc
{
    public PixelFormat ColorFormat;

    public PixelFormat DepthFormat;

    public PixelFormat MotionVectorsFormat;

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
- Color 为已色调映射、sRGB 显示编码（SDR，数值范围 [0, 1]）、未合成 UI 的当前帧；Output 与之同格式、同编码。不支持 HDR 显示输出：RHI 没有 HDR 交换链与 10-bit 格式，MetalFX 插帧器也没有对应选项。UI 由应用分别合成到真实帧与插帧上。
- Color、Output 为输出尺寸，Depth、MotionVectors 为输入尺寸，覆盖同一视野。运动矢量与抖动按 Color（输出）网格填写：源渲染的采样偏移为 sᵢ（输入像素）时，`JitterOffset = −sᵢ × O / I`；源帧无抖动时填 0。Flags 只使用 `DepthReversed`。
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
| 能力 | 系统 27 及以上，且各描述符的 `supportsMetal4FX` | `SuperSampling`、`SuperSamplingDenoising`、`FrameGeneration` 的 `Available` |
| 超分通用 | 抖动、运动倍率、预曝光直接写入 | 同左；创建标志固定含 `IsHdr`，`ExposureScale = 1`，`RenderSubrectDimensions` 为输入尺寸 |
| 时域 | `inputContentPropertiesEnabled = false`；`autoExposureEnabled`、`jitteredMotionVectorsEnabled`、`outputResolutionMotionVectorsEnabled` 取对应标志；未设置 `AutoExposure` 时绑定 Exposure | `AutoExposure`、`JitteredMotionVectors` 分别加 `AutoExposure`、`MvJittered` 创建标志；未设置 `OutputResolutionMotionVectors` 时加 `MvLowRes`，设置时运动倍率乘 O / I；未设置 `AutoExposure` 时绑定 Exposure 并固定预设 K |
| 降噪 | 启用 SpecularHitDistance；`autoExposureEnabled = true`，不绑定曝光纹理；矩阵经 `Unsafe.BitCast<Matrix4x4, SimdFloat4x4>` 传入 | 加 `MvLowRes`；`DLUnified`、硬件深度、`Unpacked` 粗糙度，默认曝光路径；矩阵按行主序直接传入 |
| 深度方向 | 每次写入 `depthReversed` | 超分写入 `DepthInverted` 创建标志；帧生成每次写入 `DepthInverted` |
| 帧生成运动与抖动 | 直接写入（Color 网格像素） | 均乘 I / O（输入网格像素），不再乘 2 或翻转 Y |
| 帧生成相机 | `worldToViewMatrix`、`viewToClipMatrix` 经 `Unsafe.BitCast` 写入；另由 ViewToClip 推导 near、far、垂直 FOV（度）与 aspect 一并写入 | 当前姿态、ViewToClip 及其逆、前后帧 clip 变换；FOV 为弧度 |
| 帧生成颜色 | Color、Output 按 ColorFormat 原样传入 | `ColorBuffersHDR = false` |
| 帧生成历史 | `requiresPrevColorTexture = false`，上一帧颜色由原生保存；Reset 写入 `shouldResetHistory` | 原生历史；Reset 写入 `Reset` |
| 帧间隔 | 写入 `deltaTime` | 无对应输入 |

DLSS 帧生成的相机推导：令 V、P 为当前矩阵，V′、P′ 为上一真实帧矩阵，`ClipToPrevClip = (V·P)⁻¹ · (V′·P′)`，`PrevClipToClip` 为其逆，Reset 帧为单位矩阵；相机位置、Right、Up 取 V⁻¹ 的第 4、1、2 行，Forward 取第 3 行（右手投影取负）。

### 5.3 固定值

DLSS 帧生成：

- 创建：`Width` / `Height` 为输出尺寸，`NativeBackbufferFormat` 为 ColorFormat 对应的原生格式，`RenderWidth` / `RenderHeight` 为输入尺寸，`DynamicResolutionScaling = false`。Vulkan helper 不转发后三项，需直接在参数块写入 `DLSSG.InternalWidth`、`DLSSG.InternalHeight` 和 `DLSSG.DynamicResolution = 0`。
- 求值：Depth、MVecs 子矩形为输入尺寸，Backbuffer、Output 子矩形为输出尺寸，基点为 0；`MultiFrameCount = MultiFrameIndex = 1`；`CameraMotionIncluded = true`；`MvecJittered = false`（经参数键写入）；`ClipToLensClip` 为单位矩阵；`CameraPinholeOffset = 0`；`OrthoProjection`、`MotionVectorsDilated`、`MenuDetectionEnabled`、`AutomodeOverrideReset`、`NotRenderingGameFrames` 均为 false；`MinRelativeLinearDepthObjectSeparation = 40`；`MotionVectorsInvalidValue = 0`；不绑定插值状态缓冲。
- 单位以 FG 指南正文（运动纹理分辨率的像素）和 NVIDIA RTX Remix 的接入为准，不采用头文件结构注释中的 normalized / clip 描述。

MetalFX：

- 超分：关闭 reactive mask、denoise strength mask 和 transparency overlay；`requiresSynchronousInitialization = false`；时域超分的各 content / output offset 为 0。
- 帧生成：`requiresPrevColorTexture = false`、`prevColorTexture = null`；`isDistortionTextureEnabled = false`；`uiTexture = null`、`uiTextureFormat = Invalid`、`isUITextureComposited = false`、`scaler = null`；`contentWidth` / `contentHeight` 为输入尺寸，各 offset 为 0。

### 5.4 状态与命令

- 原生命令在 RHI 的 `BeginExternalCommands` / `EndExternalCommands` 区间内录制；Metal 通过 `GetNativeObject(NativeObjectType.MTL4CommandBuffer)` 取得命令缓冲。
- DLSS：NGX 初始化成功后才获取能力参数。Create 在 GraphicsQueue 录制、提交并等待，Evaluate 录入调用方的命令缓冲。D3D12 要求非 Copy 队列与匹配的节点；Vulkan 的 image view 缓存与空描述符集沿用现有 DLSS 扩展。每个功能对象持有一个 NGXParameter（`FreeMemOnReleaseFeature = 1`），Create 与 Evaluate 复用且不 Reset；先释放特性句柄再释放参数块，功能对象先于 Context 释放。
- MetalFX：Context 借用设备，自建并持有 `MTL4Compiler`。三个效果都不持有额外纹理：帧生成以 `requiresPrevColorTexture = false` 创建，上一帧颜色由 MetalFX 内部保存，与 DLSS 一致，两端都不复制。

MetalFX 的同步：Metal 4 不做冒险跟踪，MetalFX 在 `Encode` 内自行结束其编码器，但不为后续编码器建立依赖；RHI 在 Metal 上结束外部命令区间时也不等待之前的工作。因此三个 MetalFX 效果在 `Encode` 之后、`EndExternalCommands` 之前，都在同一命令缓冲编码一个同步计算编码器，以生产者屏障结束，使之后的全部工作等待 MetalFX 完成；区间结束后不再录制任何命令，与 DLSS 一致。这段收尾由 `MetalFXContext` 提供，三个效果共用：

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

Vector2 inputToOutput = new((float)OutputWidth / inputSize.Width, (float)OutputHeight / inputSize.Height);

FrameGeneratorArgs frameArgs = new()
{
    Color = displayColor,
    Depth = depth,
    MotionVectors = motionVectors,
    Output = generatedColor,
    DeltaTimeInSeconds = deltaTimeInSeconds,
    JitterOffset = args.JitterOffset * inputToOutput,
    MotionVectorScale = args.MotionVectorScale * inputToOutput,
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
- `Extensions.cs`：公共入口，按 GraphicsContext 缓存后端，随上下文的 `Disposing` 释放；另以 internal 扩展成员提供 `.Success()` 与从 ViewToClip 推导 near、far、FOV、aspect（两端帧生成共用）。
- `ReconstructionBackend.cs`：internal 抽象类，声明能力、两个尺寸查询与三个 Create，由 `DLSSContext`、`MetalFXContext` 实现；按图形 API 只创建其一。
- `DLSSContext*.cs`：NGX 会话、能力、尺寸查询、创建时的模式反查及参数块与特性的释放，DirectX 12 与 Vulkan 为同一类的 partial；`DLSSFeature.cs` 持有特性句柄与参数块。DLSS 帧生成的前后帧 clip 变换与相机基向量在 `DLSSFrameGenerator` 内推导。
- `MetalFXContext.cs`：设备、编译器、能力、尺寸计算与三个效果共用的同步收尾。
- `*Formats.cs`：格式、标志与原生值映射；六个提供方实现分别继承对应的公共类型。
- 依赖：Zenith.NET、NGX.NET 310.9.16、包含 MetalFX 27 API 的 Metal.NET；不含 shader 或原生桥。

## 8. 待验证

以下映射来自文档，需在 Metal.NET 绑定 MetalFX 27 后实测：

- MetalFX 帧生成在 `requiresPrevColorTexture = false`、`prevColorTexture = null` 下的正常帧与 Reset 帧行为。
- MetalFX 帧生成同时写入矩阵与 near、far、FOV、aspect 时的结果；Apple 未说明后者在提供矩阵后是否仍被使用。
- `OutputResolutionMotionVectors` 的单位：按 Apple 文档，MetalFX 的运动倍率换算到 Color 纹理像素，故直接写入；DLSS 去掉 `MvLowRes` 后按显示分辨率像素解释，故乘 O / I。两端都需实测确认。
- `JitteredMotionVectors` 在两端的扣除结果一致。
- DLSS 创建时的模式反查，以及 MetalFX 帧生成去掉消费者屏障后的同步（与超分相同）。

## 参考

- [NVIDIA DLSS SDK 310.9.16](https://github.com/NVIDIA/DLSS/tree/9981c6350dc58c0ce21f024fd65e31e851c31689)：SR、RR、FG 指南与头文件
- [Apple MetalFX](https://developer.apple.com/documentation/metalfx)；27 新增：[requiresPrevColorTexture](https://developer.apple.com/documentation/metalfx/mtlfxframeinterpolatordescriptor/requiresprevcolortexture)、[worldToViewMatrix](https://developer.apple.com/documentation/metalfx/mtlfxframeinterpolatorbase/worldtoviewmatrix)、[viewToClipMatrix](https://developer.apple.com/documentation/metalfx/mtlfxframeinterpolatorbase/viewtoclipmatrix)、[isJitteredMotionVectorsEnabled](https://developer.apple.com/documentation/metalfx/mtlfxtemporalscalerdescriptor/isjitteredmotionvectorsenabled)、[isOutputResolutionMotionVectorsEnabled](https://developer.apple.com/documentation/metalfx/mtlfxtemporalscalerdescriptor/isoutputresolutionmotionvectorsenabled)
- [Metal.NET](https://github.com/qian-o/Metal.NET)：当前 2.6.0 尚未绑定上述 27 API
- [RTX Remix 的 DLSS 帧生成接入](https://github.com/NVIDIAGameWorks/dxvk-remix/blob/0ba9973fd856e29fe9cf8dd2f2bc754bcbb2037a/src/dxvk/rtx_render/rtx_ngx_wrapper.cpp#L1028-L1077)
