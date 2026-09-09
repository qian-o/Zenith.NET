# Sponza 渲染与超分实验开发文档

**v3.0 封板规格（2026-09-09），替代 v2.0。** 技术路线、Pass 职责与数据契约已确定，实施基点为 `9eb1e18`。开始前完整阅读本文与仓库根目录的 [Zenith.NET 代码风格与规范](</Users/wangxi/Documents/GitHub/Zenith.NET/Zenith.NET 代码风格与规范.md>)，并完成第 2 节的源码阅读。本文规定项目设计与验收，根规范规定代码写法；封板不代表新管线已实现或画质、跨后端运行已经通过。

**启动条件：等待用户另行通知开始重构。** 文档封板不自动授权启动 Renderer、Pass、Shader 或材质代码改写。收到启动通知后按第 7 节自主实施和验证；封板后的算法、职责或范围变化仍须先说明，不能隐式替代。第 6 节保留真实验证范围与未验证项，不将未验证误写成阻挡或已完成。

## 1. 目标、质量与工作边界

实现完整、高质量、观感真实且无渲染错误的 Sponza 光栅化管线，保留空间和时域超分。日光方向、建筑阴影、天空、材质、间接光、反射与体积光必须一致；后期服务于正确光照，不能掩盖问题。旧 Renderer、Pass、Shader 不构成保留约束，可在白名单内重写、重组和删除。

采用延迟 PBR、LUT 天空大气、PCSS 太阳阴影、光栅化探针的一次漫反射反弹 GI、局部探针与 SSR 混合反射、GTAO、分信号降噪、体积光、自动曝光、柔和 Bloom、色调映射和现有 SGSR。场景可见性由光栅化产生；SSR 仅遍历屏幕深度，体积光仅积分介质。不使用硬件光追、RayQuery、场景 BVH/几何求交或其他场景光追路径；不增加独立全屏 TAA、CSM、体积云或范围外效果。

**质量是完成门槛。** 目标为验收范围内零已知未解决的渲染错误，包括漏光、错位反射、重复计光、阴影错误、接缝、黑晕、闪烁、拖影、曝光跳变和非法 GPU 行为。发现问题必须修复并复验，不能以能运行、已编译、存在 Pass 或性能不足宣布完成。不能通过关闭效果、压低贡献、扩大模糊、过曝、裁掉问题视角或只挑截图通过验收。

一次反弹、有限探针密度、屏幕空间可见范围与分帧收敛是公开的技术边界，不是渲染错误的免责条款。若这些边界在目标场景中造成无法消除的可见问题，必须说明具体场景与能力差距并提交用户讨论；不能擅自放宽标准或假报完美。未运行的平台与未覆盖场景明确列为未验证。

| 范围 | 约定 |
| --- | --- |
| 可修改 | 本实验 Renderer.cs、Passes/、Assets/Shaders/、渲染所需 Models/Helpers；可重写、重组、删除旧渲染实现。RenderSettings/UpscalingMode 与宿主辅助文件仍只读 |
| 只读 | App.cs 全部、Program、整个 Handlers、已有 CocoaHelper/ImGuiHelper、已有 RenderSettings/UpscalingMode、模型/字体；实验外代码、RHI、后端、共享扩展、工程/依赖/构建配置 |
| 文档 | 仅按用户确认的方案更新本 DEVELOPMENT.md；根规范只读，不新增额外文档、README、报告、Evidence 或验证归档 |
| 文件交付 | 正式仓库只保留项目实现与本规格。临时项目、探针、脚本、配置、日志、截图放仓库外，不加入正式项目/解决方案，不复制回仓库 |
| 公共路径 | 正式 C# 与 Slang 只用现有公共 RHI/扩展；不按 API、OS、设备或厂商增加算法、格式、资源或 shader 分支/条件编译。沿用 App 初始化和 ZenithCompiler |
| 禁止旁路 | 不调用原生图形 API、原生句柄互操作、后端强转、反射或 P/Invoke，不复制后端/扩展补能力，不新增/升级/替换正式依赖，不以第三方源码或外部工具替代现有加载/渲染能力 |

用户另行授权修复两处后端问题：Vulkan GetAttachmentView 的单面视图类型，以及 DirectX12 的 3D 非零 mip UAV 深度范围。该授权仅限这两处，不扩大渲染项目对其他 RHI/后端/扩展的修改权限；第 6 节区分源码修复、构建与实际 GPU 验证状态。

ImageSharp 的颜色语义支持已完成，并由用户统一为 `compand=true, generateMipMaps=true` 的接口与默认值。重构按当前接口接入，不恢复旧重载或默认值。Sponza 的材质绑定、颜色解码与按颜色语义缓存纹理在重构第一步成套迁移；封板阶段不修改材质代码，不增加依赖。

保留以下宿主契约：

- `RenderSettings` 保持现有普通 struct 和三个字段，由 Renderer 的 `public RenderSettings Settings;` 持有，App 初始化并直接控制。默认 `None + RenderScale=1 + TimeOfDay=12`。新增效果参数由 Renderer 组装具名 Args，不增加设置控件或统计 UI。
- 保留 Color 直接绑定及 `Update(CameraHandler camera)`、`Render(CommandBuffer commandBuffer)`、`Resize(uint width, uint height)` 的入口和调用位置。不加空 Color 判断改变宿主流程。
- 相机只读，基准矩阵直接来自 `camera.View` / `camera.Projection`；不得写回、额外调用 camera.Update、替换位置/朝向/FOV/near/far/速度/初始镜头。Temporal 仅调整 Renderer 自有 GPU 投影副本。
- 不接管输入，不注册窗口/键鼠事件、轮询输入、改变焦点/鼠标捕获，不增加快捷键、镜头预设、自动漫游或替代控制器。使用原始启动镜头与既有 WASDQE/右键拖动验收，不修改模型变换改善构图。

**遇到能力问题先公开，不隐式替代。** 先保证自身参数、格式、用途、子资源、布局和命令顺序合法。发现 RHI/扩展不能表达需求、后端映射不符合契约或正确调用仍失败，应在对话中说明触发条件、源码/运行证据、影响与候选处理，等待用户决定受影响路径；可继续独立工作。不得自行加入额外复制、改资源类型/格式、换算法、降质量、关闭功能或修改共享层来绕过。普通合法调用与已确认算法内的正常分支不需要逐项批准。

现有超分扩展对齐官方实现，SGSR 边缘偏色不在处理范围；不修改高通算法、不加修补/防御或替代历史系统。GPU 计时问题留在单独任务，不在本项目调查或修复，不添加伪工作、修饰结果或用 FPS 冒充 GPU 耗时。

仓库外验证可以引用已有工程及依赖，通过公共入口选择后端，不能建立替代正式执行能力。范围内调参以质量通过为前提；改变算法、职责、数据语义或范围须先向用户说明并取得决定。

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

Renderer 组装同帧数据、选择模式、决定缓存/历史失效并按依赖录制命令。Pass 不持有/调用其他 Pass，不读取 App、活动 Settings 或相机，不保存 CommandBuffer。构造传入 GraphicsContext 等长期依赖；记录方法以 CommandBuffer 为第一参数，随后为具名 Args。效果内部允许固定的几个阶段，不为每次 Dispatch 建类。

