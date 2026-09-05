# Sponza 高质量光栅化 PBR 开发文档

## 1. 目标与执行边界

面向后续开发 agent。目标是一个材质可信、光影柔和、室内有层次、运动稳定的 Sponza 场景，支持程序化天空、06:00–18:00 时间变化和三种超分选项。以好看和完整为优先，不追求严格物理模拟。“高质量”按本文截图与稳定性验收，不宣称存在适合所有硬件的最佳算法。

只使用传统顶点／片元光栅化和 compute；禁止 BLAS/TLAS、RayQuery、硬件或软件场景光追、路径追踪。屏幕空间深度采样可用于遮蔽；反射采用光栅捕获的局部探针。本版不实施 SSR、SSGI、体素 GI、网格着色、动画系统或通用引擎框架。

**当前交付仅有 App 的 ImGui 控件，渲染尚未实现。** `Renderer.Color` 返回 `null!`，直接运行尚不能显示场景。本文描述待开发行为，不代表已有能力。后续收到实施任务时按阶段持续推进；本次不要据本文提前实现 Renderer。

## 2. 必读代码与风格

以下路径相对仓库根目录。实施前重新阅读当前版本，不以文档代替源码：

| 文件／目录 | 需要理解的约定 |
| --- | --- |
| `sources/Experiments/Sponza/App.cs`、`Renderer.cs`、`Handlers/`、`Helpers/` | 窗口、相机、ImGui、输出纹理及生命周期 |
| `sources/Experiments/CornellBox/Renderers/RasterizationRenderer.cs` 及对应 Slang | 管线创建、常量结构、DrawIndexed、矩阵顺序 |
| `sources/Experiments/FluidTank/Renderer.cs`、`Helpers/GraphicsHelper.cs` | 多 pass、资源上传、尺寸重建；不复制其中光追路径 |
| `sources/Zenith.NET/CommandBuffer.cs`、`Texture.cs`、`TextureView.cs`、`Structs/` | RHI 真实签名、资源用法、子资源与附件 |
| `documents/docs/fundamentals/{bindless-resources,synchronization,shaders}.md` | 描述符句柄、同步和编译 |
| `sources/Extensions/Zenith.NET.Extensions.Upscaling/` | SGSR1、SGSR2 参数及底层 shader |
| `sources/Extensions/Zenith.NET.Extensions.ImageSharp/Extensions.cs` | UNorm 载入与 CPU mip 的局限 |
| `sources/Extensions/Zenith.NET.Extensions.ImGui/` | Legacy 色彩路径、纹理绑定存续 |
| `sources/Directory.Build.props`、`sources/Experiments/Directory.Packages.props` | net10.0、nullable、unsafe、包集中管理 |

保持文件作用域 namespace、`internal class`、私有 camelCase 字段、公开 PascalCase 成员、目标类型 `new()`、集合表达式和显式 Dispose。GPU 常量沿用显式布局；pass 专用结构可放文件末尾用 `file struct`。只提取重复且已有两个调用点的 helper，不引入依赖注入、服务定位器、ECS、通用 RenderGraph 或新的解决方案项目。

沿用当前 Windows→DirectX12、macOS→Metal、Linux→Vulkan 的选择。优先在可用 Windows 设备运行，其他平台没有实机则明确记为未验证。

## 3. 目录与职责

保持已有目录，以下是未来文件建议，不要求一次性创建空文件：

```text
Sponza/
  App.cs                     # 窗口、输入、三个主控件、提交和呈现
  Renderer.cs                # 唯一渲染入口，持有资源并调度 pass
  Program.cs
  DEVELOPMENT.md
  Models/
    Scene.cs                 # CPU 场景和 GPU 几何／材质资源
    Vertex.cs
    Material.cs
    RenderSettings.cs        # 后续接入时引入，使用 UpscalingMode 枚举
    UpscalingMode.cs         # 已实现：None / Spatial / Temporal
    FrameData.cs             # 当前／前帧矩阵、分辨率、太阳、抖动
  Passes/
    ShadowPass.cs
    GBufferPass.cs
    AmbientOcclusionPass.cs
    SkyPass.cs
    EnvironmentPass.cs
    LightingPass.cs
    TransparentPass.cs
    FogPass.cs
    AntiAliasingPass.cs
    UpscalingPass.cs
    PostProcessPass.cs
  Helpers/
    CocoaHelper.cs
    ImGuiHelper.cs
    GraphicsHelper.cs
    SceneLoader.cs
  Handlers/                  # 延续 CameraHandler、ImGuiHandler
  Assets/
    Fonts/
    Models/                  # 模型、贴图、原始许可证和来源记录
    Shaders/                 # 各 pass 的 .slang 和共享函数
```

