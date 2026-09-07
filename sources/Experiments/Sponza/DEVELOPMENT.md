# Sponza 渲染与超分实验开发文档

**v2.0 实施规格，替代 v1.2。** 开始前阅读本文与仓库根目录的 [Zenith.NET 代码风格与规范](</Users/wangxi/Documents/GitHub/Zenith.NET/Zenith.NET 代码风格与规范.md>)，并完成第 2 节要求的源码阅读。本文规定项目范围、设计和验收；根规范规定代码写法。当前 Renderer/Pass/Shader 仍是骨架，本文不代表功能或画质已经完成。

## 1. 目标与工作边界

实现观感出色的 Sponza 光栅化 PBR 场景和程序化天空，用于展示空间与时域超分。优先保证材质、日光、阴影和环境反射的观感，无需严格物理真实；复杂度适中，Pass 依赖明确且单向。先完成原生画质，再比较超分，不靠模糊、过曝或额外后期掩盖问题。

本轮采用前向 PBR、单张过滤阴影、程序化天空与预过滤环境镜面、接触 AO、色调映射和现有 SGSR。禁止光追，不增加 CSM/PCSS、实时 GI、SSR、局部反射探针、体积雾、Bloom 或独立 TAA。

| 范围 | 约定 |
| --- | --- |
| 可修改 | 本实验的 Renderer.cs、Passes/、Assets/Shaders/，以及新增且实际需要的渲染 Models/Helpers；可删除 Pass/Shader 占位文件 |
| App 唯一例外 | 仅在现有 ImGuiHelper.Settings 回调末尾追加只读尺寸、模式、GPU 统计；既有控件、默认值、绑定、布局及调用顺序不改 |
| 只读 | App 其余部分、Program、整个 Handlers、已有 CocoaHelper/ImGuiHelper、已有 RenderSettings/UpscalingMode、模型/字体；实验外代码、RHI、后端、共享扩展、工程/依赖/构建配置 |
| 文件交付 | 仓库内只添加项目运行所需的实现文件，不新增工具专属指令、额外文档/README、报告或验证归档。允许在自主选择的仓库外临时目录建立测试项目、探针、脚本及保存必要日志/截图；其源码、配置和输出留在仓库外，不加入仓库项目/解决方案、不复制回仓库。验证结果在对话中说明 |
| 公共路径 | 正式实验的 C# 与 Slang 只用现有公共 RHI/扩展；不按 API、OS、设备或厂商增加算法、格式、资源或 shader 分支/条件编译。沿用 App 初始化和 ZenithCompiler，不另建编译链 |
| 禁止旁路 | 不调用原生图形 API、原生句柄/纹理互操作、后端类型强转、反射或 P/Invoke，不复制后端/扩展来补能力；不新增、升级、替换正式项目的包或项目引用，不以 DLL、第三方源码或外部工具代替现有加载/渲染能力 |

保留以下已完成的宿主契约：

- `RenderSettings` 保持普通 struct，由 Renderer 的 `public RenderSettings Settings;` 持有，App 初始化并直接控制。默认原生基线为 `None + RenderScale=1 + TimeOfDay=12`。
- 保留 Color 的直接绑定，以及 `Update(CameraHandler camera)`、`Render(CommandBuffer commandBuffer)`、`Resize(uint width, uint height)` 的入口和调用位置。不添加空 Color 判断来改变宿主流程。
- 相机只读，未抖动矩阵直接取 `camera.View` / `camera.Projection`。不得写回相机、额外调用 camera.Update、替换位置/朝向/FOV/near/far/速度/初始镜头；Temporal 仅调整 Renderer 自有的 GPU 投影副本。
- 可修改文件也不能接管输入、注册窗口/键鼠事件、轮询输入、改变焦点/鼠标捕获，或增加快捷键、镜头预设、自动漫游/构图、替代控制器。验收使用原始启动镜头和既有 WASDQE/右键拖动，不额外调整模型变换改善构图。