| 模块 | 具名输入 | 具名输出与更新时机 |
| --- | --- | --- |
| AtmospherePass | SkyData、大气参数、具名 ObserverPositionWorld | AtmosphereData：观察点无关的基础 LUT；MainSkyViewData / ProbeSkyViewData：分别对应主相机/当前探针的天空视图；按各自依赖更新 |
| ShadowPass | SceneData、SkyData | ShadowData：深度、光照矩阵及采样参数；太阳/场景变化 |
| ProbeCapturePass | SceneData、SkyData、AtmosphereData、ProbeSkyViewData、ShadowData、探针位置/面及捕获 near/far | ProbeCaptureOutput：DiffuseRadiance、SpecularRadiance、RadialDistance；按冻结的光照批次直接光栅化捕获 |
| ProbeFilterPass | ProbeCaptureOutput、探针槽位、缓存版本 | ProbeData：辐照度、镜面 mip、距离矩；捕获后更新；另拥有初始化一次的 BRDF LUT |
| GBufferPass | SceneData、FrameData | GBufferOutput：五个当前资源；HistoryGeometryData：上一渲染帧的几何引导资源；每帧 R |
| ScreenPyramidPass | DeviceDepth / 反射合成前 HdrColor | DepthHierarchy / RadiancePyramid；每帧按依赖分两次调用 |
| AmbientOcclusionPass | DeviceDepth、SurfaceNormal、FrameData、AO 参数 | AmbientVisibility；每帧半 R，求解与半 R 保边滤波，不生成全 R 输出 |
| DiffuseGiPass | GBufferOutput、HistoryGeometryData、ProbeData、FrameData、GI 参数 | IndirectIrradiance；每帧半 R，短历史稳定与半 R 保边滤波，不含接收材质或 AO，不生成全 R 输出 |
| LightingPass | GBufferOutput、FrameData、SkyData、AtmosphereData、MainSkyViewData、ShadowData、ProbeData、IndirectIrradiance、AmbientVisibility、与上游一致的 GI/AO 参数、借用 HDR 目标 | 在 R 完成 GI/AO 保边重建、无兼容样本时的当前像素直接求值、材质响应和光照合成；写出基础 HdrColor，自有独立 ProbeSpecular |
| ReflectionPass | 基础 HdrColor、ProbeSpecular、BrdfIntegration、GBufferOutput、HistoryGeometryData、DepthHierarchy、RadiancePyramid、FrameData、借用 HDR 目标 | SSR 求解、专用降噪及镜面替换后的 HDR；每帧 |
| VolumetricPass | FrameData、DeviceDepth、SkyData、AtmosphereData、ShadowData、雾参数、表面 HDR、借用 HDR 目标 | 自有体积散射/透射率与历史，写出雾合成 HDR；每帧 |
| ExposurePass | 雾合成 HDR、DeviceDepth、实际帧间隔、测光参数 | Exposure：GPU 内 1×1 曝光状态；每帧 |
| BloomPass | 当前模式的 HDR、Exposure、显示尺寸、Bloom 参数 | Bloom HDR；每帧，尺度按 D 定义 |
| ToneMappingPass | HDR、Bloom、Exposure、借用 LDR 目标及其 layout | 写满目标并恢复 Sampled，不拥有目标 |
| UpscalingPass | 第 5 节 HDR/LDR、深度、motion、FrameData、具名目标 | Temporal 自有 D/HDR；Spatial/双线性写借用 Color |

主线为 Atmosphere 基础 LUT/Shadow → 对应观察点的天空视图 → 探针缓存；GBuffer → 深度层级/GTAO/GI → Lighting → 辐射颜色层级 → Reflection → Volumetric → Exposure → 第 5 节输出。主相机天空视图与当前探针天空视图分别传递，不能混用。天空在 Lighting 按背景深度求值，GBuffer 提供背景 depth=1 和旋转 motion，不为天空伪造表面法线或 GI。

| 数据 | 必需语义 |
| --- | --- |
| FrameData | 所需当前/上一渲染帧相机值、未抖动/已抖动矩阵及逆矩阵、R/D、像素 jitter、帧序号、实际帧间隔和历史有效性；不含相机/Settings/Pass 对象 |
| SkyData | TimeOfDay 对应的表面指向太阳的世界方向、SunAngularRadiusInRadians、SunIrradianceOutsideAtmosphere；后者为垂直太阳方向平面上的大气外线性相对辐照度，未乘透射率，所有相关 Pass 共用 |
| AtmosphereData | 借用 Transmittance 与单位太阳辐照度下的 MultiScattering 响应 LUT、大气参数和版本；不包含某个观察点的天空视图 |
| SkyViewData | 借用天空散射辐亮度 LUT、对应 ObserverPositionWorld 及高度/当地向上方向等采样映射参数；MainSkyViewData 与 ProbeSkyViewData 为两个具名实例，不能互换 |
| SceneData | 借用的顶点/索引/材质资源、纹理/视图、绘制记录，含索引范围、材质索引与世界变换；加载后复用 |
| GBufferOutput | 五个具名资源；法线为世界空间，SurfaceNormal 为法线贴图扰动前的表面法线，Depth 为普通 device depth |
| HistoryGeometryData | 借用上一渲染帧 DeviceDepth、SurfaceNormal、ShadingNormalRoughness，由 GBufferPass 所有，Renderer 每帧显式交给 GI/SSR；有效性由 FrameData 提供，帧末才交换，不能缓存旧绑定或隐式复制 |
| ProbeCaptureOutput | 借用当前探针三个单 mip Cube：DiffuseRadiance、SpecularRadiance、RadialDistance；含 ProbePositionWorld、CaptureNearInMeters、CaptureFarInMeters 和捕获版本，语义见第 4.3 节 |
| ProbeData | 具名 OldIrradiance / NewIrradiance、OldSpecular / NewSpecular、共享 DistanceMoments、BrdfIntegration、探针元数据及 Blend / OldVersion / NewVersion；只借出完整且可采样的缓存，不暴露构建中的备用组或可变所有者 |
| XxxPassArgs / Output | 只含该次所需数据/资源；不放 Renderer、其他 Pass、回调、object、字符串资源表或靠下标猜职责的纹理集合 |

字段表达坐标空间、单位与颜色域，如 CameraPositionWorld、JitterInPixels、RadiusInMeters。资源格式/尺寸来自 Texture.Desc，不维护第二套可独立修改的描述。辐照度、已乘材质的辐亮度和最终 HDR 不混用。

### 3.2 所有权与 GPU 布局

Renderer 拥有 Color、按模式需要的 R/LDR、两张明确的 R/HDR 周转目标、场景资源和各 Pass。场景资源对象拥有顶点/索引/材质缓冲与去重纹理/视图，共用资源只释放一次。GBufferPass 拥有当前 GBuffer 和历史拒绝实际需要的上一帧深度/法线等资源。各效果分别拥有自身管线、常量、采样器、中间图与历史；ProbeCapturePass 拥有捕获资源，ProbeFilterPass 拥有两组过滤缓存、距离矩和 BRDF LUT；UpscalingPass 拥有扩展实例与 D/HDR。

