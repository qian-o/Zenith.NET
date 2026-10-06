# Zenith.NET.Extensions.DLSS 设计文档

| 项目 | 内容 |
| --- | --- |
| 状态 | 草案第 6 版 |
| 基线 | `feature/dlss-integration` @ `1a8f294` |
| 依赖 | `Zenith.NET`、`NGX.NET` 310.9.1 |
| 参照 | Upscaling 扩展（调用形式）、ImGui 扩展（纹理绑定）、Skia 扩展（每上下文原生状态及其释放）、Apple 为 MetalFX 帧插值提供的 [`PresentThread`](https://github.com/apple/game-porting-toolkit/blob/main/game-porting-skills/skills/using-metalfx-frame-interpolation/SKILL.md)、[NGX.NET Showcase](https://github.com/qian-o/NGX.NET/tree/master/Showcase) |

## 1. 范围与原则

- 三个公开对象：超分辨率（含 DLAA）`DLSSSuperResolution`、光线重建 `DLSSRayReconstruction`、帧生成 `DLSSFrameGeneration`。生成帧如何呈现由应用决定（2.7）。支持 DirectX 12 与 Vulkan，NGX 没有 Metal 实现。
- 调用形式与 Upscaling 扩展相同：`context.CreateXxx(desc)` 创建，`xxx.Dispatch(commandBuffer, args)` 录制。纹理经 `texture.DLSSBinding` 传入，用法同 `texture.ImGuiBinding`。
- 类型名为 `DLSS` 加 NVIDIA 功能名，之后新增的功能按同一规则命名。
- 参数沿用 NGX 原义。扩展只做类型转换：纹理转为原生资源，枚举与格式转为 NGX 取值，按 NGX 的需要对矩阵求逆。不做数值换算，也不做兼容处理。
- 不验证参数，不抛出异常，NGX 调用失败只输出调试信息。NGX 的初始化选项不向应用公开。
- RHI 只保留本分支已有的改动（第 5 节），不再为扩展修改 RHI。

## 2. 公共 API

### 2.1 入口

| 成员 | 说明 |
| --- | --- |
| `context.DLSSCapabilities` | 能力（2.3） |
| `context.GetDLSSSuperResolutionOptimalSettings(outputWidth, outputHeight, mode)` | 超分辨率的推荐输入尺寸 |
| `context.GetDLSSRayReconstructionOptimalSettings(outputWidth, outputHeight, mode)` | 光线重建的推荐输入尺寸 |
| `context.CreateDLSSSuperResolution(desc)`、`CreateDLSSRayReconstruction(desc)`、`CreateDLSSFrameGeneration(desc)` | 创建功能对象 |
| `texture.DLSSBinding` | 纹理绑定（2.2） |

每个上下文的 NGX 状态由内部的 `DLSSContext` 持有，释放方式与 Skia 的 `SKRenderer` 相同。以下为 `Extensions` 的节选：

```csharp
public static class Extensions
{
    private static readonly Lock @lock = new();
    private static readonly Dictionary<GraphicsContext, DLSSContext> contexts = [];

    extension(GraphicsContext context)
    {
        public DLSSCapabilities DLSSCapabilities
        {
            get
            {
                DLSSContext dlssContext = AcquireContext(context);

                DLSSCapabilities capabilities = dlssContext.Capabilities;

                ReleaseContext(dlssContext);

                return capabilities;
            }
        }

        public DLSSSuperResolution CreateDLSSSuperResolution(DLSSSuperResolutionDesc desc)
        {
            return new(AcquireContext(context), desc);
        }
    }

    internal static void ReleaseContext(DLSSContext dlssContext)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (dlssContext.RemoveReference() && contexts.Remove(dlssContext.Context))
        {
            dlssContext.Dispose();
        }
    }

    private static DLSSContext AcquireContext(GraphicsContext context)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (!contexts.TryGetValue(context, out DLSSContext? dlssContext))
        {
            contexts[context] = dlssContext = new(context);
        }

        dlssContext.AddReference();

        return dlssContext;
    }
}
```

- 功能对象创建时取得一个引用，`Destroy` 中调用 `ReleaseContext`；最后一个引用释放时关闭 NGX。查询只在调用期间持有引用。
- NGX 初始化在本机约 1 秒（第 5 节）。没有存活的功能对象时，每次查询都会初始化并关闭一次 NGX。重建功能对象时先创建新对象再释放旧对象，NGX 就不会重新初始化。
- 后端不是 DirectX 12 或 Vulkan、平台不在 NGX 支持范围内，或 NGX 初始化失败时：能力全部为 `false`，查询返回 0，`Dispatch` 不录制命令。
- `NGXResult` 的 `Success()` 与后端的同名成员一致：失败时输出 `Debug.WriteLine`，继续执行。

### 2.2 DLSSBinding

```csharp
namespace Zenith.NET.Extensions.DLSS;

public readonly struct DLSSBinding
{
    internal readonly Texture? Texture;

    internal DLSSBinding(Texture texture)
    {
        Texture = texture;
    }
}
```

- Args 的纹理字段都是 `DLSSBinding`。`default` 表示未提供，可选输入保持默认即可。
- 绑定在 `Dispatch` 时经 `DLSSContext` 解析（3.2）。`Texture` 不公开所属上下文，所以属性只记录纹理。
- 只绑定第 0 级、第 0 层。深度模板格式只取深度方面，与 RHI 采样深度纹理的方式相同。
- 不提供 `TextureView` 的绑定：NGX 的 DirectX 12 接口只接收资源，视图的格式与范围传不进去。

### 2.3 能力、档位与最佳设置

```csharp
namespace Zenith.NET.Extensions.DLSS;

public readonly struct DLSSCapabilities(bool superResolutionSupported, bool rayReconstructionSupported, bool frameGenerationSupported)
{
    public readonly bool SuperResolutionSupported = superResolutionSupported;

    public readonly bool RayReconstructionSupported = rayReconstructionSupported;

    public readonly bool FrameGenerationSupported = frameGenerationSupported;
}
```

```csharp
namespace Zenith.NET.Extensions.DLSS;

public readonly struct DLSSOptimalSettings(uint inputWidth, uint inputHeight, uint minInputWidth, uint minInputHeight, uint maxInputWidth, uint maxInputHeight)
{
    public readonly uint InputWidth = inputWidth;

    public readonly uint InputHeight = inputHeight;

    public readonly uint MinInputWidth = minInputWidth;

    public readonly uint MinInputHeight = minInputHeight;

    public readonly uint MaxInputWidth = maxInputWidth;

    public readonly uint MaxInputHeight = maxInputHeight;
}
```

| 类型或成员 | 取值 | NGX |
| --- | --- | --- |
| `SuperResolutionSupported`、`RayReconstructionSupported`、`FrameGenerationSupported` | | `SuperSampling.Available`、`SuperSamplingDenoising.Available`、`FrameGeneration.Available` |
| `DLSSOptimalSettings` | 0 表示该档位或尺寸不受支持 | `DLSS.GetOptimalSettings`、`DLSSD.GetOptimalSettings` |
| `DLSSMode` | `DLAA`、`UltraQuality`、`Quality`、`Balanced`、`Performance`、`UltraPerformance` | `NGXPerfQualityValue` 的 `DLAA`、`UltraQuality`、`MaxQuality`、`Balanced`、`MaxPerf`、`UltraPerformance` |

- `DLSSMode` 由超分辨率与光线重建共用。
- 渲染预设决定 DLSS 使用哪个模型，不公开，固定为 `Default`：NGX 按档位选用 NVIDIA 推荐的模型，并随驱动更新。

### 2.4 超分辨率

`DLSSSuperResolution` 的公开成员与 `TemporalUpscaler` 相同：`Desc { get; }` 与 `Dispatch(CommandBuffer commandBuffer, DLSSSuperResolutionArgs args)`。调试事件名为 `"DLSS Super Resolution"`。

```csharp
namespace Zenith.NET.Extensions.DLSS;

public struct DLSSSuperResolutionDesc
{
    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public DLSSMode Mode;

    public bool IsHDR;

    public bool IsAutoExposureEnabled;

    public bool IsDepthReversed;

    public bool IsMotionVectorJittered;

    public bool IsAlphaUpscalingEnabled;
}
```

```csharp
namespace Zenith.NET.Extensions.DLSS;

public struct DLSSSuperResolutionArgs
{
    public DLSSBinding Input;

    public DLSSBinding Depth;

    public DLSSBinding MotionVectors;

    public DLSSBinding Exposure;

    public DLSSBinding ReactiveMask;

    public DLSSBinding Output;

    public uint InputContentWidth;

    public uint InputContentHeight;

    public float JitterOffsetX;

    public float JitterOffsetY;

    public float MotionVectorScaleX;

    public float MotionVectorScaleY;

    public float PreExposure;

    public float ExposureScale;

    public bool Reset;
}
```

| 字段 | NGX | 说明 |
| --- | --- | --- |
| `InputWidth`、`InputHeight` | `InWidth`、`InHeight` | 渲染分辨率；使用动态分辨率时为上限 |
| `OutputWidth`、`OutputHeight` | `InTargetWidth`、`InTargetHeight` | |
| `Mode` | `InPerfQualityValue` | |
| `IsHDR`、`IsAutoExposureEnabled`、`IsDepthReversed`、`IsMotionVectorJittered`、`IsAlphaUpscalingEnabled` | `IsHDR`、`AutoExposure`、`DepthInverted`、`MVJittered`、`AlphaUpscaling` 标志 | Alpha 取自 `Input` 的 A 通道 |
| `Input`、`Depth`、`MotionVectors`、`Output` | `pInColor`、`pInDepth`、`pInMotionVectors`、`pInOutput` | 必需 |
| `Exposure` | `pInExposureTexture` | 1×1 曝光纹理 |
| `ReactiveMask` | `pInBiasCurrentColorMask` | 0 到 1，偏向当前帧的程度 |
| `InputContentWidth`、`InputContentHeight` | `InRenderSubrectDimensions` | 动态分辨率下本帧的有效区域 |
| `JitterOffsetX`、`JitterOffsetY` | `InJitterOffsetX`、`InJitterOffsetY` | 输入像素 |
| `MotionVectorScaleX`、`MotionVectorScaleY` | `InMVScaleX`、`InMVScaleY` | 把运动矢量换算到输入像素；0 按 1 |
| `PreExposure`、`ExposureScale` | `InPreExposure`、`InExposureScale` | 0 按 1 |
| `Reset` | `InReset` | 丢弃历史 |

扩展固定设置 `MVLowRes`：运动矢量与输入同分辨率。

### 2.5 光线重建

光线重建一次完成降噪与超分辨率，启用后取代超分辨率与应用自己的降噪。它只接受 HDR 输入，不支持动态分辨率。公开成员的形态与超分辨率相同，调试事件名为 `"DLSS Ray Reconstruction"`。

```csharp
namespace Zenith.NET.Extensions.DLSS;

public struct DLSSRayReconstructionDesc
{
    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public DLSSMode Mode;

    public bool IsDepthReversed;

    public bool IsDepthLinear;

    public bool IsRoughnessPacked;

    public bool IsMotionVectorJittered;

    public bool IsAlphaUpscalingEnabled;
}
```

```csharp
using System.Numerics;

namespace Zenith.NET.Extensions.DLSS;

public struct DLSSRayReconstructionArgs
{
    public DLSSBinding Input;

    public DLSSBinding DiffuseAlbedo;

    public DLSSBinding SpecularAlbedo;

    public DLSSBinding Normals;

    public DLSSBinding Roughness;

    public DLSSBinding Depth;

    public DLSSBinding MotionVectors;

    public DLSSBinding SpecularMotionVectors;

    public DLSSBinding SpecularHitDistance;

    public DLSSBinding TransparencyLayer;

    public DLSSBinding TransparencyLayerOpacity;

    public DLSSBinding ColorBeforeTransparency;

    public DLSSBinding SubsurfaceScatteringGuide;

    public DLSSBinding DepthOfFieldGuide;

    public DLSSBinding Exposure;

    public DLSSBinding ReactiveMask;

    public DLSSBinding Output;

    public float JitterOffsetX;

    public float JitterOffsetY;

    public float MotionVectorScaleX;

    public float MotionVectorScaleY;

    public Matrix4x4 WorldToView;

    public Matrix4x4 ViewToClip;

    public float PreExposure;

    public float ExposureScale;

    public bool Reset;
}
```

| 字段 | NGX | 说明 |
| --- | --- | --- |
| 尺寸、`Mode` | 同超分辨率 | |
| `IsDepthReversed`、`IsMotionVectorJittered`、`IsAlphaUpscalingEnabled` | `DepthInverted`、`MVJittered`、`AlphaUpscaling` 标志 | |
| `IsDepthLinear` | `InUseHWDepth`：`Linear` 或 `HW` | |
| `IsRoughnessPacked` | `InRoughnessMode`：`Packed` 或 `Unpacked` | 打包时粗糙度在 `Normals` 的 A 通道 |
| `Input`、`DiffuseAlbedo`、`SpecularAlbedo`、`Normals`、`Depth`、`MotionVectors`、`Output` | `pInColor`、`pInDiffuseAlbedo`、`pInSpecularAlbedo`、`pInNormals`、`pInDepth`、`pInMotionVectors`、`pInOutput` | 必需 |
| `Roughness` | `pInRoughness` | 未打包时必需 |
| `SpecularMotionVectors`、`SpecularHitDistance` | `pInMotionVectorsReflections`、`pInSpecularHitDistance` | 改善运动中的反射，提供其一即可 |
| `TransparencyLayer`、`TransparencyLayerOpacity`、`ColorBeforeTransparency` | `pInTransparencyLayer`、`pInTransparencyLayerOpacity`、`pInColorBeforeTransparency` | |
| `SubsurfaceScatteringGuide`、`DepthOfFieldGuide` | `pInScreenSpaceSubsurfaceScatteringGuide`、`pInDepthOfFieldGuide` | |
| `Exposure`、`ReactiveMask` | `pInExposureTexture`、`pInBiasCurrentColorMask` | |
| `WorldToView`、`ViewToClip` | `pInWorldToViewMatrix`、`pInViewToClipMatrix` | 必需；不含抖动 |
| 其余数值与 `Reset` | 同超分辨率 | |

扩展固定设置 `IsHDR`、`MVLowRes` 标志与 `InDenoiseMode = DLUnified`，`InRenderSubrectDimensions` 取 Desc 的输入尺寸。

### 2.6 帧生成

帧生成在相邻两个真实帧之间插入一帧。它处理即将呈现的最终画面（色调映射之后、含 UI），每个真实帧调用一次。生成帧写入 `Output`，如何呈现由应用决定（2.7）。调试事件名为 `"DLSS Frame Generation"`。

```csharp
namespace Zenith.NET.Extensions.DLSS;

public struct DLSSFrameGenerationDesc
{
    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public PixelFormat Format;

    public bool IsHDR;

    public bool IsDepthReversed;

    public bool IsMotionVectorJittered;

    public bool IsDynamicResolutionEnabled;

    public bool IsUIRecompositionEnabled;
}
```

```csharp
using System.Numerics;

namespace Zenith.NET.Extensions.DLSS;

public struct DLSSFrameGenerationArgs
{
    public DLSSBinding Color;

    public DLSSBinding HudlessColor;

    public DLSSBinding UI;

    public DLSSBinding UIAlpha;

    public DLSSBinding DistortionField;

    public DLSSBinding Depth;

    public DLSSBinding MotionVectors;

    public DLSSBinding Output;

    public uint InputContentWidth;

    public uint InputContentHeight;

    public float JitterOffsetX;

    public float JitterOffsetY;

    public float MotionVectorScaleX;

    public float MotionVectorScaleY;

    public Matrix4x4 ViewToClip;

    public Matrix4x4 ClipToPrevClip;

    public Vector3 CameraPosition;

    public Vector3 CameraUp;

    public Vector3 CameraRight;

    public Vector3 CameraForward;

    public float CameraPinholeOffsetX;

    public float CameraPinholeOffsetY;

    public float CameraNear;

    public float CameraFar;

    public float CameraFovAngleVer;

    public float CameraAspectRatio;

    public bool Orthographic;

    public bool Reset;
}
```

| 字段 | NGX | 说明 |
| --- | --- | --- |
| `InputWidth`、`InputHeight` | `RenderWidth`、`RenderHeight` | 深度与运动矢量的分辨率；使用动态分辨率时为上限 |
| `OutputWidth`、`OutputHeight` | `Width`、`Height` | 颜色与生成帧的分辨率 |
| `Format` | `NativeBackbufferFormat` | `Color` 与 `Output` 的格式，转为 DXGI_FORMAT 或 VkFormat 的数值 |
| `IsHDR`、`IsDepthReversed` | `ColorBuffersHDR`、`DepthInverted` | |
| `IsMotionVectorJittered` | `DLSSG.MvecJittered` | |
| `IsDynamicResolutionEnabled` | `DynamicResolutionScaling` | |
| `IsUIRecompositionEnabled` | `DLSSG.UserInterfaceRecompositionEnabled` | 需要 `HudlessColor`，以及 `UI` 或 `UIAlpha` |
| `Color`、`Depth`、`MotionVectors`、`Output` | `pBackbuffer`、`pDepth`、`pMVecs`、`pOutputInterpFrame` | 必需；`Output` 与 `Color` 同格式 |
| `HudlessColor` | `pHudless` | 绘制 UI 之前的同一帧，强烈建议提供 |
| `UI`、`UIAlpha` | `pUI`、`pUIAlpha` | 预乘 Alpha 的 UI，或单通道的 UI 不透明度，二选一 |
| `DistortionField` | `pBidirectionalDistortionField` | 镜头畸变等后处理的双向畸变场 |
| `InputContentWidth`、`InputContentHeight` | `MvecsSubrectSize`、`DepthSubrectSize` | 动态分辨率下本帧的有效区域 |
| `JitterOffsetX`、`JitterOffsetY` | `JitterOffset` | 裁剪空间 |
| `MotionVectorScaleX`、`MotionVectorScaleY` | `MvecScale` | 把运动矢量归一化到 [-1, 1] |
| `ViewToClip` | `CameraViewToClip`；其逆矩阵为 `ClipToCameraView` | 不含抖动 |
| `ClipToPrevClip` | `ClipToPrevClip`；其逆矩阵为 `PrevClipToClip` | |
| `CameraPosition`、`CameraUp`、`CameraRight`、`CameraForward` | `CameraPos`、`CameraUp`、`CameraRight`、`CameraFwd` | 世界空间 |
| `CameraPinholeOffsetX`、`CameraPinholeOffsetY` | `CameraPinholeOffset` | |
| `CameraNear`、`CameraFar`、`CameraFovAngleVer`、`CameraAspectRatio` | `CameraNear`、`CameraFar`、`CameraFOV`、`CameraAspectRatio` | 视场角为弧度 |
| `Orthographic`、`Reset` | `OrthoProjection`、`Reset` | |

- 第一次调用与 `Reset` 为真时没有上一帧，NGX 输出的是真实帧的拷贝。
- 扩展固定 `MultiFrameCount = 1`、`MultiFrameIndex = 1`，即 2 倍帧率。

### 2.7 呈现生成帧

扩展不负责呈现，做法与 MetalFX 相同：MetalFX 只提供插帧器，Apple 的 `PresentThread` 是放在应用侧的示例代码。生成帧何时呈现、是否丢弃、如何与真实帧交替，都由应用决定。

参考做法，结构与 `PresentThread` 相同：

- 交换链以 `UsePresentQueue = true` 创建。生成帧与真实帧都在 `swapChain.Queue` 上拷贝到 `Drawable` 并呈现，不排在 `GraphicsQueue` 的渲染工作之后。拷贝与呈现的写法见文档「平台集成」中的「使用呈现队列」。
- 渲染线程：把本帧最终画面（含 UI）渲染到后缓冲，作为帧生成的 `Color`；`Dispatch` 输出到生成帧纹理，提交后把完成值交给呈现线程。
- 呈现线程：帧生成完成后拷贝并呈现生成帧，拷贝以帧生成的完成值为等待条件；等到约半个帧间隔后，拷贝并呈现真实帧。帧间隔取最近几次真实帧呈现间隔的平均值，生成帧与真实帧各显示约半个间隔。第一帧与 `Reset` 为真时只呈现真实帧。
- 后缓冲与生成帧纹理各两张交替使用。渲染线程开始使用某一张之前，等呈现线程用完它，即最多一帧在途。
- 交换链尺寸变化时，先停止呈现线程，再调用 `SwapChain.Resize`，然后重建帧生成对象与上述纹理。

### 2.8 示例

超分辨率：

```csharp
DLSSOptimalSettings settings = context.GetDLSSSuperResolutionOptimalSettings(displayWidth, displayHeight, DLSSMode.Quality);

DLSSSuperResolution superResolution = context.CreateDLSSSuperResolution(new()
{
    InputWidth = settings.InputWidth,
    InputHeight = settings.InputHeight,
    OutputWidth = displayWidth,
    OutputHeight = displayHeight,
    Mode = DLSSMode.Quality,
    IsHDR = true,
    IsAutoExposureEnabled = true
});

commandBuffer.Transition(output, default, TextureLayout.Sampled, TextureLayout.Storage);

superResolution.Dispatch(commandBuffer, new()
{
    Input = color.DLSSBinding,
    Depth = depth.DLSSBinding,
    MotionVectors = motionVectors.DLSSBinding,
    Output = output.DLSSBinding,
    JitterOffsetX = jitter.X,
    JitterOffsetY = jitter.Y,
    Reset = reset
});

commandBuffer.Transition(output, default, TextureLayout.Storage, TextureLayout.Sampled);
```

帧生成。应用把场景与 UI 渲染到后缓冲 `backBuffer` 并转换到 `Sampled`，另外保留不含 UI 的 `hudless` 与只含 UI 的 `ui`；`generated` 是应用的生成帧纹理：

```csharp
SwapChain swapChain = context.CreateSwapChain(new()
{
    Surface = surface,
    Format = PixelFormat.B8G8R8A8UNorm,
    UsePresentQueue = true
});

DLSSFrameGeneration frameGeneration = context.CreateDLSSFrameGeneration(new()
{
    InputWidth = renderWidth,
    InputHeight = renderHeight,
    OutputWidth = swapChain.Desc.Surface.Width,
    OutputHeight = swapChain.Desc.Surface.Height,
    Format = swapChain.Desc.Format,
    IsUIRecompositionEnabled = true
});

commandBuffer.Transition(generated, default, TextureLayout.CopySrc, TextureLayout.Storage);

frameGeneration.Dispatch(commandBuffer, new()
{
    Color = backBuffer.DLSSBinding,
    HudlessColor = hudless.DLSSBinding,
    UI = ui.DLSSBinding,
    Depth = depth.DLSSBinding,
    MotionVectors = motionVectors.DLSSBinding,
    Output = generated.DLSSBinding,
    JitterOffsetX = clipJitter.X,
    JitterOffsetY = clipJitter.Y,
    MotionVectorScaleX = 1.0f / renderWidth,
    MotionVectorScaleY = 1.0f / renderHeight,
    ViewToClip = projection,
    ClipToPrevClip = clipToPrevClip,
    CameraPosition = camera.Position,
    CameraUp = camera.Up,
    CameraRight = camera.Right,
    CameraForward = camera.Forward,
    CameraNear = camera.NearPlane,
    CameraFar = camera.FarPlane,
    CameraFovAngleVer = float.DegreesToRadians(camera.Fov),
    CameraAspectRatio = camera.AspectRatio,
    Reset = reset
});

commandBuffer.Transition(generated, default, TextureLayout.Storage, TextureLayout.CopySrc);
commandBuffer.Transition(backBuffer, default, TextureLayout.Sampled, TextureLayout.CopySrc);

TimelineValue generatedValue = commandBuffer.Submit();
```

之后把 `generated`、`backBuffer` 与 `generatedValue` 交给呈现线程，按 2.7 呈现。

## 3. 内部设计

### 3.1 文件

| 文件 | 内容 |
| --- | --- |
| `Extensions.cs` | 入口（2.1） |
| `DLSSBinding.cs`、`DLSSCapabilities.cs`、`DLSSOptimalSettings.cs`、`DLSSMode.cs` | 2.2、2.3 |
| `DLSSSuperResolution*.cs`、`DLSSRayReconstruction*.cs`、`DLSSFrameGeneration*.cs` | 各功能的类、Desc 与 Args，每个公共类型一个文件 |
| `DLSSContext.cs` | 内部：NGX 初始化与关闭、能力、参数块、绑定解析 |
| `DLSSFormats.cs` | 内部：数值映射（3.4） |

### 3.2 DLSSContext

`internal unsafe class DLSSContext : DisposableObject`，每个 `GraphicsContext` 一个，生命周期见 2.1。

| | DirectX 12 | Vulkan |
| --- | --- | --- |
| 初始化 | `D3D12.InitWithProjectID`，传入 `D3D12Device` | `Vulkan.InitWithProjectID`，传入 `VulkanInstance`、`VulkanPhysicalDevice`、`VulkanDevice`、`VulkanGetInstanceProcAddr`、`VulkanGetDeviceProcAddr` |
| 命令列表 | `D3D12GraphicsCommandList` | `VulkanCommandBuffer` |
| 纹理 | `D3D12Resource` | `NGXResourceVK`：`VulkanImage` 加绑定表中的 VkImageView |
| 关闭 | `D3D12.Shutdown1(device)` | `Vulkan.Shutdown1(device)` |

- 初始化参数固定：项目标识为常量 GUID，引擎类型为 `CUSTOM`，引擎版本取 Zenith.NET 程序集版本，数据目录为临时目录。初始化后读取能力参数，得到 `Capabilities`。
- 只在 DirectX 12 与 Vulkan 后端、Windows 或 Linux 的 x64 与 arm64 上加载 NGX，因为 `NGX.RuntimeDirectory` 在其他平台抛出异常。其他情况下各方法直接返回默认值。
- 绑定表与 ImGui 的 `ImGuiRenderer` 相同：`Dictionary<Texture, nint>` 记录 Vulkan 纹理的 VkImageView。纹理首次出现时用 `vkCreateImageView` 创建视图，函数经 `VulkanGetDeviceProcAddr` 加载，与 Skia 的 `SKRenderer` 相同。每次 `Dispatch` 先移除 `IsDisposed` 为真的纹理并销毁其视图；`DLSSContext` 销毁时销毁全部视图。DirectX 12 直接传资源，不需要绑定表。
- 一把静态锁串行化全部 NGX 调用与绑定表访问，因为 NGX.NET 要求串行调用 SDK。`Extensions` 的锁只保护注册表，锁顺序固定为先注册表锁、后 NGX 锁。
- 销毁顺序：销毁视图，`DestroyParameters(capabilities)`，`Shutdown1(device)`。

### 3.3 功能类

三个功能类的结构相同：

- 构造：`AllocateParameters`，设置 `FreeMemOnReleaseFeature = 1`；帧生成另设 `MvecJittered` 与 `UserInterfaceRecompositionEnabled`。每个对象独占一个参数块，因为 NGX 求值前会把全部参数写入传入的参数块。
- `Dispatch`：`BeginDebugEvent` 与 `Barrier(All, All)`；在 NGX 锁内移除已释放纹理的绑定，首次调用时在该命令缓冲中创建特性，再由 Args 构造求值参数并求值；最后 `Barrier(All, All)` 与 `EndDebugEvent`。首次 `Dispatch` 时创建特性，对应 `TemporalUpscaler` 首次 `Dispatch` 初始化内部资源。
- `Destroy`：`ReleaseFeature`（已创建时）、`DestroyParameters`、`Extensions.ReleaseContext`。

| | 超分辨率 | 光线重建 | 帧生成 |
| --- | --- | --- | --- |
| 创建参数 | `NGXDLSSCreateParams` | `NGXDLSSDCreateParams` | `NGXDLSSGCreateParams` |
| 创建（DirectX 12） | `D3D12.CreateDLSSExt` | `D3D12.CreateDLSSDExt` | `D3D12.CreateDLSSG` |
| 创建（Vulkan） | `Vulkan.CreateDLSSExt1` | `Vulkan.CreateDLSSDExt1` | `Vulkan.CreateDLSSG` |
| 求值参数 | `NGXD3D12DLSSEvalParams`、`NGXVKDLSSEvalParams` | `NGXD3D12DLSSDEvalParams`、`NGXVKDLSSDEvalParams` | `NGXD3D12DLSSGEvalParams`、`NGXVKDLSSGEvalParams`，另有 `NGXDLSSGOptEvalParams` |
| 求值 | `EvaluateDLSSExt` | `EvaluateDLSSDExt` | `EvaluateDLSSG` |

### 3.4 格式

`DLSSFormats` 集中全部数值映射，与 Skia 的 `SKFormats` 一样不依赖 Silk.NET：

- `PixelFormat` 到 VkFormat（Vulkan 视图与 `NGXVkFormat`）与 DXGI_FORMAT（帧生成的 `NativeBackbufferFormat`）；深度模板格式的 Vulkan 视图只取深度方面。
- `DLSSMode` 到 `NGXPerfQualityValue`，Desc 的布尔字段到 `NGXDLSSFeatureFlags`。

## 4. 使用约定

- **命令缓冲**：可以使用任意队列，不在渲染通道内调用。调用前输入处于 `Sampled`、输出处于 `Storage`，调用后布局不变。`Dispatch` 在 NGX 求值前后各插入一次 `Barrier(All, All)`。
- **状态**：NGX 会替换管线、描述符与描述符堆。DLSS 只出现在两段完整的命令之间，扩展不保存也不恢复命令缓冲状态；后续命令照常先 `SetPipeline`，后端在 `SetPipelineImpl` 中重新绑定描述符堆。
- **纹理用途**：输入需要 `Sampled`；输出需要 `Storage` 与 `TransferDst`。Vulkan 下 NGX 会用 `vkCmdClearColorImage` 清除输出：本机超分辨率使用预设 K（DLAA、Quality、Balanced 档位的默认预设）时出现，输出缺少 `TransferDst` 时验证层报错。
- **单位**：按 NGX 原义。超分辨率与光线重建的抖动以输入像素为单位，运动矢量经 `MotionVectorScale` 换算到输入像素；帧生成的抖动在裁剪空间，运动矢量经 `MotionVectorScale` 归一化到 [-1, 1]。矩阵按 `System.Numerics` 原样传递，与 NGX 的行主序一致，且不含抖动。
- **CornellBox**：运动矢量为当前帧减上一帧的 NDC 差值。超分辨率与光线重建的 `MotionVectorScale` 取 (−0.5·W, 0.5·H)，帧生成取 (−0.5, 0.5)，W、H 为渲染尺寸。
- **重建**：Desc 不可变，尺寸、档位或格式变化时重建。先创建新对象再释放旧对象，避免 NGX 重新初始化（2.1）。交换链尺寸变化时重建帧生成对象（2.7）。
- **生命周期与线程**：GPU 完成引用某对象的全部工作后再 `Dispose` 它；Args 中的纹理也要存活到相应工作完成。同一对象不可并发 `Dispatch`。

## 5. RHI 依赖与验证

本分支保留的 RHI 改动：

| 改动 | 用途 |
| --- | --- |
| `NativeObjectType.D3D12GraphicsCommandList`、`VulkanCommandBuffer` | 把命令列表传给 NGX |
| 描述符堆在 `SetPipelineImpl` 中绑定 | NGX 替换描述符堆后，下一次 `SetPipeline` 恢复 |
| Vulkan 设备扩展 `VK_NVX_binary_import`、`VK_NVX_image_view_handle` | NGX 的 Vulkan 实现所需 |
| `CommandQueueType.Present`、`GraphicsContext.PresentQueue`、`SwapChainDesc.UsePresentQueue`、`SwapChain.Queue` | 应用在呈现队列上拷贝并呈现生成帧与真实帧，不排在 `GraphicsQueue` 的渲染工作之后（2.7） |

`VK_KHR_push_descriptor` 已从设备扩展列表移除。验证环境：RTX 4070 Ti SUPER，驱动 617.14，Vulkan SDK 1.4.357 的验证层，2026-10-06。

| 项 | 结果 |
| --- | --- |
| 覆盖范围 | 超分辨率、光线重建、帧生成各自创建特性并求值 3 帧。两组输入：只含必需输入；含全部可选输入（D32 深度、曝光、反应遮罩、预设 K、Alpha 放大、未打包粗糙度、线性深度、镜面命中距离与镜面运动矢量、透明层、次表面散射与景深引导、HDR、HUDless 与 UI 重组、畸变场、动态分辨率） |
| 移除前后 | NGX 调用全部成功，输出均值逐位相同，验证层消息相同（只有第 4 节所述的清除输出） |
| NGX 的行为 | 初始化时查询 `vkCmdPushDescriptorSetKHR`，移除扩展后驱动返回 NULL；三项功能都没有调用它 |
| 未覆盖 | NGX 的扩展需求查询仍列出 `VK_KHR_push_descriptor`；其他 GPU 架构与驱动版本未验证 |
| NGX 开销 | `InitWithProjectID` 约 1.0 至 1.1 秒，`Shutdown1` 约 45 ms |

帧生成另在 DirectX 12（RGBA8、BGRA8，D3D12 调试层）与 Vulkan（BGRA8，验证层）上验证：创建与求值成功，输出正确，没有验证消息。

呈现队列在同一环境下验证（D3D12 调试层、Vulkan 验证层）：

| 项 | 结果 |
| --- | --- |
| 队列 | DirectX 12 创建独立的 DIRECT 队列；Vulkan 取图形队列族中另一个队列，`PresentQueue.Type` 为 `Present` |
| 不排在渲染工作之后 | `GraphicsQueue` 上有约 25 ms 的拷贝负载时，在呈现队列上拷贝并呈现约 5 ms（DirectX 12）与 2 ms（Vulkan），且都在负载完成前结束；走 `GraphicsQueue` 时要等负载结束 |
| 线程 | 呈现线程与渲染线程同时提交，没有验证消息 |
| 拷贝所在队列 | 在 `TransferQueue` 上写入交换链图像时，D3D12 调试层报错并移除设备，所以拷贝放在 `swapChain.Queue` |

## 附录：固定的 NGX 参数

| 功能 | 参数 | 值 |
| --- | --- | --- |
| 全部 | `CreationNodeMask`、`VisibilityNodeMask` | 1 |
| 全部 | `FreeMemOnReleaseFeature` | 1 |
| 全部 | 项目标识、引擎类型与版本、数据目录、日志级别 | 见 3.2 |
| 超分辨率、光线重建 | 渲染预设提示 | `Default`，由 NGX 按档位选择 |
| 超分辨率、光线重建 | `MVLowRes` 标志 | 设置 |
| 超分辨率、光线重建 | `InEnableOutputSubrects`、各子矩形基点 | `false`、0 |
| 超分辨率、光线重建 | `InSharpness`、`DoSharpening` | 0、不设置（锐化已从 DLSS 移除） |
| 超分辨率、光线重建 | 研究、调试与保留用途的参数：`GBufferSurface`、`InToneMapperType`、`pInMotionVectors3D`、`pInIsParticleMask`、`pInAnimatedTextureMask`、`pInDepthHighRes`、`pInPositionViewSpace`、`pInRayTracingHitDistance`、`pInTransparencyMask`、指示器坐标轴翻转 | 默认值 |
| 光线重建 | `IsHDR` 标志、`InDenoiseMode` | 设置、`DLUnified` |
| 光线重建 | `pInAlpha`、`pInOutputAlpha` | `null`，Alpha 取自 `Input` |
| 光线重建 | Streamline 指南未说明语义的引导：`pInReflectedAlbedo`，`ColorBeforeTransparency` 以外的各类 ColorBefore/After，射线方向，漫反射命中距离，`pInTransparencyLayerMvecs`、`pInDisocclusionMask`、`pInResponsivityMask` | `null` |
| 帧生成 | `MultiFrameCount`、`MultiFrameIndex` | 1、1 |
| 帧生成 | `ClipToLensClip` | 单位矩阵 |
| 帧生成 | `CameraMotionIncluded` | `true` |
| 帧生成 | `MotionVectorsDilated`、`MotionVectorsInvalidValue`、`BidirectionalDistFieldPrecisionInfo` | 默认值 |
| 帧生成 | `NotRenderingGameFrames`、`MenuDetectionEnabled`、`AutomodeOverrideReset`、`pOutputDisableInterpolation` | `false`、`null` |
| 帧生成 | `pOutputRealFrame`、`InvertXAxis`、`InvertYAxis`、`UserDebugText` | `null`、0 |
| 帧生成 | `MinRelativeLinearDepthObjectSeparation`、`LinearizedDepth_Scale`、`LinearizedDepth_NearFarPartition` | NGX 默认值 |
| 帧生成 | 颜色、生成帧、HUDless、UI、畸变场的子矩形 | 整张纹理 |
| 帧生成 | `ResourceAlwaysProvidedFlags`、`ResourceNeverProvidedFlags`、`AsyncCreateEnabled`、`EvalFlags`、`BackbufferFrameID`、`FullscreenMode`、`TargetFrameRate` | 不设置 |