只有实现对应目录内容时才删除其中占位文件。App 不持有场景中间纹理；Renderer 对外只暴露最终 `Color`，接收 settings 和 camera，继续提供 `Update`、`Render`、`Resize`、`Dispose`。可在 Update 增加 settings、delta 参数，不另建宿主。Pass 只录制命令，不提交、不 Present、不创建窗口。记录每种资源的唯一所有者，禁止 pass 重复释放 Renderer 所有的输入。

## 4. 控件与设置契约

本次 App 字段为 `renderScale = 1.0f`、`upscalingMode = UpscalingMode.None`、`timeOfDay = 12.0f`。超分使用 Models 中的 `UpscalingMode` 枚举，由下拉框直接选择。遵循现有实验英文 UI。两个滑条使用 AlwaysClamp，Ctrl+单击输入也受范围约束；时间用十进制小时显示，例如 6.5 h 为 06:30。

| 设置 | 当前 UI | 后续渲染语义 |
| --- | --- | --- |
| Render scale | 0.50–1.00，默认 1.00 | 以 framebuffer 像素数计算内部宽高，非面积比例 |
| None | `UpscalingMode.None` | 不运行 SGSR；低分辨率使用普通双线性展示，仍尊重 scale |
| Spatial | `UpscalingMode.Spatial` | 内部分辨率抗锯齿，之后 SGSR1 放大 |
| Temporal | `UpscalingMode.Temporal` | SGSR2 Quality，负责时域重建；不叠加另一次 TAA |
| Time of day | 6.0–18.0，默认 12.0 | 手动时间，不自动播放；统一驱动太阳、天空、环境光和雾 |

未来将上述状态映射到 Models 中强类型 settings，避免到处判断数字；每帧在 UI 结束后由 Renderer 消费一次快照。不要把扩展的 `TemporalUpscalerMode.Speed/Quality` 当作三种 UI 模式。默认值保持当前 UI；另可记录一组适合截图的 16:30 参数，但不要悄悄改变用户选择。

内部尺寸 `max(1, round(framebufferDimension * scale))`；实际 dispatch 向上取整，shader 必须做边界检查。不能为整除 8 扭曲画面比例。输出 Color 始终为 framebuffer 尺寸；ImGui 使用逻辑尺寸，UI 不参与超分。支持 100% Temporal（等尺寸重建）。

尺寸／scale／模式变化：在安全帧边界重建依赖尺寸的资源和超分实例，清除历史；仅在计算后的尺寸改变时重建纹理。时间变化不重建屏幕纹理，但更新光照、环境缓存并使相关历史失效。

## 5. RHI 必须遵守的约束