AtmospherePass 拥有基础 LUT、一张 MainSkyView 和一张复用的 ProbeSkyView；两张天空视图使用同一生成算法，Renderer 分别调度并传递具名输出。ProbeCapturePass 拥有三张复用的捕获 Cube 及一张仅用于当前面深度测试的 2D D32 附件；颜色和径向距离直接写 Cube 面，不经额外 2D 颜色中转。ProbeFilterPass 消费当前捕获并写入指定缓存槽位后，捕获资源才可按 GPU 命令依赖顺序复用。

HDR 周转固定为 Lighting 写 A，Reflection 读 A 写 B，Volumetric 读 B 写 A。Exposure 从 A 测光，Temporal 的 Input=A、OpaqueInput=B；A/B 保留至该次超分读取完成，不为 OpaqueInput 新增整图复制。曝光只在最终 ToneMapping 应用。

跨 Pass 传 Texture/Buffer 或实际需要的具名 TextureView 借用引用，不只传裸 ResourceHandle。struct 复制不复制资源、不转移所有权；Args/Output 不实现 IDisposable。下游不释放输入、不保留上游旧绑定，只有显式输出目标可写；同一子资源禁止原地读写合成。每帧重新取得输出，重建后旧视图/句柄失效。

CPU FrameData/Args 不能整体上传 GPU。每个 Pass 使用显式布局的 `file struct XxxConstants`，StructLayout/FieldOffset 对应 Slang，初始化所有字段/padding，GPU 布尔用 uint。ResourceHandle 仅在 GPU 布局或现有扩展 Args 组装时提取。禁止通过嵌套 ConstantBuffer 句柄解引用组织 Scene 常量。

`SetConstantBuffer(Buffer buffer, uint offsetInBytes)` 第二参数是字节偏移，不是绑定槽；三后端使用一个根常量入口。同帧 primitive、probe face、mip 和步骤使用独立缓冲或 256 字节对齐的不同区域，通过公共入口绑定偏移；不覆盖 GPU 尚需读取的数据。不能把大型缓冲默认 ConstantHandle 当作任意大小 CBV，须核对实际绑定范围；跨帧 Wait 不能补救同帧覆盖。

### 3.3 尺寸、格式与颜色域

D 为 framebuffer 像素尺寸；R 各维为 `max(1, floor(D * RenderScale))`，半 R 为 `max(1, ceil(R/2))`。UI 保持逻辑尺寸，不随 R/D 重建相机投影。矩阵 row-major、行向量、`ViewProjection = View * Projection`、`mul(position, matrix)`；普通 Z，clear=1、LessEqual，不额外转置或按后端翻转。

以下为已选方案起点，具体组合仍须通过第 6 节验证；枚举存在不等于设备已运行通过，失败不能默默换格式。

| 资源 | 尺寸 / 格式 | 数据约束 |
| --- | --- | --- |
| AlbedoMetallic | R / R8G8B8A8SRgb | 线性 RGB 写入，附件编码、采样解码；A 为线性 UNorm 金属度，不重复编码 |
| ShadingNormalRoughness | R / RGBA16Float | 世界空间着色法线 xyz、感知粗糙度 w |
| SurfaceNormal | R / R16G16SNorm | 八面体编码的未扰动世界空间表面法线 |
| DeviceDepth / EncodedMotion | R / D32Float、R16G16UNorm | 硬件深度直接采样；天空 depth=1、旋转 motion。不再建 D32→R32 兼容视图或重复深度颜色附件 |
| DepthHierarchy | R 起始 / R32Float mip | 普通 device Z，每级取覆盖区域最小值；保留奇数边缘，不当作米距离 |
| HdrColor / ProbeSpecular / RadiancePyramid | R 起始 / RGBA16Float | 未曝光辐亮度；颜色层级来自 SSR 合成前 HDR |
| AmbientVisibility / IndirectIrradiance | 半 R / R16Float、RGBA16Float | AO 可见度；GI 为接收材质应用前辐照度，均在 Lighting 中完成最终半 R→R 引导采样 |
| Atmosphere 基础 LUT | 256×64、32² / RGBA16Float | 透射率与单位太阳辐照度下的多次散射响应，独立于观察位置和当前太阳方向/强度 |
| MainSkyView / ProbeSkyView | 各 192×108 / RGBA16Float | 对应观察点的天空散射辐亮度；已应用太阳源强度与大气积分，不含锐利太阳盘 |
| ProbeCaptureOutput 三个颜色附件 | 每面 128²，单 mip / RGBA16Float、RGBA16Float、R32Float | 依次为 DiffuseRadiance、SpecularRadiance、RadialDistance，均直接写 Cube 面；同尺寸单张 2D D32 仅供当前面的硬件深度测试 |
| Probe irradiance / specular / moments | 每面 32² / RGBA16Float；128² 起始完整 mip / RGBA16Float；64² / RG32Float | 径向世界距离及平方的矩；探针槽位、面和 mip 明确 |
| BRDF LUT | 256² / RG16Float | 环境镜面积分，初始化一次 |
| 体积网格 | ceil(Dw/12)×ceil(Dh/12)×48 / RGBA16Float，单 mip | 对数深度；注入/历史存散射源与消光，积分图存散射 RGB 与透射率 |
| Exposure / Bloom | 1×1 / R32Float；四分之一 D 起始 / RGBA16Float 多级 | 曝光留在 GPU；Bloom 保持 HDR，显示空间半径不随 RenderScale 改变 |
| R/LDR / Color / Temporal HDR | R / RGBA8UNorm；D / RGBA8UNorm；D / RGBA16Float | LDR 已编码；Color 初始含 Sampled/ColorAttachment/Storage 用途 |

四张 GBuffer 颜色附件的尺寸、SampleCount.Count1、Shader 输出位置一一对应，仅声明实际用途。项目自身 HDR 及效果历史使用统一未曝光线性域，光源尺度一致，检查 FP16 范围及 NaN/Inf。SGSR 内部历史遵守官方实现，不声称其内部也保存场景 HDR。曝光只在 ToneMapping 应用一次，SGSR PreExposure=1；保留 ImGuiColorSpace.Legacy 与 UNorm 交换链，最终只编码一次 sRGB。

### 3.4 生命周期、历史与同步