**调用方责任：**实验保证参数、资源/视图、格式、附件、子资源范围和命令顺序合法，不要求框架替错误用法兜底。理解 RHI、后端和扩展的实际实现是开发前置要求；源码阅读用于确认设计与用法，不扩大修改权限，也不将收集、修补框架或高通内部问题另立为实验任务。仓库外验证仍使用现有公共接口和依赖，不另造替代加载/渲染链。

仓库外临时项目可以引用仓库已有工程及既有依赖，并通过现有公共入口选择需要验证的后端；这些引用和测试配置只属于临时项目，不改变正式项目配置，也不将测试分支或验证产物带入实验。Agent 可自行选择临时目录和验证方式，无需逐项询问。

范围内的实现、调参和验证自主推进。分辨率、采样数、曝光、bias、AO 半径/强度是调优起点；可保持算法和数据语义进行调整，最终值留在实现中。新增效果、改变职责/数据语义或扩大范围须由用户决定，不能自行修改本文或根规范扩大授权。实际链路失败先修正自身调用，正确用法仍无法完成时说明具体受阻项并继续独立工作；不静默关闭功能后宣称完成。

## 2. 实现前阅读与代码规范

### 2.1 先理解实现，再编写调用

开始实现前，完整阅读并理解当前仓库的 RHI 设计、三个后端和各扩展的实际实现。先读架构文档，再沿当前源码确认具体行为；不能只读本机后端、几个接口声明，或按类型名和其他引擎的习惯开始写代码。

| 阅读范围 | 必须理解的内容 |
| --- | --- |
| 架构文档与 Zenith.NET 核心 | 资源与视图、描述符/句柄、管线与 shader、命令/队列、同步/提交、上传/读回、查询和生命周期的设计与公共契约 |
| DirectX12、Metal、Vulkan 三个后端 | 上述接口如何实际落地，参数限制、格式/用途/子资源映射、命令作用域、屏障与队列行为、坐标/布局及释放时机；理解差异，不在实验中增加后端分支 |
| Extensions 中各扩展 | ImageSharp、ImGui、Upscaling、Skia 的职责、依赖和调用流程；重点跟踪本实验的纹理加载、UI 绑定、超分 Args/Desc、资源所有权、shader 输入及编译/生成产物 |
| 现有实验与 Sponza 宿主 | 已有公共接口调用方式及本项目 App/Renderer/Handlers 的时序，识别已经完成且必须保留的操作功能 |

对准备使用的 API，在当前源码中确认其存在、签名、参数语义、合法输入、生命周期和后端行为，再实现调用；不要猜方法名、重载、默认值、单位或接口支持的能力。不确定处先查声明、调用点和实际实现，必要时在仓库外做最小验证。开始编写前，在对话中简要说明关键调用链和源码依据，无需另写报告或等待逐项批准。

### 2.2 代码规范与组织

遵循根规范中适用于实验项目的规则，重点为 §6 数据类型、§7 成员组织、§8.3 换行、§10 显式偏好和 §12.4 示例资源释放。阅读后端源码不等于把其专用示例或互操作写法搬入实验，也不授权新增建议配置或全仓整理；规范引用不扩大第 1 节的修改权限。根规范的历史统计仅供参考，具体写法核对当前代码；适用章节优先于总览中的概括。

只强调三项容易误用的约定：

- **优先单行。** 调用、声明和表达式特别长时才换行，续行对齐首参数或对应表达式起始列；不按固定列宽拆行，不用悬挂四空格。Allman 代码块和多字段初始化器保持结构换行。
- **参数是数据。** CPU 参数/输入/输出使用普通 `internal struct`、公开字段和对象初始化器，按值传递；不使用 var、record、in/scoped 参数或属性包装纯数据。“只读”指组装后只读使用。
- **资源有生命周期。** Renderer、Pass 和资源所有者使用 `internal class : IDisposable`，沿用实验的明确释放清单；不套用核心库的资源继承体系。临时 Shader 使用 using 声明。