1. `ResourceHandle` 是两个 uint，共 8 字节；Slang 对应 `DescriptorHandle<T>`，不是裸 32 位索引。每个 GPU 结构核对字段 offset、stride、padding、矩阵和句柄。`SetConstantBuffer` 第二参数是字节偏移。逐 draw 常量要用不同 buffer 或满足后端对齐的不同切片，不能反复覆写同一地址后期望 GPU 看到各次旧值。
2. `ZenithCompiler` 使用 row-major，现有 shader 为 `mul(vector, matrix)`。保留行向量顺序 Model→View→Projection；法线用逆转置。标准深度 0 近、1 远、清除 1，暂不改 reversed-Z，以匹配 SGSR2 深度选择。三后端用同一个测试三角形验证 UV、正面绕序和屏幕 Y，不盲目添加 API 专用翻转。
3. Usage 在创建时齐全：附件、Sampled、Storage、传输各按用途声明。SampledHandle 不会自动转换 layout；输出给 ImGui 前必须成为 Sampled。
4. `Transition(texture, default, ...)` 只处理 mip0/layer0；阴影数组、立方体六面和 mip 链逐子资源转换。`Undefined` 仅用于允许丢弃旧内容的写入；历史纹理绝不能每帧从 Undefined 读取。
5. 同 layout 的写后读用 `Barrier`，切换角色用 `Transition`。compute Dispatch 放在 render pass 外。`BeginRenderPass` 附件尺寸／格式必须匹配管线。深度供后续采样时转为适合的 layout，透明只读深度按实际后端实现验证。
6. 首版保持 App 的单帧 `Submit().Wait()`，同一 graphics command buffer 录制所有帧 pass。初始化上传可批量提交并等待。确认正确前不引入异步 compute 或多帧并行；未来并行需独立每帧常量、明确 timeline 和延迟释放。
7. Resize／模式切换必须在 `imGui.Binding(renderer.Color)` **之前**处理。当前 App 在 UI 之前绑定 Color：未来需要调整为 UI→应用设置／重建→绑定 Color→录制，防止 draw list 引用已释放纹理。当前 ImGuiRenderer 缓存绑定并清理 IsDisposed 资源；保留这个机制，验证重建后缓存会回收，不编造 Unbind API。自动清理不能挽救已经引用旧纹理的本帧 draw list。
8. `Capabilities` 当前只提供设备名、RayTracingSupported、MeshShadingSupported；没有通用格式支持查询。不要编造 `SupportsFormat`。优先选仓库已用格式；新增 MRT／深度采样／cube storage 用小场景验证，不支持时记录具体后端限制并选简单替代格式。

## 6. 资产与材质