- Renderer 构造时 Settings 尚未赋值：先创建 D/Color，首个 Update 再依据有效快照建立内部目标和扩展；不移动宿主绑定/调用。
- 首帧消费前依次生成大气基础 LUT、BRDF LUT、太阳阴影及完整探针缓存；每个探针先生成对应 ProbeSkyView，再捕获六面并过滤。MainSkyView 在主视图光照消费前有效。曝光第一帧直接初始化为当前测光目标，后续才做时间适应；无效 GI/SSR/体积历史不得读取未初始化内容。
- Update 复制 Settings/相机；完整记录渲染帧后才推进帧序号、jitter、前帧矩阵与效果历史。未渲染/最小化不推进；恢复时处理真实时间间隔与历史有效性。
- Scale/模式变化按需重建相关尺寸资源/扩展并重置历史；Color 只随 D 改变。Resize 不使静态探针、阴影和大气基础 LUT 无故失效。
- 相机数据不连续、显露、深度/法线/粗糙度不匹配时拒绝相关历史。光照/探针版本变化降低或重置对应历史；曝光不因普通模式/Scale 切换重置。
- GBuffer 历史在本帧使用完成后交换；GI、SSR、体积分别维护信号与历史权重，不能让 SGSR 代替。体积按世界位置重投影，不使用表面 motion。Temporal 下公共 HDR 输出仍落在当前 jittered R 网格，不能先消抖再交 SGSR 重复去 jitter。
- 新纹理从 Undefined 开始，采样交接为 Sampled；所有者跟踪真实 layout，借用写目标显式传入 layout，写完恢复 Sampled。逐 mip/layer 范围合法，不能假报已初始化。
- 保留 App 单队列 Submit().Wait()；Pass 不提交/等待，不释放借用 CommandBuffer；加载扩展沿用原流程。Transition/Barrier 依真实 producer/consumer 放置，不能机械用 All，也不能假设 Transition 在每个后端都提供全部依赖。
- Resize/Dispose 在 UI/GPU 不再引用后释放，先视图后纹理；稳定帧不创建纹理/管线、不复制场景数组，不增加通用资源池或防御异常体系。

## 4. 场景、光照与效果实现

### 4.1 当前资产与材质读取

从 `Path.Combine(AppContext.BaseDirectory, "Assets", "Models", "Sponza.gltf")` 加载，模型/accessor 只走现有 SharpGLTF.Core。图像只调用现有 Zenith.NET.Extensions.ImageSharp 的 LoadTextureFromFile / LoadTextureFromStream；不直接使用 SixLabors.ImageSharp，不另写解析器、解码器、预转换或替代加载链。

| 资产事实 | 实现要求 |
| --- | --- |
| 103 primitives、25 材质、65 图像 | 按 glTF 引用和相对 URI 加载、缓存复用；数量不写死，不扫描目录或按文件名猜映射 |
| 根 scale 约 0.008，世界尺寸约 30×12×18 m | 节点变换只应用一次，阴影和光照使用变换后的世界坐标 |
| 输入索引 UInt16 | 保留 primitive/材质对应关系；合并缓冲正确处理局部索引或 baseVertex，通过 accessor 读取，不假设紧密排列 |
| NORMAL/TANGENT/UV | 保留原数据和 tangent.w；plain_white 无法线贴图且缺切线，可直接用几何法线 |
| MASK | ivy_leaves、flowers_and_leaves、hanging_chain 双面、cutoff=0.5；其余 OPAQUE，无 BLEND；颜色和阴影共用裁剪 |
| 材质因子 | 保留约 (0.588,0.588,0.588,1) 的 baseColorFactor；plain_white metallicFactor=0，其余缺项按 glTF 默认值；无 AO/emissive 贴图，不猜通道 |
| 纹理语义 | 基础色 RGB 由硬件 sRGB 采样解码，alpha 不转换；normal/MR 使用线性 UNorm，roughness=G、metallic=B。当前 SceneResources 尚未区分用途，默认 compand=true 会把数据纹理也当作 sRGB，基础色 Shader 仍再次解码；必须先完成下述迁移才能建立原生画质基线 |

当前公共签名为 `LoadTextureFromStream(Stream stream, bool compand = true, bool generateMipMaps = true)` 与 `LoadTextureFromFile(string file, bool compand = true, bool generateMipMaps = true)`。compand 同时决定 R8G8B8A8SRgb / R8G8B8A8UNorm 格式及 mip 的 gamma 正确处理；基础层上传原始编码字节，alpha 处理、重采样器与上传流程不变，传入 Stream 仍由调用方所有。stream/file 后的第一个参数是 compand，旧两参数成员已删除，不沿用旧位置参数或 srgb 参数名；需要关闭 mip 时明确传 generateMipMaps=false。

重构第一步按 glTF 通道用途明确传参：基础色 compand=true，法线/MR compand=false，通常均保留 generateMipMaps=true；同时删除基础色 Shader 的重复 DecodeSrgb，并保留线性材质因子与 alpha。缓存键采用图像引用与 compand 语义，同一图像兼作颜色和数据时分别生成相应纹理/mip 链；不能只迁移绑定或 Shader 的一半，也不能仅用两个 view 共用不同语义的 mip 链。迁移覆盖主视图、MASK 阴影和探针捕获共用的材质路径，不改资产、主相机或宿主。