跨文件数据放 Models，Pass 放 Passes，实际重复的加载/资源创建辅助放 Helpers，Slang 放 Assets/Shaders；单个 Pass 私有 GPU 常量放其 C# 文件末尾的 `file struct XxxConstants`。不引入通用 Pass 基类、接口体系、RenderGraph、资源注册表或依赖注入框架，不把渲染调度藏进 Helper。

## 3. Pass、数据与资源契约

### 3.1 调度与输入输出

Renderer 组装同帧数据，选择模式、决定缓存失效并录制命令。Pass 不持有/调用其他 Pass，不读取 App、活动 Settings 或相机，不保存 CommandBuffer。构造时传入 GraphicsContext 等长期依赖；记录方法以 CommandBuffer 为第一参数，随后是具名 Args，少量简单输入可直接作为参数。

| 入口 | 输入 | 输出及更新时机 |
| --- | --- | --- |
| EnvironmentPass.Record | SkyData | EnvironmentData；首次及天空参数变化时更新，否则返回有效缓存 |
| ShadowPass.Record | SceneData、SkyData | ShadowData；首次及太阳/场景变化时更新，否则返回有效缓存 |
| ScenePass.Record | SceneData、FrameData、SkyData、ShadowData、EnvironmentData | SceneOutput；每帧 |
| AmbientOcclusionPass.Record | HdrColor、IndirectDiffuse、DeviceDepth、FrameData、AO 参数 | 新的 AO 合成 HDR Texture；每帧，无历史 |
| ToneMappingPass.Record | HDR、曝光、借用 LDR 目标及其当前 layout | 写满目标，返回时为 Sampled；不另建输出 |
| UpscalingPass.RecordBilinear / RecordSpatial | LDR、借用目标及其当前 layout | 写入目标；封装双线性/现有空间超分 |
| UpscalingPass.RecordTemporal | AO HDR、Scene depth/motion、FrameData | 返回自有的 D 尺寸 HDR；封装现有时域超分 |

这里仍是六个职责模块，允许模块内部有少量固定步骤。数据流为 Environment/Shadow → Scene → AO → 第 5 节的模式输出；Temporal 另取 Scene 的 depth/motion。

| 数据 | 必需语义 |
| --- | --- |
| FrameData | 当帧相机值、所需未抖动/已抖动矩阵及逆矩阵、上一渲染帧矩阵、R/D、像素单位 jitter、帧序号与 reset；不含相机、设置对象、Pass 或场景资源 |
| SkyData | 从本帧 TimeOfDay 计算的太阳世界方向、线性光色/亮度和天空参数；三个相关 Pass 共用同一份 |
| SceneData | 借用的顶点/索引/材质资源、纹理和绘制记录；记录索引范围、材质索引与世界变换，加载后复用，Pass 不修改 |
| ShadowData / EnvironmentData | 借用的阴影 Texture、光照 ViewProjection、采样参数 / 预过滤环境 Texture；不暴露 Pass 或可变缓存状态 |
| SceneOutput | 四个具名 Texture 字段：HdrColor、IndirectDiffuse、DeviceDepth、EncodedMotion；硬件深度附件只在 Scene 内部使用 |
| XxxPassArgs | 仅组合本次需要的数据与资源；不放 Renderer、其他 Pass、回调、object、字符串资源表或靠下标猜含义的纹理数组 |

字段表达坐标空间、单位和颜色域，如 CameraPositionWorld、JitterInPixels、RadiusInMeters。格式和实际尺寸读取 Texture.Desc，不维护另一套可独立修改的描述信息。每个标量不必另包一层，也不建立可空的“全模式参数包”。

### 3.2 所有权与 GPU 数据

| 所有者 | 自有资源 |
| --- | --- |
| Renderer | 最终 Color、按路径需要的 R/LDR、场景资源对象和六个 Pass |
| 场景资源对象 | 顶点/索引/材质缓冲及去重后的材质纹理，共用纹理只释放一次 |
| Environment / Shadow | 自己的缓存输出和所需管线/常量/采样器 |
| Scene / AO | 自己的目标、中间图、管线/常量/采样器 |
| Upscaling | 扩展实例及 Temporal 的 D/HDR 输出 |
| ToneMapping | 管线/常量/采样器；不拥有传入的输出目标 |

