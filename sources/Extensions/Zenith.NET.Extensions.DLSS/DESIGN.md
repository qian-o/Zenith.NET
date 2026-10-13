# Zenith.NET.Extensions.DLSS 设计文档

| 项目 | 内容 |
| --- | --- |
| 状态 | 第 17 版：后端结构调整草案；当前实现仍为上一版 |
| 依赖 | `Zenith.NET`、`NGX.NET` 310.9.1.1 |
| 参照 | Upscaling 扩展（调用形式）、ImGui 扩展（纹理绑定）、Skia 扩展（每上下文原生状态及其释放）、Apple 为 MetalFX 帧插值提供的 [`PresentThread`](https://github.com/apple/game-porting-toolkit/blob/main/game-porting-skills/skills/using-metalfx-frame-interpolation/references/present-thread.md)、[NGX.NET Showcase](https://github.com/qian-o/NGX.NET/tree/master/Showcase) |

## 1. 范围与原则

- 三个公开对象：超分辨率（含 DLAA）`DLSSSuperResolution`、光线重建 `DLSSRayReconstruction`、帧生成 `DLSSFrameGeneration`。生成帧如何呈现由应用决定（2.7）。支持 DirectX 12 与 Vulkan，NGX 没有 Metal 实现。
- 调用形式与 Upscaling 扩展相同：`context.CreateXxx(desc)` 创建，`xxx.Dispatch(commandBuffer, args)` 录制。纹理经 `texture.DLSSBinding` 传入，用法同 `texture.ImGuiBinding`。
- 类型名为 `DLSS` 加 NVIDIA 功能名，之后新增的功能按同一规则命名。
- 参数沿用 NGX 原义。扩展只做类型转换：纹理转为原生资源，枚举与格式转为 NGX 取值，按 NGX 的需要对矩阵求逆；未公开的 NGX 参数取固定值（附录）。不做数值换算，也不做兼容处理。
- 本轮将 DLSS 按独立后端组织：Desc 的布尔配置改为 `DLSSFeatureFlags`、`DLSSDepthType`、`DLSSRoughnessMode`；增加 `DLSSFeature` 基类；DX12 与 Vulkan 分别实现后端；原生特性在具体功能对象构造期间创建。现有初始化签名、原生数据绑定、可空资源、Args 与呈现职责保持不变。
- 调用方负责正确的平台、图形 API、参数和资源生命周期。初始化入口机制与其他扩展一致：`GetBackend` 未找到后端时抛出未初始化异常。原生调用保持单一流程，不另加失败或错误使用的兜底；`Success()` 仅输出调试信息。
- 内部按 DirectX 12、Vulkan 后端的方式组织不安全调用：直接构造原生结构、传入指针、显式管理所有权。使用 NGX.NET 的指针重载，不通过其 `string`、`in`、`out` 便利重载执行 NGX 调用。
- 后端保持单个 `partial DLSSBackend`，公共入口按 `Context.GraphicsApi` 分派到对应实现文件。公开功能类不包含图形 API 分支。
- 扩展仅依赖 `Zenith.NET` 与 `NGX.NET`。沿用现有 Vulkan 函数指针、局部 ABI 结构与数值映射，不新增 Vulkan NuGet 包、图形后端项目引用或原生桥接层。第 5 节记录的 RHI 改动已经存在，本次不再修改 RHI。
- 本轮先修订设计，确认后再修改实现。当前代码是上一版已编译的实现；第 5 节的历史验证与上一版编译结果不覆盖本轮新结构。

## 2. 公共 API

本节映射表与附录中的 `In*`、`pIn*` 等名称表示 NVIDIA SDK 的原生字段或参数键；新版 NGX.NET 的 C# 成员拼写见 3.5。NGX.NET 的命名调整不传播到公开成员；本轮明确调整的 Desc 配置以 2.3 的公共枚举为准。

### 2.1 入口

| 成员 | 说明 |
| --- | --- |
| `context.InitializeDLSS(project, version, dataDirectory)` | 显式初始化；每个 `GraphicsContext` 一次 |
| `context.DLSSCapabilities` | 能力（2.3） |
| `context.GetDLSSSuperResolutionOptimalSettings(outputWidth, outputHeight, mode)` | 超分辨率的推荐输入尺寸 |
| `context.GetDLSSRayReconstructionOptimalSettings(outputWidth, outputHeight, mode)` | 光线重建的推荐输入尺寸 |
| `context.CreateDLSSSuperResolution(desc)`、`CreateDLSSRayReconstruction(desc)`、`CreateDLSSFrameGeneration(desc)` | 创建功能对象，构造返回时原生特性已经创建 |
| `texture.DLSSBinding` | 查找所属后端，准备或复用原生绑定数据（2.2） |

初始化直接接收三个参数，不引入配置类型或类型组合重载：

| 参数 | 类型 | 约定 |
| --- | --- | --- |
| `project` | `Guid` | 应用项目的固定标识；内部以 `ToString("D")` 转为 NGX Project ID。每个应用独立保存，运行期间不重新生成 |
| `version` | `string` | 宿主渲染引擎版本；已有 `System.Version` 的调用方传入 `ToString()`，保留自定义版本文本的表达能力 |
| `dataDirectory` | `string` | NGX 写入日志和临时文件的目录；由应用准备并保证可写 |

三项均显式提供，不使用内置项目 GUID、程序集版本推断或临时目录默认值。这里的项目标识映射到 `InitWithProjectID`，不是 NVIDIA 分配的整数 Application ID。原生语义见 [NVIDIA 初始化接口](https://github.com/NVIDIA/DLSS/blob/main/include/nvsdk_ngx.h#L173-L223)。

每个上下文的 NGX 状态由内部的 `DLSSBackend` 持有，注册与释放方式与 Skia、ImGui 相同。以下为 `Extensions` 的节选：

```csharp
public static class Extensions
{
    private static readonly Lock @lock = new();
    private static readonly Dictionary<GraphicsContext, DLSSBackend> backends = [];

    extension(GraphicsContext context)
    {
        public void InitializeDLSS(Guid project, string version, string dataDirectory)
        {
            using Lock.Scope _ = @lock.EnterScope();

            if (!backends.TryGetValue(context, out DLSSBackend? backend))
            {
                backends[context] = backend = new(context, project, version, dataDirectory);

                context.Disposing += (_, _) =>
                {
                    using Lock.Scope _ = @lock.EnterScope();

                    if (backends.Remove(context))
                    {
                        backend.Dispose();
                    }
                };
            }
        }

        public DLSSCapabilities DLSSCapabilities => GetBackend(context).Capabilities;

        public DLSSSuperResolution CreateDLSSSuperResolution(DLSSSuperResolutionDesc desc)
        {
            return new(GetBackend(context), desc);
        }
    }

    extension(Texture texture)
    {
        public DLSSBinding DLSSBinding => GetBackend(texture.Context).Binding(texture);
    }

    private static DLSSBackend GetBackend(GraphicsContext context)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (!backends.TryGetValue(context, out DLSSBackend? backend))
        {
            throw new InvalidOperationException("The graphics context has not been initialized for DLSS.");
        }

        return backend;
    }
}
```

- 首次初始化注册后端并订阅 `context.Disposing`。重复调用直接复用，后传入的参数不生效，也不重新初始化 NGX。
- 两个最佳设置查询、三个创建方法及 `Texture.DLSSBinding` 都通过 `GetBackend(context)` 转发。绑定属性通过 `texture.Context` 找到所属后端，在 `Binding(texture)` 中准备原生数据；功能对象直接使用绑定中的数据。
- 移除 `AcquireContext`、`ReleaseContext`、引用计数及按设备名称保存的进程级能力缓存。能力实例由对应的 `DLSSBackend` 持有，不在进程级注册表中缓存。
- 查询、创建和释放功能对象都不改变共享后端的生命周期。即使没有存活的功能对象，NGX 仍保持初始化，直到 `context.Disposing`。
- 后续调用通过 `GetBackend` 取得已登记的后端；未登记时与 ImGui 一样抛出明确的未初始化异常。原生调用不另加初始化状态检查或静默返回路径。
- `Success()` 是内部扩展成员，只负责在 NGX 调用失败时输出 `Debug.WriteLine`，不改变控制流。各图形 API 分支直接调用原生方法并接 `.Success()`。

### 2.2 DLSSBinding

```csharp
using NGX.NET;

namespace Zenith.NET.Extensions.DLSS;

public readonly struct DLSSBinding(nint directX12, NGXResourceVK vulkan, NGXDimensions size)
{
    internal readonly nint DirectX12 = directX12;

    internal readonly NGXResourceVK Vulkan = vulkan;

    internal readonly NGXDimensions Size = size;
}
```

绑定通常通过 `texture.DLSSBinding` 取得，原生数据保存在内部只读字段中，不存储 `Texture` 实例，也不提供 `Dispose()`。C# 的主构函数本身是公开的，不能单独声明为 `internal`，因此这套写法只隐藏字段，不隐藏构造参数签名。

| 后端 | 获取绑定时准备 | 求值时使用 |
| --- | --- | --- |
| DirectX 12 | 借用的 `ID3D12Resource` 地址 | 直接读取 `DirectX12` |
| Vulkan | 创建 image view，填好 `NGXResourceVK` 的图像、视图、范围、格式与宽高 | 取 `Vulkan` 的局部副本，设置本次 `ReadWrite` 后传地址 |

`Size` 在获取绑定时从纹理描述填入，供现有帧生成的整张纹理子矩形使用。功能本身的输入、输出及有效区域尺寸继续使用 Desc / Args；派发期间不查询纹理尺寸或其他元信息。

后端只保留 `Texture → DLSSBinding` 的获取缓存，避免重复访问属性时再次创建 image view。取消索引分配及按索引查找原生资源的表，`Dispatch` 不访问绑定缓存。纹理释放沿用 ImGui 的内联 `Disposing` 写法。

- 必需资源使用 `DLSSBinding`，允许为空的资源使用 `DLSSBinding?`，只有 `null` 表示未提供。必需参数未填写不做检查。
- 绑定结构的复制只复制数据；原生资源生命周期仍跟随纹理。
- 只绑定 mip 0、layer 0，深度模板格式只取深度方面；不增加 `TextureView` 或 RHI `ResourceHandle` 入口。

三组 Args 的可空资源如下，其余必需资源维持 `DLSSBinding`；对应原生资源语义参见 NGX 的 [SR](https://github.com/NVIDIA/DLSS/blob/main/include/nvsdk_ngx_helpers_d3d.h)、[RR](https://github.com/NVIDIA/DLSS/blob/main/include/nvsdk_ngx_helpers_dlssd_d3d.h)、[FG](https://github.com/NVIDIA/DLSS/blob/main/include/nvsdk_ngx_helpers_dlssg_d3d.h) 声明：

| 功能 | 使用 `DLSSBinding?` 的字段 |
| --- | --- |
| 超分辨率 | `Exposure`、`ReactiveMask` |
| 光线重建 | `Roughness`、`SpecularMotionVectors`、`SpecularHitDistance`、`TransparencyLayer`、`TransparencyLayerOpacity`、`ColorBeforeTransparency`、`SubsurfaceScatteringGuide`、`DepthOfFieldGuide`、`Exposure`、`ReactiveMask` |
| 帧生成 | `HudlessColor`、`UI`、`UIAlpha`、`DistortionField` |

可空性表示对应 NGX 资源在允许的配置下可以传空指针，不取消条件要求。例如未打包粗糙度时仍须提供 `Roughness`，开启 UI 重组时仍须提供对应的 HUDless / UI 资源；这些条件继续由调用方遵守。

### 2.3 能力、档位与最佳设置

```csharp
using NGX.NET;

namespace Zenith.NET.Extensions.DLSS;

public unsafe class DLSSCapabilities
{
    internal DLSSCapabilities(NGXParameter parameters)
    {
        SuperResolutionSupported = GetI(parameters, Ngx.ParameterSuperSamplingAvailable) is not 0;
        RayReconstructionSupported = GetI(parameters, Ngx.ParameterSuperSamplingDenoisingAvailable) is not 0;
        FrameGenerationSupported = GetI(parameters, Ngx.ParameterFrameGenerationAvailable) is not 0;
    }

    public bool SuperResolutionSupported { get; }

    public bool RayReconstructionSupported { get; }

    public bool FrameGenerationSupported { get; }

    private static int GetI(NGXParameter parameters, ReadOnlySpan<byte> name)
    {
        fixed (byte* pointer = name)
        {
            int value = 0;
            Ngx.Parameter.GetI(parameters, pointer, &value).Success();

            return value;
        }
    }
}
```

与 RHI 的能力类一样，在构造函数内完成查询。后端在 NGX 初始化后取得能力参数块，将它交给此构造器；不再传入三个预先计算好的 bool。参数块仍由后端持有并释放，供最佳设置查询复用，能力对象只保存查询结果。

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

```csharp
namespace Zenith.NET.Extensions.DLSS;

public enum DLSSMode
{
    DLAA,

    UltraQuality,

    Quality,

    Balanced,

    Performance,

    UltraPerformance
}
```

| 类型或成员 | 取值 | NGX |
| --- | --- | --- |
| `SuperResolutionSupported`、`RayReconstructionSupported`、`FrameGenerationSupported` | | `SuperSampling.Available`、`SuperSamplingDenoising.Available`、`FrameGeneration.Available` |
| `DLSSOptimalSettings` | 0 表示该档位或尺寸不受支持 | `DLSS.GetOptimalSettings`、`DLSSD.GetOptimalSettings` |
| `DLSSMode` | `DLAA`、`UltraQuality`、`Quality`、`Balanced`、`Performance`、`UltraPerformance` | `NGXPerfQualityValue` 的 `Dlaa`、`UltraQuality`、`MaxQuality`、`Balanced`、`MaxPerf`、`UltraPerformance` |

- `DLSSMode` 由超分辨率与光线重建共用，成员按画质从高到低排列。
- 渲染预设决定 DLSS 使用哪个模型，不公开，固定为 `Default`：NGX 按档位选用 NVIDIA 推荐的模型，并随驱动更新。

创建配置统一使用以下枚举；`Reset` 等每帧参数仍留在 Args。

```csharp
[Flags]
public enum DLSSFeatureFlags
{
    None = 0,

    Hdr = 1 << 0,

    AutoExposure = 1 << 1,

    DepthReversed = 1 << 2,

    MotionVectorJittered = 1 << 3,

    AlphaUpscaling = 1 << 4,

    DynamicResolution = 1 << 5,

    UIRecomposition = 1 << 6
}
```

```csharp
public enum DLSSDepthType
{
    Hardware,

    Linear
}
```

```csharp
public enum DLSSRoughnessMode
{
    Unpacked,

    Packed
}
```

| Desc | 配置字段 | 使用的 Flags |
| --- | --- | --- |
| 超分辨率 | `Mode`、`Flags` | Hdr、AutoExposure、DepthReversed、MotionVectorJittered、AlphaUpscaling |
| 光线重建 | `Mode`、`DepthType`、`RoughnessMode`、`Flags` | DepthReversed、MotionVectorJittered、AlphaUpscaling；HDR 与低分辨率运动矢量仍由内部固定 |
| 帧生成 | `Flags` | Hdr、DepthReversed、MotionVectorJittered、DynamicResolution、UIRecomposition |

枚举取值遵循公共 API 的语义顺序，由 `DLSSFormats` 显式映射到 NGX。`Hardware = 0`、`Unpacked = 0` 保留原布尔字段为 false 时的语义。帧生成的 Flags 分别写入其创建结构、求值结构及参数键，不直接转换成 NGX 的 SR/RR 特性位掩码。

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

    public DLSSFeatureFlags Flags;
}
```

```csharp
namespace Zenith.NET.Extensions.DLSS;

public struct DLSSSuperResolutionArgs
{
    public DLSSBinding Input;

    public DLSSBinding Depth;

    public DLSSBinding MotionVectors;

    public DLSSBinding? Exposure;

    public DLSSBinding? ReactiveMask;

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
| `Flags` 中的 Hdr、AutoExposure、DepthReversed、MotionVectorJittered、AlphaUpscaling | `IsHDR`、`AutoExposure`、`DepthInverted`、`MVJittered`、`AlphaUpscaling` 标志 | Alpha 取自 `Input` 的 A 通道 |
| `Input`、`Depth`、`MotionVectors`、`Output` | `pInColor`、`pInDepth`、`pInMotionVectors`、`pInOutput` | 必需 |
| `Exposure` | `pInExposureTexture` | 1×1 曝光纹理 |
| `ReactiveMask` | `pInBiasCurrentColorMask` | 0 到 1，偏向当前帧的程度 |
| `InputContentWidth`、`InputContentHeight` | `InRenderSubrectDimensions` | 必需；本帧的渲染尺寸，不使用动态分辨率时等于 `InputWidth`、`InputHeight` |
| `JitterOffsetX`、`JitterOffsetY` | `InJitterOffsetX`、`InJitterOffsetY` | 输入像素 |
| `MotionVectorScaleX`、`MotionVectorScaleY` | `InMVScaleX`、`InMVScaleY` | 把运动矢量换算到输入像素；0 时 NGX 按 1 处理 |
| `PreExposure`、`ExposureScale` | `InPreExposure`、`InExposureScale` | 0 时 NGX 按 1 处理 |
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

    public DLSSDepthType DepthType;

    public DLSSRoughnessMode RoughnessMode;

    public DLSSFeatureFlags Flags;
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

    public DLSSBinding? Roughness;

    public DLSSBinding Depth;

    public DLSSBinding MotionVectors;

    public DLSSBinding? SpecularMotionVectors;

    public DLSSBinding? SpecularHitDistance;

    public DLSSBinding? TransparencyLayer;

    public DLSSBinding? TransparencyLayerOpacity;

    public DLSSBinding? ColorBeforeTransparency;

    public DLSSBinding? SubsurfaceScatteringGuide;

    public DLSSBinding? DepthOfFieldGuide;

    public DLSSBinding? Exposure;

    public DLSSBinding? ReactiveMask;

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
| `Flags` 中的 DepthReversed、MotionVectorJittered、AlphaUpscaling | `DepthInverted`、`MVJittered`、`AlphaUpscaling` 标志 | Alpha 取自 `Input` 的 A 通道 |
| `DepthType` | `InUseHWDepth`：`Linear` 或 `HW` | |
| `RoughnessMode` | `InRoughnessMode`：`Packed` 或 `Unpacked` | 打包时粗糙度在 `Normals` 的 A 通道 |
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
    public PixelFormat Format;

    public uint InputWidth;

    public uint InputHeight;

    public uint OutputWidth;

    public uint OutputHeight;

    public DLSSFeatureFlags Flags;
}
```

```csharp
using System.Numerics;

namespace Zenith.NET.Extensions.DLSS;

public struct DLSSFrameGenerationArgs
{
    public DLSSBinding Color;

    public DLSSBinding? HudlessColor;

    public DLSSBinding? UI;

    public DLSSBinding? UIAlpha;

    public DLSSBinding? DistortionField;

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
| `Format` | `NativeBackbufferFormat` | `Color` 与 `Output` 的格式，转为 DXGI_FORMAT 或 VkFormat 的数值 |
| `InputWidth`、`InputHeight` | `RenderWidth`、`RenderHeight` | 深度与运动矢量的分辨率；使用动态分辨率时为上限 |
| `OutputWidth`、`OutputHeight` | `Width`、`Height` | 颜色与生成帧的分辨率 |
| `Flags` 中的 Hdr、DepthReversed | `ColorBuffersHDR`、`DepthInverted` | |
| `Flags.MotionVectorJittered` | `DLSSG.MvecJittered` | |
| `Flags.DynamicResolution` | `DynamicResolutionScaling` | |
| `Flags.UIRecomposition` | `DLSSG.UserInterfaceRecompositionEnabled` | 需要 `HudlessColor`，以及 `UI` 或 `UIAlpha` |
| `Color`、`Depth`、`MotionVectors`、`Output` | `pBackbuffer`、`pDepth`、`pMVecs`、`pOutputInterpFrame` | 必需；`Output` 与 `Color` 同格式 |
| `HudlessColor` | `pHudless` | 绘制 UI 之前的同一帧，强烈建议提供 |
| `UI`、`UIAlpha` | `pUI`、`pUIAlpha` | 预乘 Alpha 的 UI，或单通道的 UI 不透明度，二选一 |
| `DistortionField` | `pBidirectionalDistortionField` | 镜头畸变等后处理的双向畸变场 |
| `InputContentWidth`、`InputContentHeight` | `MvecsSubrectSize`、`DepthSubrectSize` | 动态分辨率下本帧的有效区域；未启用动态分辨率时可为 0 |
| `JitterOffsetX`、`JitterOffsetY` | `JitterOffset` | 裁剪空间 |
| `MotionVectorScaleX`、`MotionVectorScaleY` | `MvecScale` | 必需；把运动矢量归一化到 [-1, 1] |
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

参考做法，线程划分与呈现顺序同 `PresentThread`：

- 交换链以 `UsePresentQueue = true` 创建。生成帧与真实帧都在 `swapChain.Queue` 上拷贝到 `Drawable` 并呈现，不排在 `GraphicsQueue` 的渲染工作之后。拷贝与呈现的写法见文档「平台集成」中的「使用呈现队列」。
- 渲染线程：把本帧最终画面（含 UI）渲染到后缓冲，作为帧生成的 `Color`；`Dispatch` 输出到生成帧纹理，提交后把完成值交给呈现线程。
- 呈现线程：帧生成完成后拷贝并呈现生成帧，拷贝以帧生成的完成值为等待条件；等到约半个帧间隔后，拷贝并呈现真实帧。帧间隔取最近几次真实帧呈现间隔的平均值，生成帧与真实帧各显示约半个间隔。第一帧与 `Reset` 为真时只呈现真实帧。
- 后缓冲与生成帧纹理各两张交替使用。渲染线程开始使用某一张之前，等呈现线程用完它，即最多一帧在途。这个等待用呈现线程的通知或 `IsCompleted` 判断，不对呈现队列的完成值调用 `Wait()`：`Timeline.Wait` 在等待期间持有该时间线的锁，会挡住呈现线程的 `Submit` 与 `Present`。
- 交换链尺寸变化时，先停止呈现线程，再调用 `SwapChain.Resize`，然后重建帧生成对象与上述纹理。

### 2.8 示例

以下示例均在创建 `GraphicsContext` 后先初始化一次。`project` 是应用保存的固定 GUID，`version` 与 `dataDirectory` 由应用提供：

```csharp
context.InitializeDLSS(project, version, dataDirectory);

DLSSCapabilities capabilities = context.DLSSCapabilities;
```

应用依据能力选择已有的渲染路径，再进行最佳设置查询和功能对象创建。CornellBox 在创建 renderer 之前完成此初始化。

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
    Flags = DLSSFeatureFlags.Hdr | DLSSFeatureFlags.AutoExposure
});

commandBuffer.Transition(output, default, TextureLayout.Sampled, TextureLayout.Storage);

superResolution.Dispatch(commandBuffer, new()
{
    Input = color.DLSSBinding,
    Depth = depth.DLSSBinding,
    MotionVectors = motionVectors.DLSSBinding,
    Output = output.DLSSBinding,
    InputContentWidth = settings.InputWidth,
    InputContentHeight = settings.InputHeight,
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
    Format = swapChain.Desc.Format,
    InputWidth = renderWidth,
    InputHeight = renderHeight,
    OutputWidth = swapChain.Desc.Surface.Width,
    OutputHeight = swapChain.Desc.Surface.Height,
    Flags = DLSSFeatureFlags.UIRecomposition
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

按 RHI 的方式分组，分类目录不改变公共命名空间 `Zenith.NET.Extensions.DLSS`：

```text
Extensions.cs
DLSSCapabilities.cs
DLSSFeature.cs
DLSSSuperResolution.cs
DLSSRayReconstruction.cs
DLSSFrameGeneration.cs
Enums/
    DLSSMode.cs
    DLSSFeatureFlags.cs
    DLSSDepthType.cs
    DLSSRoughnessMode.cs
Structs/
    DLSSBinding.cs
    DLSSOptimalSettings.cs
    DLSSSuperResolutionDesc.cs
    DLSSSuperResolutionArgs.cs
    DLSSRayReconstructionDesc.cs
    DLSSRayReconstructionArgs.cs
    DLSSFrameGenerationDesc.cs
    DLSSFrameGenerationArgs.cs
Backends/
    DLSSBackend.cs
    DLSSBackend.DirectX12.cs
    DLSSBackend.Vulkan.cs
    DLSSFormats.cs
```

Vulkan 的 file-local ABI 声明放在 `DLSSBackend.Vulkan.cs` 后部，不增加 Vulkan NuGet 包或新的后端项目。

### 3.2 DLSSBackend

保持一个 `internal unsafe partial class DLSSBackend : DisposableObject`，按实现内容分成三个文件：

| 文件 | 负责内容 |
| --- | --- |
| `DLSSBackend.cs` | 构造函数、GraphicsContext、锁、能力与参数管理、绑定缓存、公共入口及 GraphicsApi 分派 |
| `DLSSBackend.DirectX12.cs` | D3D12 初始化、分配、绑定、特性创建、求值与释放的具体方法 |
| `DLSSBackend.Vulkan.cs` | Vulkan 对应方法、函数指针、image view、空描述符集与局部 ABI 声明 |

三个文件共享同一个实例的字段。公共入口通过 `switch (Context.GraphicsApi)` 调用对应的私有实现方法；不增加后端派生类、接口或 API 子目录。初始化参数仍是 `project`、`version`、`dataDirectory`。

初始化内存改为调用内作用域：

```csharp
using NGXStrings strings = new();

byte* projectId = strings.AllocUtf8(project.ToString("D"));
byte* engineVersion = strings.AllocUtf8(version);
void* applicationDataPath = strings.AllocWide(dataDirectory);
void* path = strings.AllocWide(Ngx.RuntimeDirectory);

NGXFeatureCommonInfo featureInfo = new()
{
    PathListInfo = new()
    {
        Path = &path,
        Length = 1
    }
};
```

不保留实例级或 static 的 `NGXStrings`、`ZenithMarshal.Scope` 字段。项目、版本、数据目录和搜索路径缓冲区在 Init 调用后释放；Vulkan 函数名转换使用所在调用的局部 scope。NGX.NET 310.9.1.1 的字符串重载采用相同的局部 `NGXStrings` 用法；[NVIDIA Streamline 初始化实现](https://github.com/NVIDIA-RTX/Streamline/blob/2122257e0fce486f91b385aa63b9a09b0a34b363/source/plugins/sl.common/commonEntry.cpp#L1454-L1521) 也使用局部路径内容与指针数组。这里仍调用 unsafe 指针接口。

能力参数块需要跨调用供最佳设置查询使用，由后端保存至销毁；设备、能力参数、Vulkan view 与辅助对象的原生生命周期不随临时字符串一起结束。NGX 初始化后，后端取得能力参数块，再调用 `new DLSSCapabilities(parameters)`，由能力类自己查询三项支持状态。

绑定继续只用一个 `Dictionary<Texture, DLSSBinding>` 缓存完整原生数据。共享获取流程调用对应 API 的绑定创建方法；首次获取时内联订阅 `Texture.Disposing`，删除缓存并调用对应 API 的绑定销毁方法。DX12 只借用资源地址，Vulkan 释放自身创建的 view。Dispatch 直接使用绑定值，不查表，不新增独立的资源注册框架。

后端退出时先销毁缓存中的原生绑定，清空缓存，再销毁该后端辅助对象、能力参数块并关闭 NGX。原生调用仍直接接 `.Success()`，不增加错误使用的兜底流程。

### 3.3 DLSSFeature 与功能类

新增公共抽象基类，集中参数块与特性句柄的所有权。三个公开功能类都继承它，不再各自声明和释放一份重复的状态：

```csharp
using NGX.NET;

namespace Zenith.NET.Extensions.DLSS;

public abstract class DLSSFeature : DisposableObject
{
    internal readonly DLSSBackend Backend;
    internal readonly NGXParameter Parameters;

    internal NGXHandle Handle;

    internal DLSSFeature(DLSSBackend backend)
    {
        Backend = backend;
        Parameters = backend.AllocateParameters();
    }

    protected override void Destroy()
    {
        Backend.ReleaseFeature(Handle);
        Backend.DestroyParameters(Parameters);
    }
}
```

`DLSSFeature` 必须是 public，才能作为现有公开功能类的基类；其后端、参数与 Handle 均保持 internal。基类构造器只分配公共参数，不调用依赖派生 Desc 的虚方法。`FreeMemOnReleaseFeature = 1` 仍在参数分配时设置。

具体功能构造器先保存 Desc；FG 还先设置运动矢量抖动与 UI 重组参数，然后创建原生 Handle。以超分辨率的构造流程为例：

```csharp
internal DLSSSuperResolution(DLSSBackend backend, DLSSSuperResolutionDesc desc) : base(backend)
{
    Desc = desc;

    using Lock.Scope _ = DLSSBackend.Lock.EnterScope();

    CommandBuffer commandBuffer = backend.Context.GraphicsQueue.CommandBuffer();
    backend.Create(commandBuffer, this);
    commandBuffer.Submit().Wait();
}
```

后端为三种 feature 提供对应的 Create / Evaluate 实现，Create 直接向 `feature.Handle` 写入原生结果。类字段取址仍用 `fixed`，不增加临时句柄或成功后接管流程。

NGX 的 Create 需要正在录制的命令列表：[D3D12](https://github.com/NVIDIA/DLSS/blob/main/include/nvsdk_ngx.h#L473-L535)、[Vulkan](https://github.com/NVIDIA/DLSS/blob/main/include/nvsdk_ngx_vk.h#L466-L524)。为保留 `CreateXxx(desc)` 签名，构造过程采用与 [SkiaTexture](../Zenith.NET.Extensions.Skia/SkiaTexture.cs) 相同的 GraphicsQueue 录制、提交并等待方式。命令缓冲由 RHI 队列池管理，不新建原生命令池或手动销毁它。

这会把创建命令及一次 GPU 等待移到构造阶段。构造返回后 Handle 已创建，`Dispatch` 只进行调试事件、屏障及 Evaluate，不再判断 Handle 是否需要首次创建。Vulkan 的空描述符集由 Vulkan 后端在调用 NGX 前绑定。

| | 超分辨率 | 光线重建 | 帧生成 |
| --- | --- | --- | --- |
| 创建参数 | `NGXDLSSCreateParams` | `NGXDLSSDCreateParams` | `NGXDLSSGCreateParams` |
| 创建（DirectX 12） | `D3D12.CreateDLSSExt` | `D3D12.CreateDLSSDExt` | `D3D12.CreateDLSSG` |
| 创建（Vulkan） | `Vulkan.CreateDLSSExt1` | `Vulkan.CreateDLSSDExt1` | `Vulkan.CreateDLSSG` |
| 求值 | `EvaluateDLSSExt` | `EvaluateDLSSDExt` | `EvaluateDLSSG` |

### 3.4 格式与配置映射

`DLSSFormats` 保留 PixelFormat 到 DXGI_FORMAT / VkFormat 的数值映射，以及 `DLSSMode` 到 NGX 档位的映射，不依赖 Silk.NET。

- `DLSSFeatureFlags` 按各功能实际使用的位逐项映射；SR / RR 的低分辨率运动矢量固定项、RR 的 HDR 固定项保持现有语义。
- `DLSSDepthType.Hardware` / `Linear` 分别映射到 `NGXDLSSDepthType.Hw` / `Linear`；不能直接按数值强转。
- `DLSSRoughnessMode.Unpacked` / `Packed` 分别映射到对应 NGX 枚举。
- FG 的动态分辨率写入创建参数，HDR 与反转深度写入求值参数，运动矢量抖动与 UI 重组写入参数块；不引入另一组公开 FG flags 类型。

### 3.5 NGX.NET 310.9.1.1 与不安全调用

本节以 [NuGet 310.9.1.1 发布元数据](https://api.nuget.org/v3-flatcontainer/ngx.net/310.9.1.1/ngx.net.nuspec) 指向的提交 [`548477c66462fa851fe6389397f65bbaf2eb915f`](https://github.com/qian-o/NGX.NET/tree/548477c66462fa851fe6389397f65bbaf2eb915f/NGX.NET) 为基线。该包无 Vulkan NuGet 依赖，`Directory.Packages.props` 已更新为此版本。

| 310.9.1 的内部用法 | 310.9.1.1 的用法 |
| --- | --- |
| `using Ngx = NGX.NET.NGX` | `using NGX.NET` 后直接调用真实类型 `Ngx`，移除旧别名 |
| `NGXParameter*`、`NGXHandle*` 成员字段 | `NGXParameter`、`NGXHandle` 值句柄；`default` 为空，使用 `IsNull` 判断 |
| 分配参数、取得能力、创建特性的双重指针输出 | 直接向目标值句柄传入地址，如 `&parameters`、`&handle`；仅输出位置使用句柄指针 |
| Evaluate、Destroy、Release 接收句柄指针 | 接收值句柄；创建与求值结构仍传 `&createParams`、`&evalParams` |
| `Ngx.Succeeded`、`Ngx.Failed` | `Ngx.NVSDK_NGX_SUCCEED`、`Ngx.NVSDK_NGX_FAILED`；`Success()` 使用失败宏输出诊断 |
| UTF-8 `sbyte*` | `byte*`，包括 Vulkan GIPA/GDPA 的函数名称参数 |
| `NGXMarshal`、`NGXEncoding` | `NGXStrings.AllocUtf8`、`AllocWide` 分配缓冲区，再传给指针重载 |
| 参数键为 `string` | 带 NUL 终止符的 `ReadOnlySpan<byte>` 常量，用 `fixed` 取得 `byte*`；不转回字符串，不重复追加终止符 |
| `NGXBool8` | `Bool8`，按字段类型与 `bool` 隐式转换；`int`、`uint`、`byte` 参数仍按各自 ABI 转换 |
| `NGXVersion.API`、`NGXEngineType.CUSTOM` | `NGXVersion.Api`、`NGXEngineType.Custom` |
| `NGXPerfQualityValue.DLAA`、`NGXDLSSDenoiseMode.DLUnified`、`NGXDLSSDepthType.HW` | `Dlaa`、`DlUnified`、`Hw`；公共 `DLSSMode.DLAA` 保持不变 |
| `NGXDLSSFeatureFlags.IsHDR`、`MVLowRes`、`MVJittered` | `IsHdr`、`MvLowRes`、`MvJittered` |
| `NGXResourceVKType.VKIMAGEVIEW` | `NGXResourceVKType.VkImageView` |

原生结构按新版生成声明逐项适配，保留现有字段映射语义：

| 位置 | 旧 C# 成员示例 | 新 C# 成员示例 |
| --- | --- | --- |
| SR 创建的 `Feature`、RR 创建 | `InWidth`、`InHeight`、`InTargetWidth`、`InTargetHeight`、`InPerfQualityValue` | `Width`、`Height`、`TargetWidth`、`TargetHeight`、`PerfQualityValue` |
| SR / RR 创建 | `InFeatureCreateFlags`、`InEnableOutputSubrects` | `FeatureCreateFlags`、`EnableOutputSubrects` |
| RR 创建 | `InDenoiseMode`、`InRoughnessMode`、`InUseHWDepth` | `DenoiseMode`、`RoughnessMode`、`UseHWDepth` |
| SR / RR 求值 | `PInColor`、`PInOutput`、`PInDepth`、`PInMotionVectors` | `Color`、`Output`、`Depth`、`MotionVectors`；SR 的颜色与输出仍在 `Feature` 中 |
| SR / RR 求值 | `InReset`、`InJitterOffsetX`、`InMVScaleX`、`InRenderSubrectDimensions` | `Reset`、`JitterOffsetX`、`MVScaleX`、`RenderSubrectDimensions`；`Reset` 仍为 `int`，传 `args.Reset ? 1 : 0` |
| RR 求值 | `PInWorldToViewMatrix`、`PInViewToClipMatrix` | `WorldToViewMatrix`、`ViewToClipMatrix`，仍为 `Matrix4x4*` |
| FG 求值 | `PBackbuffer`、`PDepth`、`PMVecs`、`POutputInterpFrame` | `Backbuffer`、`Depth`、`MVecs`、`OutputInterpFrame` |

DirectX 12 直接使用绑定内部的地址，必需资源读取 `args.Input.DirectX12`，可选资源如曝光纹理读取 `args.Exposure?.DirectX12 ?? 0`，不再通过后端解析。

Vulkan 的 `NGXResourceVK` 已在绑定时构造。求值时只复制到局部变量或栈数组，按本次输入或输出角色设置 `ReadWrite`，再把地址传给 NGX。可选资源为 `null` 时传空指针，不修改已保存的绑定数据。例如：

```csharp
NGXResourceVK input = args.Input.Vulkan;
input.ReadWrite = false;

NGXResourceVK output = args.Output.Vulkan;
output.ReadWrite = true;

NGXResourceVK exposure = args.Exposure?.Vulkan ?? default;
exposure.ReadWrite = false;

NGXVKDLSSEvalParams evalParams = new()
{
    Feature = new() { Color = &input, Output = &output },
    ExposureTexture = args.Exposure.HasValue ? &exposure : null
};
```

此处仅节选资源赋值，其余求值字段按 2.4 至 2.6 填写。删除后端的 `D3D12Resource`、`VulkanResource` 解析与 `Size` 查询。帧生成直接使用绑定中准备好的尺寸，例如 `args.Color.Size`、`args.HudlessColor?.Size ?? default`。矩阵继续直接传值或地址，不添加转置或数据换算。

NGX 调用采用图形后端同样的局部结构、地址传参和尾部 `.Success()` 风格，参照 [DXComputePipeline](../../Zenith.NET.DirectX12/DXComputePipeline.cs)、[VKGraphicsContext](../../Zenith.NET.Vulkan/VKGraphicsContext.cs) 与 [VKShader](../../Zenith.NET.Vulkan/VKShader.cs)。以下为 DX12 后端的参数分配与参数键使用方式，位于对应的 NGX 锁内；后端辅助方法如接收参数键，使用 `ReadOnlySpan<byte> name` 并在调用处固定：

```csharp
NGXParameter parameters = default;

Ngx.D3D12.AllocateParameters(&parameters).Success();

fixed (byte* name = Ngx.ParameterFreeMemOnReleaseFeature)
{
    Ngx.Parameter.SetUI(parameters, name, 1);
}
```

`GetCapabilityParameters` 由对应 API 的实现取得能力参数块；`DLSSCapabilities` 在自身构造器中完成查询。两个 `GetOptimalSettings` 方法仍将能力句柄按值传入，各宽高和 sharpness 传局部变量地址。

特性创建位于对应 partial 文件，例如 DX12 已准备好 `createParams` 后，直接填写基类持有的 Handle：

```csharp
fixed (NGXHandle* handle = &feature.Handle)
{
    Ngx.D3D12.CreateDLSSExt(commandList, 1, 1, handle, feature.Parameters, &createParams).Success();
}
```

Vulkan 实现在 `DLSSBackend.Vulkan.cs` 中调用 `CreateDLSSExt1`；RR、FG 同理。Evaluate 仍构造各自原生求值结构并传地址，数据映射与 2.4 至 2.6 一致。Vulkan GIPA/GDPA 的指针类型仍为 `delegate* unmanaged[Cdecl]<nint, byte*, delegate* unmanaged[Cdecl]<void>>`。

`NGXDLSSGOptEvalParams` 在新版有无参构造器，`new()` 已设置 `MultiFrameCount = 1`、`MultiFrameIndex = 1`、`MinRelativeLinearDepthObjectSeparation = 40.0f`。保持现有 2 倍帧率语义，使用 `new() { ... }` 并沿用这些默认值；不能改为 `default` 后只填写其余字段，也不在扩展中重复维护相同默认数值。该结构及 `NGXResourceVK.ReadWrite` 的 `Bool8` 字段直接赋 `bool`，与 SR / RR 的整数 `Reset` 区别处理。

## 4. 使用约定

- **初始化**：先调用 `InitializeDLSS(project, version, dataDirectory)`，再查询能力、最佳设置、创建功能对象或访问 `texture.DLSSBinding`。参数首次生效，NGX 是否可用由能力查询体现。
- **命令缓冲**：使用图形队列或计算队列，不在渲染通道内调用；传输队列不能执行计算。调用前输入处于 `Sampled`、输出处于 `Storage`，调用后布局不变。`Dispatch` 在 NGX 求值前后各插入一次 `Barrier(All, All)`。
- **状态**：NGX 会替换管线、描述符与描述符堆。DLSS 只出现在两段完整的命令之间，扩展不保存也不恢复命令缓冲状态；后续命令照常先 `SetPipeline`，即使是 DLSS 之前用过的同一管线，后端在 `SetPipelineImpl` 中重新绑定描述符堆。
- **纹理用途**：输入需要 `Sampled`；输出需要 `Storage` 与 `TransferDst`。Vulkan 下 NGX 会用 `vkCmdClearColorImage` 清除输出：本机超分辨率使用预设 K（DLAA、Quality、Balanced 档位的默认预设）时出现，输出缺少 `TransferDst` 时验证层报错。
- **单位**：按 NGX 原义。超分辨率与光线重建的抖动以输入像素为单位，运动矢量经 `MotionVectorScale` 换算到输入像素；帧生成的抖动在裁剪空间，运动矢量经 `MotionVectorScale` 归一化到 [-1, 1]。矩阵按 `System.Numerics` 原样传递，与 NGX 的行主序一致，且不含抖动。
- **CornellBox**：运动矢量按 Upscaling 扩展（SGSR2）的约定编码，存的是 0.2495 ×（当前帧 − 上一帧的 NDC）+ 0.5。NGX 只有缩放没有偏移，接入 DLSS 时需写入未编码的 NDC 差值；此时超分辨率与光线重建的 `MotionVectorScale` 取 (−0.5·W, 0.5·H)，帧生成取 (−0.5, 0.5)，W、H 为渲染尺寸。
- **重建**：Desc 不可变，任一字段变化时重建。构造期间即提交创建命令并等待完成；旧对象仍需等 GPU 完成引用后释放。交换链尺寸变化时重建帧生成对象（2.7）。
- **生命周期与线程**：相关 GPU 工作完成后再释放纹理和功能对象，并在 `GraphicsContext` 之前释放；绑定值不延长资源生命周期。纹理释放时内联清理绑定，后端退出时清理剩余 view。同一功能对象不可并发 `Dispatch`。

## 5. RHI 依赖与验证

以下是 310.9.1 实现已有的 RHI 改动与历史验证记录，保留原环境和结果。它们不构成 310.9.1.1 或本次初始化、生命周期重构的验证结论。

已存在的 RHI 改动：

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

与 Zenith 命令混合录制在同一环境下验证（D3D12 调试层、Vulkan 验证层）：

| 项 | 结果 |
| --- | --- |
| 覆盖范围 | DirectX 12 与 Vulkan，图形队列与计算队列，只含必需输入与含全部可选输入。每帧一个命令缓冲：Zenith 的计算管线写入 DLSS 的输入，NGX 求值（第一帧创建特性），再用同一计算管线复制输出、用图形管线绘制输出（计算队列上只复制），共 3 帧 |
| 结果 | 三项功能的 NGX 调用全部成功，输出与输入一致；NGX 之后的复制与绘制结果与 NGX 输出逐字节相同；没有验证消息。Vulkan 的结果是在绑定空描述符集（3.3）之后得到的 |
| Vulkan 输出为 0 | 不绑定空描述符集时，三项功能在图形队列与计算队列上输出都为 0，NGX 与验证层都不报错。触发条件是同一 VkQueue 上在特性第一次求值之前调用过 `SetPipeline`，只写描述符不会触发；第一次求值之后再调用，或第一次求值放在另一个 VkQueue 上，都不受影响。NGX 调用前绑定空描述符集或其他描述符集可以避免，只绑定不使用描述符堆的管线不行。DirectX 12 没有这个问题 |
| 参数 | 超分辨率的 `InputContentWidth`、`InputContentHeight` 为 0 时求值返回 `FAIL_InvalidParameter`；帧生成未启用动态分辨率时为 0 可正常求值。启用 Alpha 放大且 `Input` 的 A 通道为 0.5 时，超分辨率与光线重建输出的 A 通道也为 0.5。两个后端结果相同 |

呈现队列在同一环境下验证（D3D12 调试层、Vulkan 验证层）：

| 项 | 结果 |
| --- | --- |
| 队列 | DirectX 12 创建独立的 DIRECT 队列；Vulkan 取图形队列族中另一个队列，`PresentQueue.Type` 为 `Present` |
| 不排在渲染工作之后 | `GraphicsQueue` 上有约 25 ms 的拷贝负载时，在呈现队列上拷贝并呈现约 5 ms（DirectX 12）与 2 ms（Vulkan），且都在负载完成前结束；走 `GraphicsQueue` 时要等负载结束 |
| 线程 | 呈现线程与渲染线程同时提交，没有验证消息 |
| 拷贝所在队列 | 在 `TransferQueue` 上写入交换链图像时，D3D12 调试层报错并移除设备，所以拷贝放在 `swapChain.Queue` |

### 5.1 本次修订状态

上一版实现已完成 `dotnet build sources/Experiments/CornellBox/CornellBox.csproj -c Release`，0 警告、0 错误；未做 NGX GPU 运行验证。

本轮只更新后端结构设计，枚举配置、DX12 / Vulkan partial 实现文件、能力构造查询、局部初始化内存、DLSSFeature 基类和构造期创建尚未实现。前述编译结果与第 5 节历史运行结果均不覆盖本轮草案。

确认后同步修改扩展与 CornellBox。调用方的布尔 Desc 字段改为相应枚举；Metal 等不使用 NGX 的路径不构造假的能力对象，应用可用可空能力引用表示未启用 DLSS。公共命名空间、Dispatch 参数及绑定数据保持当前约定，不扩展 RHI 或新增 Vulkan 包。

## 附录：NGX 初始化与固定参数

| 功能 | 参数 | 值 |
| --- | --- | --- |
| 全部 | `CreationNodeMask`、`VisibilityNodeMask` | 1 |
| 全部 | `FreeMemOnReleaseFeature` | 1 |
| 全部 | Project ID、Engine Version、Application Data Path | 来自 `InitializeDLSS` 的 `project`、`version`、`dataDirectory`，见 2.1 |
| 全部 | 引擎类型、SDK 版本、功能路径、日志配置 | 固定配置见 3.2 |
| 超分辨率、光线重建 | 渲染预设提示 | `Default`，由 NGX 按档位选择 |
| 超分辨率、光线重建 | `MVLowRes` 标志 | 设置 |
| 超分辨率、光线重建 | `InEnableOutputSubrects`、各子矩形基点 | `false`、0 |
| 超分辨率、光线重建 | `InSharpness`、`DoSharpening` | 0、不设置（锐化已从 DLSS 移除） |
| 超分辨率、光线重建 | 研究、调试与保留用途的参数：`GBufferSurface`、`InToneMapperType`、`pInMotionVectors3D`、`pInIsParticleMask`、`pInAnimatedTextureMask`、`pInDepthHighRes`、`pInPositionViewSpace`、`InFrameTimeDeltaInMsec`、`pInRayTracingHitDistance`、`pInTransparencyMask`、指示器坐标轴翻转，以及超分辨率的 `pInMotionVectorsReflections` | 默认值 |
| 光线重建 | `IsHDR` 标志、`InDenoiseMode`、`InRenderSubrectDimensions` | 设置、`DLUnified`、Desc 的输入尺寸 |
| 光线重建 | `pInAlpha`、`pInOutputAlpha` | `null` |
| 光线重建 | Streamline 指南未说明语义的输入：`pInReflectedAlbedo`、`pInScreenSpaceRefractionGuide`，`ColorBeforeTransparency` 以外的各类 ColorBefore/After，各类射线方向，漫反射命中距离，`pInTransparencyLayerMvecs`、`pInDisocclusionMask`、`pInResponsivityMask` | `null` |
| 帧生成 | `MultiFrameCount`、`MultiFrameIndex` | 新版 `NGXDLSSGOptEvalParams` 的 `new()` 默认值 1、1 |
| 帧生成 | `ClipToLensClip` | 单位矩阵 |
| 帧生成 | `CameraMotionIncluded` | `true` |
| 帧生成 | `MinRelativeLinearDepthObjectSeparation` | 新版 `NGXDLSSGOptEvalParams` 的 `new()` 默认值 40；不能用 `default` 的 0 覆盖 |
| 帧生成 | `MotionVectorsDilated`、`MotionVectorsInvalidValue`、`BidirectionalDistFieldPrecisionInfo` | 默认值 |
| 帧生成 | `NotRenderingGameFrames`、`MenuDetectionEnabled`、`AutomodeOverrideReset`、`pOutputDisableInterpolation` | `false`、`null` |
| 帧生成 | `pOutputRealFrame` 及其子矩形、`InvertXAxis`、`InvertYAxis`、`UserDebugText` | `null`、0 |
| 帧生成 | 运动矢量与深度的子矩形基点 | 0 |
| 帧生成 | 颜色、生成帧、HUDless、UI、UI 不透明度、畸变场的子矩形 | 整张纹理 |
| 帧生成 | `LinearizedDepth_Scale`、`LinearizedDepth_NearFarPartition`、`ResourceAlwaysProvidedFlags`、`ResourceNeverProvidedFlags`、`AsyncCreateEnabled`、`EvalFlags`、`BackbufferFrameID`、`FullscreenMode`、`TargetFrameRate` | 不设置 |