材质法线贴图属于线性数据，不进行 sRGB 转换，但可以用 mipmap 做缩小过滤。对本资产的 RGB 法线编码，线性平均与向量平均相容，采样后按 `2 * sample - 1` 解码、应用法线强度，再在照明前归一化。高频法线引起的镜面闪烁需要实际验收，不能将法线方差/粗糙度联合预过滤等高级方案预设为所有纹理的必要条件；新增方案仍先讨论。GBuffer 的当帧法线资源与材质法线贴图区分，当前方案不为 GBuffer 法线建立 mip。基础语义参见 [glTF 材质规范](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html#materials)。

### 4.2 PBR、天空与太阳阴影

GBuffer 正确保留材质因子、UV、TBN、tangent.w、双面法线和 MASK。使用 GGX、相关 Smith visibility、Schlick Fresnel，明确感知粗糙度与 GGX 参数的平方关系；低粗糙度高光不能被错误数值下限压平。环境镜面使用预过滤纹理与 BRDF 积分 LUT，不将旧解析拟合当作架构约束。

AtmospherePass 使用 Rayleigh/Mie 散射、透射率 LUT、多次散射近似及天空视图 LUT。AtmosphereData 中的两张基础 LUT 分别提供透射率与单位太阳辐照度下的多次散射响应，按大气介质、行星及地表参数生成；太阳天顶角是 LUT 的采样参数，太阳方向/强度与主相机移动不使基础 LUT 重建。参考 [Hillaire 天空大气方案](https://github.com/sebh/UnrealEngineSkyAtmosphere) 的算法，不引入其框架、依赖或场景光追路径。

SkyData 的 SunIrradianceOutsideAtmosphere 明确为大气外源强度，不预乘透射率。表面直接光、探针捕获中可见表面的直接光，以及体积散射在各自世界位置查询 TransmittanceToSun，仅乘一次。太阳盘辐亮度从同一源强度和角半径推导，并在观察点按朝向太阳的路径应用透射率；不另设不一致的盘亮度。天空视图 LUT 存储已经应用太阳源强度并完成大气积分的散射辐亮度，排除锐利太阳盘；下游直接读取，不再重复乘太阳强度或大气透射率。Lighting 单独绘制可见太阳盘，探针捕获不加入该盘。

Renderer 显式传入当前主相机或当前探针的 ObserverPositionWorld，使用同一世界到大气坐标约定计算高度和当地向上方向：世界单位为米、Y 向上、世界原点位于大气地表，行星中心为 `(0, -PlanetRadiusInMeters, 0)`。该映射只用于大气计算，不修改场景变换或相机。SkyViewData 同时携带观察位置与该 LUT 的采样映射参数，不能仅传纹理而丢失它对应的观察点。

MainSkyView 按主相机观察位置、太阳和大气依赖更新；其角度参数化不依赖相机旋转、jitter 或 R/D。每个探针捕获前先用该探针位置和冻结光照快照生成 ProbeSkyView，六面使用同一份；完成六面消费后才允许按 GPU 命令依赖顺序覆盖复用。同帧多个探针使用不同常量区域，不能用 CPU 同址覆盖代替有序的 GPU 更新，也不逐探针 Submit/Wait。Lighting 只接收 MainSkyViewData，ProbeCapture 只接收对应 ProbeSkyViewData；主相机移动不改变探针缓存版本。

`TimeOfDay=6..18` 的表面指向太阳方向为 `-Z → +Y → +Z`；正午位于正上方，光线垂直向下。太阳盘、直接光、阴影与体积光一致，光线传播方向与该方向相反；世界 Y 向上，不改相机。

ShadowPass 使用按场景世界范围拟合并留边界的稳定 4096² 正交 D32 阴影，固定光照时不跟随相机。PCSS 初始 16 次遮挡物搜索、32 次过滤，根据太阳角半径及遮挡物/接收面世界距离估计半影，正确换算至阴影 texel/UV。太阳角半径约 0.27° 起调，bias 以无痤疮且不悬浮为准。片段 Shader 必填，OPAQUE 可无颜色输出，MASK 与主视图共用裁剪；BeginRenderPass 的 colorAttachments 使用空集合 `[]`，不传 null。不以固定大半径模糊冒充接触硬化。

### 4.3 光栅化探针 GI 与缓存

GI 捕获表面受到太阳直接照射后的出射漫反射辐亮度，以及可见的散射天空，卷积为辐照度；接收表面再应用自己的漫反射响应与 1/π。漫反射与镜面捕获都排除已单独解析计算的锐利太阳盘，保留散射天空和受太阳照射表面的出射光，避免 GI 再次计入直接太阳。提供屏幕外的一次漫反射反弹与受场景遮挡的天光，不承诺完整多次反弹。捕获不读取旧 GI、SSR、曝光或 Bloom，避免反馈累积。

初始 18 个探针、两个高度层，覆盖庭院、侧廊与过渡区域；按实际空间安放，避开墙柱和实体内部，不直接均分整个 AABB。每面捕获 128²，辐照度 32²，镜面从 128² 生成完整 GGX mip，距离矩 64² RG32Float。漫反射最多混合 4 个适合的探针，结合位置、法线朝向与距离矩可见性；不把不规则布点当作规则网格插值。

捕获三个颜色附件依次为 DiffuseRadiance（RGBA16Float）、SpecularRadiance（RGBA16Float）、RadialDistance（R32Float），均为每面 128² 的单 mip Cube。辅助 2D D32 只选出当前面最近的表面，各面重用并 clear=1，不保存或采样前一面的硬件深度。RadialDistance 由片段写入 `length(PositionWorld - ProbePositionWorld)`，这是世界径向距离数据，不是 device Z 或 D32 兼容副本。

每个探针六面统一使用 90° 投影，CaptureNearInMeters 初值 0.05m；CaptureFarInMeters 为该探针到场景世界 AABB 八角点的最大距离加 1m，与太阳时间无关。距离附件清为有限的 far；天空无几何命中时保持或写入 far，不用 0、负数、NaN/Inf 表示未命中。探针位置不得使有效几何被近裁剪遗漏，far 与 far² 必须有限可表示；探针投影不改变宿主相机。

距离矩由每个输出 texel 对应的 2×2 原始径向距离 texel 生成。逐点读取 dᵢ，先计算各自的 dᵢ²，再使用同一组非负归一化权重求 `μ=Σwᵢdᵢ`、`ν=Σwᵢdᵢ²`；初始权重为源 cube texel 立体角近似 `(1+u²+v²)^(-3/2)` 的归一化结果，其中 u/v 是面内 [-1,1] 坐标。不能先过滤 d 再平方；后续过滤也同时处理两个矩，可见性方差为 `max(ν-μ², 0)`。天空样本贡献 `(far, far²)`。距离矩随场景几何、探针位置或捕获范围变化更新，太阳/大气光照变化不重建距离矩。

距离矩用于降低跨墙插值，不能修复实体内探针或据此声称没有薄墙漏光。DiffuseGiPass 只做半 R 求值、短历史稳定与半 R 保边滤波，输出不含接收材质或 AO；最终全 R 重建归 Lighting。GI 接管旧半球环境漫反射职责，两者不能全量相加。

探针直接使用公共逐面附件接口；Vulkan GetAttachmentView 已按用户授权改为单面 2D 视图，实际逐面写入/采样仍须验证，不将“2D 渲染后复制到 Cube”作为兼容实现。过滤缓存以 CubeArray 组织，Storage 与采样视图按真实接口核对；面序、朝向、mip 与卷积跨面连续性必须验收。

首次必须完成全部探针、距离矩和过滤 mip 后才发布。稳态 ProbeData 的 Old/New 两组引用可以同时指向当前完整组，Blend=0；未完成的备用组不能暴露给消费者。太阳参数稳定约 100ms 后，按冻结的太阳、阴影、大气及场景版本每帧更新一个探针至备用组；构建期间相关输入变化则取消未完成批次，只保留最新请求并重新等待稳定，不读取不匹配的阴影或大气快照。

备用整组完成后进入约 0.2s 的 Old→New 混合，Blend 从 0 到 1；两组的版本均指向各自完整光照快照，GI 与镜面使用同一个 Blend。混合期间两组均只读，新时间/光照请求仅合并为待处理的最新请求，不能覆盖任一组。混合结束且 GPU 已不再读取旧组后，新组成为当前组、旧组才能作为下一批备用；通过现有帧提交完成时序判定安全，不增加 Pass 内等待或第三组缓存。

待处理请求不会提前改变已发布版本；若混合结束时最新请求已稳定满 100ms，即可在安全的下一帧开始构建，否则继续等待稳定。场景几何、探针位置或捕获范围变化时，两组光照与共享距离矩一起失效，完整重建后发布，不在不同几何版本间混合。主相机移动、Resize 和 RenderScale 变化不使探针缓存失效。至少 18 个更新帧的收敛延迟须验收；不能达到目标观感时先报告策略能力差距，不能接受部分房间使用不同时刻的太阳。

### 4.4 GTAO 与光照合成

AO 使用 GTAO 地平线遮蔽思路，半 R，接触半径约 0.3–0.5m 起调；从深度与未扰动表面法线求解，做深度/法线引导的保边滤波。背景可见度为 1；只调制间接漫反射一次，不压黑直接光、镜面、天空或体积散射。参考 [GTAO 原始研究](https://research.activision.com/publications/archives/practical-real-time-strategies-for-accurate-indirect-occlusion)。

LightingPass 在 R 使用当前 device depth 与 SurfaceNormal，对半 R 的 AmbientVisibility 和 IndirectIrradiance 做最终引导上采样：半 R texel 与其选取的全 R 表面样本一一对应，过滤权重同时检查深度/法线连续性，不跨轮廓直接双线性混合，不另建重复的全 R GI/AO 纹理。GI/AO Pass 只负责半 R 的求解与滤波，不重复执行此步骤。

半 R texel 对应全 R 的 2×2 像素块，GI/AO 使用其中普通 Z 最小的有效像素作为表面样本；相等时按固定行优先顺序选择。奇数边缘只取有效像素，全为天空时输出 AO=1、GI=0。Lighting 对候选半 R texel 使用同一选择规则取得引导深度/法线，不假设原生尺寸向下取整的 depth mip1 等于向上取整的半 R 目标。

如果当前全 R 表面没有任何深度/法线兼容的半 R 候选，Lighting 在该像素按相同的探针 GI/GTAO 算法直接求值，使用同一份 GI/AO 参数、缓存版本与 Blend；不除以零权重和，不用常量、跨轮廓最近邻或扩大模糊填补。该分支只补足半 R 采样未覆盖的表面，可以复用实际共用的 Shader 求值函数，不调用其他 Pass、不新增效果或整图资源，也不再叠加一份材质/AO 响应。

LightingPass 随后计算直接漫反射、直接镜面、GI 漫反射和局部探针镜面，背景从 MainSkyViewData 求值并添加可见太阳盘。定义 `Lbase = Ldirect + AO * LdiffuseGI + LprobeSpecular`；接收材质响应与 AO 各应用一次，LprobeSpecular 独立保留用于替换。光照和滤波各阶段保持一致辐射单位。

### 4.5 混合反射与专用降噪

局部探针捕获真实建筑、旗帜和柱廊；镜面捕获可含直接漫反射与直接镜面，排除已有间接缓存反馈及后期。环境镜面捕获排除已由解析太阳处理的锐利太阳盘，避免重复太阳贡献。GGX 重要性采样预过滤初始每 texel 128 个确定性样本，roughness 与 mip 映射一致；每点最多混合 2 个探针，做对应空间的盒体投影修正。

SSR 读取普通 Z 最小深度层级与反射合成前的线性 HDR 颜色层级。低粗糙度区域全 R 采样或补算，普通光泽区域半 R，高粗糙度按已确认算法使用预过滤探针；初始最多 64 步遍历与 5 次交点细化。明确深度厚度、背面、近面、出屏及可信度，不能用任意厚度掩盖错误交点。参考 [层级 SSR 算法](https://gpuopen.com/manuals/fidelityfx_sdk/techniques/stochastic-screen-space-reflections/)，不引入 SDK。

SSR 独立保存历史、亮度矩、命中距离与可信度，进行重投影、历史裁剪、粗糙度相关保边滤波。检查深度、法线、粗糙度与显露；低粗糙度反射考虑命中/镜像视差，不能仅使用表面 motion。保留镜面细节，不将长拖影或模糊当作稳定。参考 [反射专用降噪设计](https://gpuopen.com/manuals/fidelityfx_sdk/techniques/denoiser/)。

`Lreflection = Lbase + confidence * (LssrSpecular - LprobeSpecular)`，两项使用相同材质响应。SSR 按可信度替换镜面项，不叠加两份反射，不读取本帧已合成 SSR 的颜色制造递归。出屏/不可信交点按混合算法转向局部探针，不代表允许在故障时关闭 SSR。近距离镜面视差、空间过渡及离屏变化必须实际验收，质量不足先报告方案能力差距。

### 4.6 体积光与历史

采用单 mip 视锥 froxel 网格，初始 `ceil(Dw/12) × ceil(Dh/12) × 48`，对数深度切片，覆盖近处约 40m 且不超过相机有效范围。注入高度雾消光、太阳散射和弱天空散射，使用太阳阴影形成建筑开口光束；使用 HG 相函数，按实际世界步长积分。

独立三维历史以世界位置重投影至上一帧体积视锥，检查有效覆盖与密度/光照变化，不使用表面 motion。时间滤波后沿视线前向积分散射 S 与透射率 T，按场景深度截断，`Lvolume = Lreflection * T + S`。表面与天空使用同一介质，局部雾与天空大气明确作用距离，避免重复消光。验收条带、边缘漏雾、运动残影及光照变化，与 SGSR 组合另按第 6 节核验。

### 4.7 曝光、Bloom 与输出

ExposurePass 从体积光合成后、Bloom 前的 R/HDR 求低分辨率对数亮度归约，降低天空测光权重，用真实时间控制明暗适应，输出 GPU 内 1×1 状态，不逐帧读回。曝光上下限/适应速度以室内外过渡稳定、受光区保留纹理为准，不用曝光抵消错误 GI 或光照尺度。

Bloom 从四分之一 D 开始，软阈值提取、多级降采样/上采样；阈值考虑曝光，输出保持未曝光 HDR。半径固定在显示空间，使 RenderScale/模式切换不改变光晕大小。ToneMapping 合成场景与 Bloom，应用曝光一次，使用 ACES fitted 风格映射并进行一次 sRGB 编码，不另建只为相加的完整 HDR 拷贝。

## 5. 三模式与时域输入

| 模式 | 完整输出路径 |
| --- | --- |
| None | R/HDR → Bloom + ToneMapping；R=D 直写 Color，否则 R/LDR → 双线性 → Color |
| Spatial | R/HDR → Bloom + ToneMapping 的 R/LDR → SGSR 1 → Color；scale=1 仍走 Spatial |
| Temporal | R/HDR + GBuffer depth/motion → SGSR 2 Quality 的 D/HDR → Bloom + ToneMapping → Color |

三模式测光来自同一公共 R/HDR，材质、世界光照、阴影质量和效果参数一致。Bloom 使用各模式正确位置的 HDR，半径按 D 定义。None/Spatial 不加投影 jitter 或全屏 AA，但效果自身历史仍工作；Temporal 不叠加独立 TAA 或 SGSR 1。使用现有 CreateSpatialUpscaler/CreateTemporalUpscaler，Desc 尺寸/模式改变时重建。

| Temporal 字段 | 调用方数据与状态 |
| --- | --- |
| Input | 体积合成后的 R 尺寸未曝光线性 HDR |
| OpaqueInput | 官方定义为透明内容绘制前的颜色，无透明内容时可与 Input 相同。本项目传体积合成前的表面 HDR（含 SSR），与 Input 同帧、同 R、同 jitter、同颜色域；这是按官方通用契约确定的本项目接入方式，体积运动质量仍须实际验证 |
| Depth / MotionVectors | R 普通 device depth（near=0、far=1）/ 编码 motion；不传米深度或原始 UV velocity |
| JitterOffsetX/Y | 输入像素单位 jitter，与 GPU 投影一致 |
| PreExposure / CameraFovAngleHor | 1 / `tan(fovYRadians / 2) * projectionAspect`；后者是水平半视角正切，aspect 来自基准投影，不传角度 |
| MinLerpContribution / SameCamera | 显式 0 / 未 jitter 相机是否静止；官方文档明确这两项仅用于 2-pass，当前 Quality 为 3-pass，不能当作效果历史补偿旋钮 |
| Reset | 首帧、尺寸/模式变化、相机不连续或显著光照不连续；普通连续运动不反复 Reset 掩盖拖影 |

GBuffer 输出 `motion = currentUnjitteredNdc.xy - previousUnjitteredNdc.xy`，编码为 `motion * 0.2495 + 32767.0 / 65535.0`，存 R16G16UNorm。静止约 0.5；天空只含旋转 motion。超出可编码范围时按扩展定义显式使用零编码的矩阵回退，不依赖 UNorm 饱和。重投影满足 `previousUv = currentUv + (-0.5 * motion.x, 0.5 * motion.y)`，允许量化误差。

Halton(2,3) 的 8 帧序列减 0.5，输入像素 jitter=(jx,jy)，NDC 偏移 `(2*jx/Rw, -2*jy/Rh)`。矩阵回退时 C/P 为当前/上一帧未 jitter ViewProjection，J 为当前 clip jitter，`ClipToPrevClip = inverse(C * J) * (P * J)`。首次/历史无效用有效当前矩阵初始化前帧基准；切回 None/Spatial 用原始投影副本。

项目自身效果历史保存未曝光线性信号；SGSR 内部保存归一化 YCoCg 等数据，遵守原实现，不修改或重新定义。[官方 SGSR2 接入文档](https://github.com/SnapdragonGameStudios/snapdragon-gsr/blob/main/sgsr/v2/README.md#integration) 将 preExposure 定义为前帧与当前帧场景预曝光的比值；本项目超分前不应用曝光，两者均为 1，故传 1，自动曝光在最终 ToneMapping 应用。不能仅因没有单独 PreviousPreExposure 参数判定缺少曝光支持。新增 HDR 范围与量化须验证，不钳制 HDR、修改编码、强制 Reset 或改变扩展参数语义作隐式补偿。

官方 3-pass Convert 使用 InputColor 与 InputOpaqueColor 的颜色差分构造透明响应，现有扩展相关计算与官方一致；不需要另加 reactive mask 参数来补齐接口。官方说明了透明内容前后颜色的通用契约，没有给出此 Sponza 体积雾 Pass 的专属配方；体积前后两图的方案按该契约推导，保留输入直至 Dispatch 完成，用实际运动/显露画面验证，不把未验证项列为 SGSR 缺陷。SSR 属于表面 HDR，包含在两张颜色图中。

## 6. RHI 就绪情况与验证边界

### 6.1 报告与决策规则

实施前核对当前源码和第 2 节调用链，将结论分为源码可定位问题、正确调用后的运行阻挡、尚未验证的能力组合、已公开的算法近似。不能把未测标为不支持，也不能把 API/枚举存在当作全后端可用；旧版本结论须刷新。

发现问题先说明需求、真实签名/参数、触发条件、源码/运行证据、影响、候选处理及质量/性能代价。依赖该问题的实现暂停，独立工作可继续。替代方案即使只用公共 RHI、没有平台分支且理论等价，也不得隐式选用；先交用户决定。修 RHI/扩展仍需单独授权。

### 6.2 封板时的就绪情况

以 `9eb1e18` 为封板时的源码基点：两处已授权后端修复和当前 ImageSharp compand 接口均已进入源码；Sponza Release 构建为 0 警告、0 错误。Metal 公共入口的基础资源组合验证通过 760 项检查，验证层消息为 0；具体范围如下，不代表新光照管线或 Sponza 画质验收通过。当前未发现新的、必须先修改 RHI 的确定性阻挡；DX12/Vulkan 对这些新组合的实际运行仍未验证。

| 需求 | 已知事实与未确认部分 | 实施约束 |
| --- | --- | --- |
| Cube/CubeArray 逐面附件 | 创建层数为 6/6×cubeCount，VK 单面视图已改为 Type2D。Metal 验证了 2 个 cube、各 6 面、3 个 mip 的 CubeArray 颜色附件 clear 和 cube 采样 | 完整场景捕获与 DX12/Vulkan 实机路径后续验收；不能把颜色 clear 探针等同全部捕获 Shader 已验证，不增加 2D+CopyTexture 或后端分支 |
| 四 MRT + D32 | Metal 已验证 R8G8B8A8SRgb、RGBA16Float、R16G16SNorm、R16G16UNorm 加 D32 的 7×5 绘制/读回；包含同一命令缓冲内绘制后采样 | sRGB RGB/线性 alpha、SNorm 正负值、UNorm 与 D32 已知值通过；实际 GBuffer 材质 Shader、其他尺寸和 DX12/Vulkan 后续验收 |
| CubeArray/mip 与 3D Storage | Metal 已验证 CubeArray 的单 mip 2DArray Storage 视图写入且其他 mip 不变；5×3×3 单 mip 3D 写入与三线性采样通过，包含同一命令缓冲内写后读 | 后续验证真实分辨率、算法、帧间历史与目标平台；不改 atlas/2D 切片，体积仍采用已定义的单 mip 网格 |
| 探针捕获与距离矩 | 第 4.3 节已固定两张 RGBA16Float 加 R32Float 的捕获颜色附件、辅助 2D D32，以及 RG32Float 距离矩的生成语义 | 这组完整捕获/卷积组合尚未运行验证，实施时按契约核验；属于未验证组合，不能提前列为不支持或暗中换格式 |
| 条件性 3D 非零 mip 问题 | DXTextureView 的 Texture3D UAV 已将 WSize 改为 uint.MaxValue，使用原生 UINT(-1) 语义从 FirstWSlice=0 覆盖所选 mip 的全部深度切片，不再使用原始 Depth | 源码修复及构建已完成，实际非零 mip GPU 回归未验证。原单 mip froxel 不触发旧错误；3D 深度随 mip 缩小，2D Array 的数组层数不缩小 |
| 单根常量与同步 | 三后端 SetConstantBuffer 均使用基址加偏移；Metal 已验证同一命令缓冲内分别绑定 0/256 字节常量区域并得到不同预期值 | 实际多 draw/face/mip 的常量区仍须隔离，producer/consumer 依赖按真实读写放置；基础探针不能代替新管线全链验证 |
| 体积光/反射 + SGSR | 已核对官方 3-pass 文档与 Convert 源码：透明前/后颜色差分为官方机制，无透明内容时允许同图；相关扩展实现一致 | 按第 5 节提供输入并验收，属于 Sponza 集成验证，不列为 RHI/SGSR 已知阻挡；不自制 mask/补丁，不悄悄移动体积 Pass |
| 材质过滤与 mip | 当前入口为 compand=true、generateMipMaps=true；Metal 已确认默认 sRGB 与显式 compand=false 的 UNorm 采样语义。此前颜色路径的 mip/alpha 验证不代表当前 Sponza 已迁移 | 重构第一步成套迁移基础色、法线/MR、Shader 解码和缓存键，迁移前的旧画面不能作正确原生基线；不恢复旧接口，不新增高级法线预过滤 |
| 能力信息 | Capabilities 不公开逐格式用途、MRT 或工作组上限查询 | 这是核验手段限制，不等于设备不支持；以实际创建/公共读写/目标设备运行验证，不加私有探测或平台分支 |

Cube 问题定位在 `sources/Zenith.NET.Vulkan/VKTexture.cs` 的 GetAttachmentView 与 VKFormats 的 TextureType 映射；与 [Vulkan Cube 视图层数规则](https://docs.vulkan.org/refpages/latest/refpages/source/VkImageViewCreateInfo.html) 对照。资源创建层数与单面附件视图范围是两件事，不能混淆。此前“普通 2D 附件 → CopyTexture → Cube”的建议绕开该视图创建，增加中间资源、传输用途与同步成本，已撤出默认技术路线，未经用户决定不实施。

材质与 SGSR 项属于加载/接入语义和质量验证，不等同于核心 RHI 缺少纹理能力。现有官方透明差分机制应正确使用；不能仅因一份表面 motion 不能精确表达体积/反射的全部运动而判定高通有 bug，也不能因专用效果降噪已完成而跳过超分验收。

### 6.3 实施时继续验证的范围

仓库外验证沿用已有工程、依赖、ZenithCompiler 与公共 RHI。先核对合法输入，再验证四 MRT 的已知颜色/法线/depth、D32 采样、Cube/CubeArray 各面和 mip、单 mip 3D Storage 写入/三线性采样、非零常量偏移与同帧复用，以及 producer/consumer 屏障。用已知输入和公共读回比较结果，覆盖非零层/mip、奇数尺寸与初始化/释放，不只以没有报错判定通过。

正式捕获路线已确定为公共逐面附件，不把“2D 附件 → CopyTexture → Cube”保留成自动后备路径。若后续出现正确调用仍失败的情况，按第 6.1 节报告后再决定。资产过滤与 SGSR 质量验证使用正常公共入口和真实目标输入，所有临时源码、配置和输出留在仓库外。GPU 计时继续单独处理，不作为启动渲染重构的前置条件。

## 7. 实施顺序、性能与完成标准

| 阶段 | 目标与通过条件 |
| --- | --- |
| 0. 启动与基线 | 等待用户启动通知；之后完整阅读两份文档及 RHI/三后端/扩展，刷新起始工作区与第 6 节验证范围，核对 API/输入/所有权；不因基础探针通过跳过完整验证 |
| 1. 材质迁移与原生主光照 | 先按当前 compand 接口成套迁移所有材质路径，再完成 GBuffer、PBR、天空、阴影与输出；默认镜头下几何、材质、MASK 和太阳方向正确 |
| 2. 原生 GI/AO | 验收反弹光、颜色传递、侧廊明暗、接触遮蔽、探针覆盖与薄墙漏光，检查缓存版本/时间切换 |
| 3. 原生反射 | 验收建筑细节、粗糙度、局部视差、屏幕边缘过渡与静止/运动降噪 |
| 4. 体积与后期 | 验收光束/遮挡/积分/历史；先固定曝光检查光照，再验证自动曝光与 Bloom，无条带、残影、曝光抽动或纹理丢失 |
| 5. 超分正确性 | None 双线性、SGSR1、SGSR2；核对尺寸/颜色域/运动/显露、体积与反射响应、历史及模式切换 |
| 6. 完整回归 | 操作、Resize、生命周期、可用后端和完整 diff；已知问题修复复验后交付，未验证/受阻不算通过 |

| 观察对象 | 通过条件 |
| --- | --- |
| 原始启动画面 | 几何、朝向、比例正确；受光保留纹理，阴影保留结构，无整体发灰、过曝或死黑 |
| 石材、布料、金属 | 基础色/因子/法线正确；粗糙度、高光和反射清晰度对应材质，不修改资产迎合效果 |
| 叶片、链条 | 镂空、双面与阴影轮廓一致，远处 mip/过滤不产生错误覆盖 |
| 柱脚、墙角、侧廊 | 阴影无痤疮/悬浮，AO 无黑晕，GI 无跨墙亮斑和探针边界，不重复压暗 |
| 反射 | 建筑细节与材质相符，无错命中、重复镜面、cube 接缝、突变或残留拖影；离屏/空间过渡达到目标观感 |
| 天空与太阳 | 6/12/18 点方向 -Z/+Y/+Z，光色、太阳盘、阴影、体积光一致，天空无接缝/异常条带 |
| 体积与后期 | 光束受建筑遮挡，雾与表面深度一致；运动无可见历史残留，曝光适应自然，Bloom 不吞纹理 |
| 三种超分模式 | 相同材质/光照下效果完整，运动与显露正确，无实验接入造成的错误；官方 SGSR 边缘偏色按用户范围不处理 |

排错顺序为资产/变换 → 材质颜色与过滤 → 法线/PBR → 直射/阴影 → 探针 GI/AO → 反射/降噪 → 体积 → 曝光/Bloom → 超分。先检查输入/中间结果再看合成；只在最终图看不到问题不构成通过。临时诊断验证后恢复，正式项目不残留调试控件或验证代码。

- 原生 None/scale=1 质量通过后，再比较 None、Spatial、Temporal 各自的 1.0、0.75、0.5。固定曝光/光照比较，再单独验证自动曝光；原生图非无锯齿真值，不因此加入另一套 AA。
- 静态对比等待缓存、效果历史和 Temporal 收敛，Temporal 至少 32 帧；用既有操作检查慢移、快速转头、停止，覆盖天空、栏杆/细柱、叶片、布料、墙角和不同粗糙度。
- 覆盖奇数尺寸、连续 Resize、最小化恢复、时间/模式/Scale 切换及退出释放。结合公共读回、验证层与实际图像核对矩阵、motion、资源内容、历史重置和缓存一致性；实验不得引入越界、NaN/Inf、泄漏或失效句柄。
- 18 个探针完整更新为 108 个视角；复用捕获资源，稳定光照下不每帧重绘。优先减少无效绘制、重复材质工作、整图复制和带宽；半 R、定向补算和采样数以质量通过为前提，不以降质完成性能验收。
- R=D=2560×1440 时，管线目标/历史初步按约 0.5–0.7GiB 规划，不含场景资产或 SGSR 内部资源；这是待按最终清单核对的预算，不是实测上限。捕获方案变化的成本单独说明，不为凑预算隐式换方案。
- GPU 耗时只报可靠实测，区分稳定帧与缓存更新，排除 ImGui/等待。可靠计时可用时预热至少 60 帧、统计至少 120 帧；当前独立计时问题不在此修复，不能可靠测量则标未验证，不用 FPS 或加工数值代替。
- 实施获准后，无争议阶段自主构建、运行、查看画面并修正，不逐阶段要求批准；遇第 6 节问题先报告并暂停依赖路径。检查相对起点的完整 diff（含暂存/新增文件），只撤销自己的越界修改，不覆盖用户已有改动，不扩展全仓格式化。

从仓库根目录执行正式项目构建与运行：

```sh
dotnet build sources/Experiments/Sponza/Sponza.csproj -c Release
dotnet run --project sources/Experiments/Sponza/Sponza.csproj -c Release --no-build
```

最终仅在对话中说明改动、构建/运行/视觉验收、可靠 GPU 耗时、已完成、未验证与受阻项。无法运行/查看画面不能宣称画质通过；未解决渲染错误不能因算法近似或平台差异被从验收中删除。一个后端通过不代表其他后端通过；全部承诺范围经过验证且已知问题清零后才能宣布完成。