跨 Pass 传 Texture/Buffer 引用，而不是只传裸 ResourceHandle。struct 复制不复制 GPU 资源，也不转移所有权；Args/输出不实现 IDisposable。下游只借用，不修改/释放输入或缓存上游旧绑定，只有显式输出目标可写。Renderer 每帧重新取输出，重建后旧纹理、视图和句柄失效；Record 返回只表示命令已记录，GPU 可见性由顺序和同步保证。

CPU FrameData/Args/资源记录不能整体上传 GPU。各 Pass 转为自己的显式布局 Constants，使用 StructLayout(LayoutKind.Explicit, Size=...)、FieldOffset 对应 Slang，并初始化所有字段和 padding。GPU 布尔标志用 uint，不上传托管引用；ResourceHandle 仅在组装明确布局的 GPU 数据或现有扩展 Args 时提取。

同帧不同 primitive、cube face/mip 或子步骤的常量使用独立缓冲或满足公共 RHI 对齐要求的不同区域，提交完成前不覆盖。单队列 Wait 只允许跨帧复用，不能修复同帧反复覆盖同一常量地址。

### 3.3 尺寸、格式与生命周期

D 是 framebuffer 像素尺寸；R 的宽高分别为 `max(1, floor(D * RenderScale))`。UI 保持逻辑尺寸，相机投影沿用原值，不用 R/D 重建视场。矩阵采用 row-major、行向量，`ViewProjection = View * Projection`，shader 使用 `mul(position, matrix)`；普通 Z，clear=1、LessEqual，不额外转置或按后端翻转。

| 目标 | 尺寸 / 格式与内容 |
| --- | --- |
| Scene.HdrColor / IndirectDiffuse | R / RGBA16F；线性 HDR。IndirectDiffuse 已乘材质系数，且已计入 HdrColor |
| Scene.DeviceDepth / EncodedMotion | R / R32Float、R16G16UNorm；另有硬件深度附件。天空写 depth=1、旋转 motion、IndirectDiffuse=0 |
| AO 中间图 / 合成输出 | max(1, ceil(R/2)) / R16Float；新 R / RGBA16F，禁止原地读写 HDR |
| Environment / Shadow | RGBA16F cube / D32Float 阴影；独立于窗口与 RenderScale |
| Renderer 的 R/LDR / Color | R / RGBA8UNorm 与 D / RGBA8UNorm，均为已编码 LDR；Color 从创建时包含 Sampled/ColorAttachment/Storage 用途 |
| Temporal 输出 | D / RGBA16F 线性 HDR，由 Upscaling 所有 |

格式、用途、附件/采样视图和子资源范围仍须满足实际公共接口用法。保留 ImGuiColorSpace.Legacy 和 UNorm 交换链，只编码一次 sRGB。

- 构造 Renderer 时 Settings 尚未由 App 赋值：先创建 D 尺寸 Color，首个 Update 才依据有效设置建立内部目标/扩展实例。不移动 App 的绑定或 Update。
- Update 复制 Settings/相机并形成当帧快照；完整记录一帧后才推进帧序号、jitter 和上一帧矩阵。未渲染/最小化时不推进；只有现有 Temporal 扩展保存重建历史。
- Scale/模式变化按需重建内部目标和 upscaler，Color 只随 D 改变。Resize/Dispose 不提前释放 UI/GPU 仍引用的资源，先释放依赖视图再释放纹理；环境/阴影仅因相关输入变化而失效。
- 采样输入交接为 Sampled。自有目标由所有者跟踪真实 layout；写借用目标时显式传入当前 layout，生产者写完恢复 Sampled。新资源从 Undefined 开始，不能假报已初始化。
- 保留 App 的单队列 Submit().Wait()；Pass 不自行提交/等待或释放借用 CommandBuffer，已有加载扩展沿用其流程。Transition/Barrier 依据实际前后命令及读写依赖放置，处理逐 mip/layer，不机械使用 All 或为无后续依赖的尾部添加屏障。
- 资源创建集中在初始化、Resize 或失效处理，稳定帧不反复创建纹理/管线、复制场景数组；释放清单复用明确的私有方法，不另建资源池框架。