默认采用 [Khronos Sponza glTF 资源](https://github.com/KhronosGroup/glTF-Sample-Assets/tree/main/Models/Sponza)，将源 glTF 目录的内容直接放入本项目 `Assets/Models/`，保留模型引用的贴图与缓冲区相对路径，不再增加 `Sponza/` 层级。原始许可证与来源记录也放在 `Assets/Models/`。实施时固定具体提交，记录下载地址、文件清单、SHA-256 和原始许可；不要把仓库代码许可证当作模型许可证。缺资产时给出实际缺失路径与获取步骤，不能用立方体冒充 Sponza 验收，也不要提交大体积二进制而不遵循仓库约定。

通过现有 SharpGLTF.Core 读取，不新加另一个 glTF 框架。访问器使用库的解码 API，处理 interleaved、normalized、索引类型、节点层级和多个 primitive；保留实例变换或明确烘焙一次。单位统一米、Y 向上，先检查包围盒再确定相机起点、far plane、光照与探针位置。负缩放要处理绕序，非均匀缩放要处理法线。

按 [glTF 2.0 材质规范](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html) 实现 baseColor、metallic-roughness、normal、occlusion、emissive、纹理坐标及采样器。baseColor/emissive 的颜色通道解码 sRGB，alpha 和数据贴图保持线性；roughness 取 G、metallic 取 B、occlusion 取 R，乘对应 factor。支持 MASK/cutoff、BLEND 和 doubleSided；阴影与 GBuffer 用同一 alpha 测试。

保留已提供的 tangent.w，缺失时生成兼容切线；TBN 正交化、背面法线处理、normal scale 必须验证。列出资产实际出现的扩展，对 required 且不支持的扩展明确报错；不无声丢材质。缺贴图使用语义默认值，不能把缺失资源当加载成功。

现有 ImageSharp helper 固定 R8G8B8A8UNorm 且直接缩小编码值。高质量路径在 Sponza 内实现按语义上传：颜色 mip 在线性域过滤后编码，normal mip 归一化，alpha mask mip 尽量保留覆盖率；纹理缓存 key 包含色彩语义。可选 sRGB 格式自动解码或 shader 手动解码，但只能解码一次。优先保留资产材质差异，不全局把地板改成镜面来制造效果。

## 7. 固定渲染方案

选择延迟 PBR + 透明前向：GBuffer 供 AO 与统一光照使用；一个太阳和少量美术补光足够。首版 CPU 视锥裁剪／材质排序／DrawIndexed，profiling 证明必要后才做 GPU culling。

### 7.1 一帧顺序与资源

初始化生成 BRDF LUT；时间变化时更新程序化天空、天空 IBL 和局部反射探针。主帧顺序如下：

```text
Settings / Camera / FrameData
  → Sun shadows
  → GBuffer (opaque + mask, depth, normal, material, velocity)
  → AO + edge-aware filter
  → Deferred lighting (sun + IBL + local probes + fill)
  → Sky background → Fog on opaque → Opaque HDR snapshot
  → Transparent forward (same lighting + fog)
  → None / Spatial: internal TAA → exposure + bloom + tone map
       → Spatial SGSR1 or None bilinear → display encoding → Color
  → Temporal: SGSR2 Quality HDR reconstruction
       → exposure + bloom + tone map → display encoding → Color
  → ImGui → swap chain
```

None 的含义是不使用超分算法，仍允许普通抗锯齿。内部 TAA 在 M1–M4 前可暂用 FXAA，无 jitter；最终按 M5 接入 TAA。两个路径采用同一曝光与 tone mapping 参数。Spatial 放在 tone-mapped、未做显示编码的 0–1 图像上，Temporal 接收 HDR；不要把 HDR 直接传给假定有限色域的空间滤波。

| 资源 | 建议格式／尺寸 | 读写与寿命 |
| --- | --- | --- |
| Depth | D32FloatS8UInt／内部尺寸 | GBuffer 写，AO／照明读；必要时另输出 R32Float 深度供采样 |
| BaseColor | R8G8B8A8UNorm／内部 | 已解码的线性 baseColor 与 alpha／有效标记 |
| NormalRoughness | R16G16B16A16Float／内部 | xyz 世界法线，w roughness |
| Material | R8G8B8A8UNorm／内部 | metallic、材质 AO、保留通道；明确每通道 |
| Emissive | R16G16B16A16Float／内部 | HDR emissive，不能挤进 8 位而无声截断 |
| Motion | R16G16B16A16Float／内部 | 给 SGSR2 的编码 NDC motion，TAA 按相同契约解码 |
| Shadow | 深度 array，4 层，每层 2048² 起步 | 每层附件 view，随后采样；4096² 为测量后高档选项 |
| AO ping-pong | R16Float／半分辨率 | compute 写，双边滤波后按深度／法线上采样 |
| SceneHdr / OpaqueHdr | R16G16B16A16Float／内部 | 分开保存透明前后图像，供时域反应遮罩 |
| Sky / probe cube | R16G16B16A16Float／256² 起步 | 六面与完整 roughness mip，缓存更新 |
| ResolvedHdr / TAA history | R16G16B16A16Float／按模式 | 历史双缓冲，尺寸和曝光约定一致 |
| Bloom pyramid | R16G16B16A16Float | 不同 mip 独立 view，逐级同步 |
| Color | B8G8R8A8UNorm／framebuffer | 末级光栅输出，再转 Sampled 给 ImGui |

以上为实施基线，MRT 数量／格式在后端验证后可打包优化。compute 中间输出优先 RGBA16F，不假设 BGRA8 可写 storage。显存统计包括全部历史、mip、层数和探针双缓冲；超预算优先降低探针／阴影分辨率，不无声删效果。

### 7.2 PBR、阴影与环境光

材质使用 GGX 分布、Smith 可见性、Schlick Fresnel、受 Fresnel 和 metallic 抑制的漫反射；roughness 下限、分母 epsilon 避免 NaN。使用 split-sum IBL：漫反射 SH 或 irradiance cube、GGX 预滤波 specular cube、BRDF LUT；补上粗糙材质能量补偿和 normal variance 引导的高光抗锯齿。公式及数值细节参考 [Filament PBR 文档](https://google.github.io/filament/Filament.md.html)，不要复制 CornellBox 的常量半球 ambient 当作最终环境光。

太阳使用 4 级 CSM：按实际场景范围设 shadow distance，log/uniform 混合分割初值 λ=0.65，级联重叠约 10%，相机未抖动视锥拟合、光空间 texel snapping。先实现固定 PCF，再完成有界 blocker search + PCSS；bias 根据法线、斜率与 texel 大小调节，不能仅用大 bias 掩盖 acne。MASK 参与阴影，跨级联和低太阳角不允许断层。非太阳局部补光使用弱强度与空间范围限制，避免穿墙。

AO 默认半分辨率、视空间采样、双边滤波；从稳定 SSAO 做起，最终实现 horizon-based 多方向遮蔽并用固定样例与参考图比较，未经验证不要声称是完整 GTAO。可参考 [AMD CACAO 官方说明](https://gpuopen.com/fidelityfx-cacao/)的屏幕空间 AO 路径。AO 主要调制间接漫反射，镜面用独立 specular occlusion；不要整张最终图乘 AO，也不要把太阳阴影、材质 AO 与 SSAO 重复压黑。

室内层次使用天空漫反射 + 有限范围的低强度暖色补光／环境可见度区域。补光明确是美术近似，不宣称多次反弹 GI。最终必须有至少一组中庭／走廊局部反射探针：用简化 forward PBR 捕获场景，六面一致时间，禁用探针自采样、屏幕效果、tone mapping；prefilter 后按包围盒校正与空间权重混合。它们补充室内倒影，不能只让天花板反射蓝天。

拖时间时太阳和背景立即更新；缓存用版本号，探针每帧更新有限面，六面及 mip 全部完成后一次交换，不能暴露半新半旧 cube。初值目标松手后 0.5 秒内跟上，按硬件测量调节。探针切换时重置／降低相关时域历史权重。首版允许同步更新以验正确，最终记录停顿是否达标。

### 7.3 程序化天空、雾和美术方向

天空完全由 shader 生成，无外部天空 HDRI。先建立一个共享 `EvaluateSky(direction, sun, parameters)`：天顶／地平线渐变、受太阳角度影响的暖色地平线、太阳圆盘与 Mie 风格光晕；地平线以下平滑接地面色。它是美术化近似，不标称真实大气散射。天空背景和 IBL 共用函数；太阳直射单独计算，IBL 预滤波排除或控制极亮太阳圆盘，防止双算与 fireflies。

默认一天为美术时间：`t=(hour-6)/12`，高度角 `sin(πt)*65°`，方位角沿东西方向连续旋转；可增加固定方位偏移让阳光斜穿中庭。用归一化方向同时驱动阴影、BRDF、天空、雾。06/18 点直射平滑衰减，保留暖色天空和环境亮度；中午偏中性，早晚暖阳／冷阴影。不在 6 或 18 点归一化零向量。

雾用指数高度密度与太阳方向散射，限制最大不透明度。基线解析距离雾；高质量阶段增加半分辨率深度截断的 16–32 步体积积分，采 CSM 形成轻微光束、双边上采样，采样界限与噪声必须稳定。透明表面按自身深度应用同一雾，避免二次覆盖。Bloom 使用 soft knee、多级 downsample/upsample，只强调高亮；禁用默认景深、运动模糊、色差和重暗角，保留建筑细节。

曝光先固定，最终可增加限幅、平滑自动曝光；拖动时间依然要看出早晚差异，不能被自动曝光全部抵消。tone mapping 统一用 ACES 拟合风格曲线并轻微调色，不称为完整 ACES 色彩管理。现有 ImGui 为 Legacy、swap chain 为 UNorm：最终 Color 存显示编码值，仅在末级编码一次，验证 UI 与场景不双重 gamma。

## 8. 抗锯齿与 SGSR 集成细则

None／Spatial 的 TAA：使用无抖动矩阵生成 motion，有抖动投影栅格化；历史重投影、深度／法线拒绝、邻域范围裁剪、运动相关权重。历史包含相应深度，处理新露出表面；不要简单固定比例混帧。Temporal 仅运行 SGSR2，不再跑这条 TAA。

通过扩展的创建方法创建 `SpatialUpscaler`／`TemporalUpscaler`，阅读 `Extensions.cs` 确认签名。desc 输入输出尺寸固定，变化时销毁旧实例并重建。扩展内部管理自身历史，调用者负责外部 Input／Output 的 layout、usage 与 lifetime。

SGSR2 的契约以本仓库 `Sgsr2ConvertQuality.slang`、`Sgsr2Activate.slang`、`Sgsr2UpscaleQuality.slang` 为准：

| 参数 | 实施要求 |
| --- | --- |
| Input | 含透明的非负线性 HDR，SampledHandle |
| OpaqueInput | 同尺寸、同曝光、同天空／雾处理的透明前快照；无透明时才允许与 Input 相同 |
| Depth | 0 近 1 远的设备深度，不是线性距离；读取最小值找近表面 |
| MotionVectors | shader 解码为 NDC 的 current−previous；有效编码 `motion * (0.499 * 0.5) + 32767/65535`，不是原始 UV／像素速度 |
| Motion 无效值 | x=0 触发 ClipToPrevClip 相机回退；真正静止应编码到约 0.5。天空 motion 只考虑相机旋转 |
| ClipToPrevClip | 行向量约定 `inverse(currentUnjitteredViewProjection) * previousUnjitteredViewProjection`；用测试点核对 |
| JitterOffsetX/Y | 内部像素单位，Halton(2,3) 居中序列；结合 Y 方向做静态边缘测试，防止抖动重复计入 motion |
| PreExposure | 初版 1，正值；与 HDR 压缩／恢复一致，不直接塞摄影 EV |
| CameraFovAngleHor | 相机当前 Fov 为垂直角度，转换水平弧度 `2*atan(tan(vertical/2)*aspect)`，核对消费方 |
| MinLerpContribution | 从 0.05 起调，依据静态噪声／拖影对比确定 |
| SameCamera | 比较未抖动 view/projection，不能因 jitter 永远判 false |
| Reset | 首帧、切模式、resize/scale、相机切换/FOV突变、时间跳变、历史无效为 true |
| Output | 可写 HDR texture 的 StorageHandle；Dispatch 后转 Sampled |

该扩展未提供通用 reset 方法或单独 reactive mask 参数；不能编造 API。Quality 中 Input/OpaqueInput 的差值参与透明反应处理。现有 shader 有 ceil dispatch 与边缘采样，必须验证 1279×719、很小窗口和非 8 倍数尺寸，不能假设扩展已经覆盖所有边界。发现问题先写最小复现，应用层可做安全适配则局部处理；若必须改公共扩展，单独记录根因、影响面和验证，不手工编辑生成的 `.g.cs`。

需要真实重置所有相关历史：TAA、AO（若采用时域）、雾（若采用时域）、SGSR2。首帧 reset 分支不应依赖未初始化历史数值；如扩展行为不满足，记录并修复可复现问题。暂停最小化期间不推进帧序列，恢复首帧 reset。

## 9. 实施阶段与退出条件

每阶段交付可运行的增量、更新下表状态与证据，先通过本阶段再继续。开发授权后不为普通实现选择重复提问；只有缺少必需资产、权限或无法确定的关键约束时报告具体阻塞，继续独立工作。不能把临时替代路径标成最终完成。

| 阶段 | 工作 | 退出条件 | 状态 |
| --- | --- | --- | --- |
| M0 | 阅读代码；资产清单／固定版本；Renderer 最小清屏；设置传递与 Color 生命周期 | 首帧、resize、最小化恢复不崩溃；三个状态进入 Renderer | 待实施 |
| M1 | glTF 几何／材质、GBuffer、CPU culling、深度和法线调试 | 完整 Sponza、MASK／双面正确；包围盒／相机合理；非均匀缩放测试通过 | 待实施 |
| M2 | GGX、CSM+PCF、程序化天空、天空 IBL、tone mapping | 06/12/18 点光向一致，天空影响物体，金属有环境反射 | 待实施 |
| M3 | PCSS、AO、局部探针、可控补光、高光抗锯齿 | 柱脚接触、走廊亮度和反射有层次；无明显漏光／级联跳变 | 待实施 |
| M4 | 透明、雾／体积光、Bloom、曝光调色 | 三个固定机位达到美术基线，开关效果有可解释差异 | 待实施 |
| M5 | 内部 TAA、Spatial、Temporal、运动矢量与历史重置 | 静止稳定，平移／旋转无持续残影，三模式切换与 scale 正确 | 待实施 |
| M6 | 资源／绑定清理、异常路径、profiling、跨后端验证和运行说明 | 下节矩阵完成，可复现性能数据；平台缺口明示 | 待实施 |

调试模式放折叠区，主界面保留三个控件；提供 BaseColor、Normal、Roughness、Metallic、Depth、Motion、AO、Cascade、直接／间接光、HDR／曝光、超分前后对照。调试输出不进入时域历史；切回最终画面 reset。最终随文记录机位、朝向、时间、scale、模式、曝光与设备，不只贴一张最好看的截图。

## 10. 验证与交付

构建：仓库根目录运行 `dotnet build sources/Experiments/Sponza/Sponza.csproj`；本机依赖已还原时可用 `--no-restore`。运行使用 `dotnet run --project sources/Experiments/Sponza/Sponza.csproj`。Release 性能数据使用 Release 构建，shader 运行时编译成功也属于验收，C# 编译通过不能代替它。

有价值的自动验证：材质通道／颜色转换、CPU/GPU 常量偏移、矩阵重投影与 motion 编解码、太阳在端点无 NaN、分辨率计算、设置变化的历史失效。资源同步和画质必须在真实 GPU 验证；不要写只重复 slider 实现的测试。

| 验证组 | 必测组合／动作 | 合格标准 |
| --- | --- | --- |
| 基础画质 | 中庭全景、走廊阴影、材质近景；06/09/12/16.5/18 点 | 阴影有层次，材质可区分，天空／太阳／反射一致，无黑块、曝光截断或浮空阴影 |
| 分辨率 | 三模式 × 0.5/0.75/1.0；1280×720、1920×1080、1279×719 | 同视野、同 UI 尺寸；无边缘垃圾／越界；正确输出尺寸 |
| 时域 | 静止 120 帧、慢平移、急转、遮挡后露出、时间来回拖 | 静态细节收敛，无持久残影、抖动、透明拖尾或首帧闪烁 |
| 生命周期 | 连续 20 次模式切换／resize，最小化恢复，正常退出 | 验证层无新增错误，无释放后句柄访问、持续显存增长或异常 |
| 色彩 | 灰阶、纯色块、亮度 >1 的 HDR、高光 Bloom | 输入解码与输出编码各一次，Bloom／曝光不作用于 UI |
| 性能 | 三模式固定机位各运行 60 秒，预热后统计 | 记录 CPU/GPU 中位数、p95、显存、实际设备／分辨率／设置 |
| 后端 | DirectX12、Vulkan、Metal 在可用设备运行 | 标记实测／仅构建／未验证，不用一个后端结论代表全部 |

性能初始目标：在记录型号的开发 GPU 上，1080p 输出、0.75 scale Temporal Quality 争取 GPU 帧时 ≤16.7 ms；这是目标，硬件未知时不保证。先用 `BeginDebugEvent` 标注各 pass，再用真实 RHI timestamp／查询能力测量。保留同步等待时区分 CPU 等待和 GPU 时间。超预算按实测热点调整采样、阴影分辨率、探针频率与后处理，保持明确的高质量档。

最终交付：完整 Sponza 实验代码、资源来源／获取说明、所有 shader、复现命令、阶段状态、固定机位截图、性能表、已知限制和平台验证结果。默认路径不可留 `null!` 输出、空 Render 或 TODO 假实现。高级效果失败时记录根因与剩余工作，不以关闭效果宣告整项完成。

## 11. 当前变更记录

- 2026-09-05：仅补齐 App 中 Render scale、None/Spatial/Temporal、Time of day 控件及局部持久状态；未接入 Renderer。
- 2026-09-05：将超分状态改为 `Models/UpscalingMode.cs` 枚举，以 ImGui 下拉框选择，替代数字状态和单选按钮。
- Sponza 项目 `dotnet build --no-restore` 通过，0 警告、0 错误；Renderer 仍为空骨架，本轮不作运行画质验证。
- M0–M6 均未实施。本文件是后续开发输入；不要把本节构建结果当作未来渲染功能的测试证据。