## 4. 场景与画质方案

### 4.1 当前资产

从 `Path.Combine(AppContext.BaseDirectory, "Assets", "Models", "Sponza.gltf")` 加载，模型/accessor 只走现有 SharpGLTF.Core。图像只调用现有 Zenith.NET.Extensions.ImageSharp 的 LoadTextureFromFile / LoadTextureFromStream；不直接使用 SixLabors.ImageSharp，不另写解析器、解码器、预转换或替代加载链。

| 资产事实 | 实现要求 |
| --- | --- |
| 103 primitives、25 材质、65 图像 | 按 glTF 引用和相对 URI 加载、缓存复用；数量不写死，不扫描目录或按文件名猜映射 |
| 根 scale 约 0.008，世界尺寸约 30×12×18 m | 节点变换只应用一次，阴影和光照使用变换后的世界坐标 |
| 输入索引 UInt16 | 保留 primitive/材质对应关系；合并缓冲正确处理局部索引或 baseVertex，通过 accessor 读取，不假设紧密排列 |
| NORMAL/TANGENT/UV | 保留原数据和 tangent.w；plain_white 无法线贴图且缺切线，可直接用几何法线 |
| MASK | ivy_leaves、flowers_and_leaves、hanging_chain 双面、cutoff=0.5；其余 OPAQUE，无 BLEND；颜色和阴影共用裁剪 |
| 材质因子 | 保留约 (0.588,0.588,0.588,1) 的 baseColorFactor；plain_white metallicFactor=0，其余缺项按 glTF 默认值；无 AO/emissive 贴图，不猜通道 |
| 纹理语义 | base color RGB 由 sRGB 解码为线性，alpha 不转换；normal/MR 为线性，roughness=G、metallic=B。现有加载器返回 RGBA8UNorm，shader 做颜色解码，接受现有过滤/mip 近似，不改加载器或重建材质 mip |

### 4.2 六个模块的实现

**Scene：前向 PBR 与天空。** 使用 GGX、Smith visibility、Schlick Fresnel 的 metallic-roughness 模型；roughness 与 GGX 参数平方关系一致，保留小数值下限。正确处理因子、TBN、双面法线和法线归一化，让石材、布料、金属可区分。材质采样状态三模式一致。天空直接调用共享程序化函数。

**Shadow：稳定日光阴影。** 单张 4096² 正交深度图按世界 AABB 拟合并留边界，固定光照时不跟随相机；固定 5×5 PCF 和少量 slope/normal bias，处理太阳与参考 up 平行的退化。片段着色器必填：OPAQUE 可为空实现且无颜色输出，MASK 保留共享 alpha 裁剪；双面材质一致，不追求距离相关半影。

**Environment：程序化天空和环境光。** 共享天空函数包含天顶/地平线、近地薄霾、太阳盘与光晕；TimeOfDay=6..18 统一控制太阳方向、光色和亮度，无云动画/大气散射系统。对同一函数做 GGX 重要性采样，生成 128²、完整 roughness mip 的 HDR cube：mip 0 直接求值，其余以 128 个确定性样本起步，`roughness = mip / (mipCount - 1)`；预过滤排除锐利太阳盘，各 mip 独立生成。使用合法的公共资源/附件/视图操作，同帧完成更新，不因相机移动重建。

环境漫反射采用天空/地面半球近似，镜面采用预过滤 cube 与解析环境 BRDF；不增加 irradiance cube、BRDF LUT 或局部探针。室内缺少真实间接遮挡和局部反射，环境强度保持克制。解析近似可参考 [Epic 的环境 BRDF 说明](https://www.unrealengine.com/blog/physically-based-shading-on-mobile)，不引入其代码或依赖。

**AO：接触层次。** 半 R、12 个确定性样本的 SSAO，半径从约 0.3 m 起调。以当前含 jitter 的逆投影从 device depth 重建 view position，按深度连续的单侧差分重建几何法线，统一叉乘朝向；不增加 normal MRT。内部固定为求解、一次深度感知滤波、合成；包含边界检查和距离衰减，背景 AO=1，合成按全分辨率深度引导上采样。仅衰减间接漫反射：`result = HDR - IndirectDiffuse * (1 - AO)`，不压黑直射、镜面和天空。不增加深度金字塔、几何预通道或时域去噪。

**ToneMapping：统一输出。** 线性 HDR 乘固定曝光，使用 ACES fitted，最后一次 sRGB 编码。先调太阳/环境比例，再调曝光。**Upscaling** 仅封装第 5 节的现有扩展与双线性输出，不复制算法。

## 5. 三模式与时域输入

| 模式 | 完整输出路径 |
| --- | --- |
| None | AO HDR → ToneMapping；R=D 时直写 Color，否则写 R/LDR → 双线性 → Color |
| Spatial | AO HDR → ToneMapping 的 R/LDR → SGSR 1 → Color；scale=1 仍走 Spatial |
| Temporal | AO HDR + Scene depth/motion → SGSR 2 Quality 的 D/HDR → ToneMapping → Color |

使用现有 CreateSpatialUpscaler / CreateTemporalUpscaler，Desc 尺寸改变时重建。SGSR 1 接收 LDR，SGSR 2 接收 HDR。None/Spatial 不加 jitter 或另一套 AA，Temporal 不叠加 TAA；三模式固定相同材质、曝光、环境、阴影质量及 AO 世界半径/样本数。AO 工作尺寸随 R 改变。

| Temporal 字段 | 调用方提供的数据 |
| --- | --- |
| Input / OpaqueInput | 同一张 AO 合成后的线性 HDR，无额外复制 |
| Depth / MotionVectors | R 尺寸普通 device depth（near=0、far=1）/ 下述编码 motion；不传米深度或原始 UV velocity |
| JitterOffsetX/Y | 当前帧输入像素单位偏移，与 GPU 投影一致 |
| PreExposure / CameraFovAngleHor | 1 / `tan(fovYRadians / 2) * projectionAspect`，后者是水平半视角正切，aspect 来自实际基准投影，不传角度 |
| MinLerpContribution / SameCamera | 显式 0 / 按未 jitter 相机是否静止填写；不作为修复算法的调节项 |
| Reset | 首帧、尺寸/模式变化、检测到相机数据不连续或时间大幅变化为 true；普通连续移动不重置 |

Scene 直接输出 `motion = currentUnjitteredNdc.xy - previousUnjitteredNdc.xy`，再编码为 `motion * 0.2495 + 32767.0 / 65535.0`，保存到 R16G16UNorm。静止值约 0.5，天空只含旋转 motion；超出可编码范围时显式使用零编码的矩阵回退，不能依赖 UNorm 饱和。重投影应满足 `previousUv = currentUv + (-0.5 * motion.x, 0.5 * motion.y)`，精度检查允许 UNorm 量化误差。

使用 Halton(2,3) 的 8 帧序列减 0.5，输入像素 jitter=(jx,jy)，NDC 偏移为 `(2*jx/Rw, -2*jy/Rh)`。需要矩阵回退时，C/P 为当前/上一帧未 jitter 的 ViewProjection，J 为当前 clip jitter，`ClipToPrevClip = inverse(C * J) * (P * J)`。首次/历史无效时使用有效的当前矩阵初始化前帧基准；帧推进遵循第 3 节。切回 None/Spatial 使用原始投影副本。

这些是接入数据约定，不是修改高通实现的要求。实际异常先检查输入、资源和前后命令，不增加补丁 Pass、替代历史系统或额外算法。

## 6. 开发顺序与完成标准

| 阶段 | 目标与通过条件 |
| --- | --- |
| 0. 理解实现 | 阅读两份文档并完成第 2.1 节的 RHI/三后端/各扩展源码理解，核对骨架、资产和起始工作区；确认实际 API 契约、调用链、可写文件与数据所有者，必要验证仅在仓库外进行 |
| 1. 原生材质 | 创建有效 Color，完成资产、Scene、ToneMapping 和基础光照；默认镜头下几何、颜色、因子、法线、MASK 正确 |
| 2. 原生光照 | 加入 Shadow、天空、Environment 并调 PBR；同镜头验证材质区别、稳定阴影和反射、时间变化 |
| 3. 原生画质 | 完成 AO 和整体调参，通过下表检查；冻结超分对比使用的画质参数 |
| 4. 超分 | None 双线性 → SGSR 1 → SGSR 2 Quality；尺寸、颜色、静态细节、运动和新显露区域正确，不改变镜头/材质迎合模式 |
| 5. 展示与回归 | 追加真实只读统计，验证耗时、Resize/生命周期和已有操作；检查完整 diff，在对话中交付结果 |

默认原生基线必须通过以下画质检查，才进入正式超分对比：

| 观察对象 | 通过条件 |
| --- | --- |
| 启动画面 | 几何、朝向和比例正确；受光区保留纹理，阴影区保留结构，无整体发灰、过曝或死黑 |
| 石材、布料、金属 | 原始颜色、纹理清楚，粗糙度和高光可区分，不全部像塑料 |
| 叶片、链条 | 镂空、双面和颜色/阴影轮廓一致 |
| 柱脚、墙角 | 阴影无明显条纹或悬浮，AO 无黑晕，不压黑直射和镜面 |
| 天空、环境镜面 | 与太阳方向/光色一致，无 cube 接缝、粗糙表面过亮或运动时明显闪烁 |

排错顺序为资产/变换 → 颜色空间/材质 → 法线/PBR → 直射/阴影 → 环境 → 曝光 → AO → 超分。临时诊断输出在确认后恢复，正式项目不残留调试文件/代码或新增控件；独立验证材料只放仓库外。None 的边缘锯齿属于对照性质，不因此跳过材质/光照验收或加入另一套 AA。

- 对比 None/scale=1 与 None、Spatial、Temporal/scale=0.5、0.75，并检查三模式的 scale=1。固定镜头和光照，观察栏杆/细柱、叶片、布料、墙角及不同粗糙度表面；原生图不是无锯齿的像素真值。
- 静态比较等待 Temporal 至少稳定 32 帧；用已有操作检查慢移、快速转头、停止收敛，覆盖奇数尺寸、连续 Resize、最小化恢复及时间切换。结合实际画面检查矩阵、motion、AO 和缓存，实验自身代码不引入越界、NaN、泄漏或失效句柄。
- GPU 计时预热至少 60 帧、统计至少 120 帧平均值。在同一队列、逻辑 Pass 前后且 render pass 外写 timestamp，完成后读取并用 GetElapsedNanoseconds 换算；排除 ImGui/等待，缓存更新成本单独说明，不用 VSync 限制的 FPS 代替。只读统计对应真实已完成帧。
- 每阶段自主构建、运行、查看画面并修正，无需逐阶段批准。检查相对起点的完整 diff（含暂存/新增文件）及操作行为，只撤销自己越界的修改，不覆盖用户已有改动；规范检查不扩展为全仓格式化或修复范围外诊断。

从仓库根目录执行：

```sh
dotnet build sources/Experiments/Sponza/Sponza.csproj -c Release
dotnet run --project sources/Experiments/Sponza/Sponza.csproj -c Release --no-build
```

完成后只在对话中说明改动、运行验证、实测耗时、已知限制及未验证平台，区分已完成、未验证和受阻。无法运行/查看画面时不能宣称画质通过；编译成功或 UI 选项存在不等于完成。用同一实现按可用设备验证目标平台，不以其他平台未测为理由增加分支或假报通过。
