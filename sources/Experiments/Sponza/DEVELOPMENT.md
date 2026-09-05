# Sponza 光栅化 PBR 实验开发文档

本文是交给后续 agent 的实施规格。目标是在现有骨架上交付一个可运行、画面精致、参数真实生效的 Sponza 实验。按里程碑连续完成开发、验证和画面调整；只有编译成功、只有天空或只有贴图模型均不算完成。

文中路径如未另行说明，均相对于仓库根目录。标为“当前”的内容是已有代码；其余文件、接口扩展、默认画面和验收项均是待实现要求，不能据此宣称渲染功能已经完成。

**变更边界：保留现有宿主和框架逻辑。** 不修改 `ImGuiHelper` 的实现、签名、窗口位置或折叠行为；控件仍写在 App 的 `ImGuiHelper.Settings(Action)` 回调中。`RenderSettings` 使用普通结构体，Renderer 通过公开字段持有，由 App 初始化和编辑。保留 App 中 Color 的直接绑定、Update/Render/Resize 回调顺序、输入、交换链和提交逻辑，不添加空 Color 分支、占位提示或移动 renderer.Update 的调用。渲染实现应遵守这些已有接口；发现共享框架或扩展的问题时记录证据，在 Sponza 范围内处理，不将本文当作修改框架代码的授权。

## 1. 最终画面与范围

默认画面为 **16:00 的暖色斜阳中庭**：阳光穿过上层建筑，在地面和柱廊形成有节奏的明暗；阴影略冷但保留石材细节；织物、石材、金属有清楚的粗糙度差异；远处有很薄的空气感；天空有自然的天顶、地平线、太阳和疏薄云层。最终按实际模型方向调整太阳方位和相机构图。

视觉优先级：材质与色彩正确 → 光影构图 → 阴影与间接光层次 → 抗锯齿及运动稳定 → 克制的反射、体积光和后期。允许艺术化光强、补光、材质微调和天空色彩，不追求照度标定或严格能量守恒；不能用全局过曝、过强 AO、镜面地板或浓雾掩盖问题。

必须交付：

- Sponza glTF 静态场景、完整金属度/粗糙度 PBR、法线贴图、正确的镂空材质。
- HDR 前向渲染、四级稳定级联阴影、PCSS 软阴影、GTAO、程序化天空与同源 IBL。
- 由光栅化生成的局部环境探针，提供室内反射和近似间接光；少量受控艺术补光。
- 低分辨率阴影体积雾、Bloom、曝光、色调映射，以及可切换的空间/时域超分。
- 原生分辨率也具备抗锯齿；鼠标移动、尺寸变化和改参数时画面稳定。
- 可复现的画质预设、调试视图、截图及验证记录。

全程禁止 BLAS/TLAS、RayQuery、光追管线、路径追踪和对三角形/BVH 的软件射线求交。不依赖 `RayTracingSupported` 或 `MeshShadingSupported`。AO、体积积分和可选 SSR 只访问光栅化生成的深度/颜色纹理，不使用场景光追。SSR 属于后续可选增强；没有它也必须依靠局部探针交付完整反射。

暂不包含：通用游戏引擎/ECS、编辑器、场景动画/蒙皮、完整 glTF 扩展集合、实时多次反弹 GI、路径追踪烘焙、体积云天气系统、HDR 显示器输出。实现选择不明确时按本文默认方案推进，不另起架构。

## 2. 仓库现状与约束

| 项目 | 当前事实与实施约束 |
| --- | --- |
| 项目入口 | `sources/Experiments/Sponza/Sponza.csproj` 已加入 `Zenith.NET.slnx`，不要重复建项目 |
| 运行方式 | `Program.cs → App.Run()`；沿用 Silk.NET 窗口/输入、Zenith.NET、ImGui |
| 框架/依赖 | `sources/Directory.Build.props` 为 net10.0；包版本由两级 `Directory.Packages.props` 管理 |
| 已引用 | SharpGLTF.Core、ImageSharp/ImGui/Upscaling 扩展、DirectX12/Metal/Vulkan 后端 |
| 后端选择 | Windows → DirectX12；macOS → Metal；Linux → Vulkan/Xlib，沿用现有选择 |
| 资源路径 | `Assets/**` 已复制到输出目录；从 `AppContext.BaseDirectory` 定位，不依赖启动工作目录 |
| renderer | 保留 `Update(CameraHandler)`；`public RenderSettings Settings;` 由 App 控制；Render/Resize/Dispose 待实现 |
| 设置 | `Models/RenderSettings.cs` 为普通 struct，成员为公开字段；初值和控件范围由 App 管理 |
| 骨架状态 | `Color => null!` 仍是原有占位实现，当前不能据编译成功宣称窗口可运行；后续由 Renderer 创建有效 Color，不改变 App 的直接绑定 |

先阅读 `Sponza/App.cs`、`Renderer.cs`、`Helpers/ImGuiHelper.cs`、`Models/RenderSettings.cs`；参考 `CornellBox/Renderers/RasterizationRenderer.cs` 的基础绘制、`FluidTank/Helpers/GraphicsHelper.cs` 的资源辅助方法。可以提取适用做法，但不要复制 FluidTank 的光追反射或庞大的单文件结构。

必须核对真实接口：

- `sources/Zenith.NET/CommandBuffer.cs`：`BeginRenderPass`、`Transition`、`Barrier`、`CopyTexture`、`Dispatch`、`WriteTimestamp`。
- `sources/Zenith.NET/ZenithCompiler.cs`：`CompileFromFile` 使用 row-major，实验着色器写 Slang。
- `sources/Zenith.NET/Structs/`：纹理、附件、视图、采样器、深度和混合状态。
- `sources/Extensions/Zenith.NET.Extensions.Upscaling/`：两个 upscaler 的 Desc、Args 及对应 Slang。
- `sources/Extensions/Zenith.NET.Extensions.ImGui/`：绑定缓存和 `ImGuiColorSpace.Legacy`。

当前 `Capabilities` 只有设备名、光追和 Mesh Shading 标志，没有格式支持查询，也没有现成 RenderGraph、自动 mip 生成或自动资源状态跟踪。不要调用想象中的 API。格式/后端限制通过小规模真实资源与管线验证确认；优先在本实验中适配，不能擅自修改 Zenith.NET、共享扩展或其他实验。

## 3. 项目结构和职责

保留现有层级，在其下按功能增加文件；不要额外创建引擎项目、服务容器或抽象框架。以下是目标文件分工，可将同一 Pass 的紧密相关子步骤放在同一个文件中。

```text
sources/Experiments/Sponza/
├── Program.cs
├── App.cs
├── Renderer.cs
├── Sponza.csproj
├── DEVELOPMENT.md
├── README.md                         # 最终运行、操作和已验证结果
├── Handlers/
│   ├── CameraHandler.cs
│   └── ImGuiHandler.cs
├── Helpers/
│   ├── CocoaHelper.cs
│   ├── ImGuiHelper.cs                # 保持原有窗口包装；控件留在 App 回调
│   ├── GraphicsHelper.cs             # 本实验真正复用的创建/上传/编译方法
│   ├── GltfHelper.cs                 # glTF 解析、节点实例与 CPU 场景数据
│   ├── TextureHelper.cs              # 按用途加载、mip、默认纹理
│   └── ScreenshotHelper.cs           # 按需回读，处理行跨度/通道顺序
├── Models/
│   ├── RenderSettings.cs
│   ├── UpscalingMode.cs
│   ├── QualityPreset.cs
│   ├── DebugView.cs
│   ├── Scene.cs                      # 场景与静态 GPU 资源所有权
│   ├── Mesh.cs
│   ├── Material.cs
│   ├── FrameData.cs                  # 相机、尺寸、矩阵、太阳和帧编号
│   └── EnvironmentProbe.cs
├── Passes/
│   ├── ShadowPass.cs
│   ├── DepthPrepass.cs
│   ├── AmbientOcclusionPass.cs
│   ├── SkyPass.cs
│   ├── EnvironmentPass.cs            # 天空 IBL、探针捕获/预过滤
│   ├── ForwardPass.cs                # opaque/mask + 单独透明阶段
│   ├── VolumetricFogPass.cs
│   ├── AntiAliasingPass.cs           # None/Spatial 路径的 FXAA
│   ├── UpscalingPass.cs              # 适配已有 SGSR 扩展
│   ├── BloomPass.cs
│   └── ToneMappingPass.cs
└── Assets/
    ├── Fonts/msyh.ttf
    ├── Models/Sponza/                # glTF、bin、贴图、来源和授权原文
    └── Shaders/
        ├── Common.slang
        ├── Material.slang
        ├── Pbr.slang
        ├── SkyCommon.slang
        └── 各 Pass 对应的 .slang 文件
```

`App` 管窗口、输入、UI、交换链和提交，沿用已有流程，并直接控制 `renderer.Settings`。`Renderer` 负责资源尺寸、变更判定、每帧数据和固定 Pass 顺序；在原有 `Update(CameraHandler)` 中读取 Settings，不新增设置传参或只读属性包装。它向 App 暴露的 GPU 资源只有最终 `Color`，统计信息可通过普通值类型单独提供。`Scene` 拥有常驻模型资源；Pass 只拥有自己创建的资源，输入资源为借用，不重复释放。

Pass 按需提供构造、`Update`、`Render(CommandBuffer, ...)`、`Resize`、`Dispose`；不为了统一形式添加空实现基类。每个 Pass 文件尾部可使用 `file struct` 声明显式布局 GPU 常量，沿用现有实验的 `[StructLayout]`/`[FieldOffset]` 风格。C# 与 Slang 的偏移、大小、句柄宽度必须逐项一致，尤其不能假设 `float3` 紧密连续排列。

代码使用文件作用域 namespace、四空格、private camelCase 字段、PascalCase 方法/属性、target-typed `new`。公共设置不直接变成 GPU 常量结构，禁止 shader 参数和 ImGui 临时变量相互混用。仅为重复使用的操作抽取 helper，保留可以直接阅读的渲染顺序。

## 4. 模型资产和材质导入

默认使用 [Khronos glTF Sample Assets 的 Sponza](https://github.com/KhronosGroup/glTF-Sample-Assets/blob/main/Models/Sponza/README.md) 版本，目标入口为 `Assets/Models/Sponza/glTF/Sponza.gltf`。该版本已有 PBR 贴图和切线，适合现有 SharpGLTF.Core 依赖。官方预览中的灯光不属于模型本身，必须自行布光。

资产阶段按下面顺序执行：

1. 检查用户是否已放入可用 glTF/glb；有则优先使用并记录路径。否则从上述仓库取得 `Models/Sponza` 及其引用的授权文件，保留 glTF URI 对应的相对目录；可用稀疏检出，避免下载整个模型库。
2. 在 `Assets/Models/Sponza/SOURCE.md` 记录实际来源、获取日期、仓库完整 commit SHA、文件清单/校验值和复制的授权文件位置；不编造固定版本号，不把资产许可自动当作本仓库代码许可。
3. 删除模型目录占位文件，核对不是 Git LFS 指针，并确认 bin 和所有贴图存在。资源取得失败时给出缺失路径和原因，继续独立 Pass 工作，但资产验收保持未完成。
4. 实现可选 `SPONZA_MODEL` 环境变量覆盖默认入口，支持绝对路径；最终 README 给出用法。发行或运行无需联网获取贴图。
5. 加载失败提供包含资源路径的明确错误，不改写 App 的显示/异常流程；不能静默以测试立方体替代最终场景。

通过 `SharpGLTF.Schema2.ModelRoot` 读取默认场景及节点树。访问 accessor 使用库接口，不自行猜测 buffer 的紧密排列；同一个 Mesh 的每个 Primitive 都有独立材质，且可能被多个节点实例化。库用法查 [SharpGLTF 源码](https://github.com/vpenades/SharpGLTF/tree/master/src/SharpGLTF.Core/Schema2)，签名以项目固定的包版本为准。

必须处理：节点累计变换、非均匀缩放的逆转置法线矩阵、负行列式的绕序/切线手性、索引类型到统一 UInt32 的转换、无索引 primitive、POSITION/NORMAL/TANGENT/TEXCOORD_0，以及材质实际引用的 TEXCOORD_1。缺失法线则按三角形生成；缺失切线则生成与法线贴图兼容的切线并处理退化 UV。静态实例优先共享几何，通过 draw 数据提供世界矩阵。只要求 triangle primitives；不支持的必需扩展或拓扑应明确报错，不能悄悄丢掉几何。

保持 glTF 的米单位、Y-up 和场景比例；不要把从旧 OBJ 示例看到的 `0.01` 缩放无条件套在 glTF 上。计算 AABB 后设置相机 near/far、移动速度、初始位置和灯光范围。第一视角约离地 1.6 m，沿中庭长轴斜向远处看，同时保留部分天空。

材质语义遵循 [glTF 2.0 规范](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html)：保留贴图与 factor 的乘积、纹理坐标选择、sampler、normal scale、occlusion strength、alpha mode/cutoff 和 double-sided；不要因画面偏暗而删除资产原有的 baseColorFactor。材质调试视图先验明颜色空间，再调灯光。

| 贴图用途 | GPU 采样与 mip 要求 | 缺失时 |
| --- | --- | --- |
| Base color / emissive | sRGB RGB 解码至线性，alpha 仍为线性；颜色 mip 在线性空间平均再编码 | 白色 base color / 黑色 emissive，保留 factor |
| Metallic / roughness | 线性；G=roughness、B=metallic | 通道值 1，与材质 factor 相乘 |
| Normal | 线性切线空间；采样后解码、归一化；mip 重新归一化并关注高光闪烁 | `(0.5, 0.5, 1)` |
| Occlusion | 线性 R；只影响间接光，避免与 GTAO 重复压黑 | 1 |
| Alpha mask | 与主材质使用同一 alpha/cutoff；mip 保持覆盖率 | 使用材质规则 |

现有 `LoadTextureFromStream` 创建 `R8G8B8A8UNorm` 并直接对原图缩放，不满足全部颜色/mip 语义。在 Sponza 的 `TextureHelper` 中按用途创建 `R8G8B8A8SRgb` 或 UNorm 纹理并上传每级 mip，不修改共享 ImageSharp 扩展。缓存键至少包含图像身份和颜色空间/用途；同一图像兼作数据图时不能错用 sRGB。支持内嵌图像和相对 URI，不依赖文件名猜材质用途。默认使用三线性过滤和 8× anisotropy，按 glTF sampler 的 wrap 方式调整。

OPAQUE/MASK 参与阴影、预深度和主渲染；MASK 的裁剪逻辑放进共享 shader，三个阶段一致。双面材质正确处理背面法线，不全场景关闭背面剔除。BLEND 使用单独的深度只读前向阶段，按距离由远到近，采用明确一致的 straight-alpha 混合；不要把叶片/旗帜的 MASK 当作 BLEND。

## 5. 坐标、深度、颜色和尺寸约定

这些约定先用小场景验证，然后固定，禁止不同 Pass 自行修正符号。

- C# 使用 `System.Numerics`，行向量；`ViewProjection = View * Projection`，Slang 使用 `mul(position, matrix)`。现有 compiler 已设置 row-major，不额外转置上传矩阵。
- 主相机沿用 `CameraHandler` 的右手视图/透视矩阵。主深度采用普通 Z：near=0、far=1、clear=1、LessEqual；阴影也使用普通 Z。第一版不引入 reversed-Z，以匹配当前 SGSR 2 shader 的深度约定。
- 定义统一的屏幕 UV 左上原点和 `ndc → uv = (x * 0.5 + 0.5, 0.5 - y * 0.5)`；后端差异集中在公共投影/坐标 helper。用坐标轴、棋盘和静止重投影分别确认 D3D12/Metal/Vulkan，不能凭经验给所有 Vulkan 矩阵额外翻 Y。
- 光照、天空、雾、反射、时间累积均在线性 HDR 中完成。最终只做一次曝光和 tone mapping，再一次线性到 sRGB 编码。
- 保持 `ImGuiColorSpace.Legacy` 和 `B8G8R8A8UNorm` 交换链时，`Renderer.Color` 存放已经编码为 sRGB 的 LDR 数值，ImGui 原样合成。不要同时换成 sRGB 输出附件又手动 gamma，避免双重转换。
- 纹理/视口大小使用 **framebuffer 像素**；UI 用逻辑尺寸。相机比例采用输出尺寸比例，不随 renderScale 或线程组补齐发生变化。
- `renderWidth = max(1, floor(outputWidth * RenderScale))`，高度同理。`Color` 始终为输出尺寸。线程组数量向上取整，但实际图像尺寸不向 8 对齐；所有 compute 写入检查线程边界，邻域 Load 单独 clamp。
- 0×0 的最小化窗口不创建资源、不提交渲染，不推进 jitter/history；恢复后重建尺寸资源并重置历史。初始化时主动使用当前 framebuffer 尺寸，不等第一次 Resize 事件。

预深度输出一张 `R32Float` 的 DeviceDepth（和硬件深度相同的 0..1 值），让屏幕空间算法无需依赖深度/模板格式采样差异。线性视距按当前投影参数从 DeviceDepth 重建，不把普通 Z 值直接当米数。另输出 view-space normal/roughness 和无 jitter 的运动向量。各数据的具体布局见下一节。

## 6. 固定渲染顺序和资源

采用 **预深度 + HDR Forward**。Sponza 的灯光数量可控，先用一个太阳和最多 8 个局部补光，不引入 deferred G-buffer/clustered light culling。CPU 做视锥裁剪、材质排序和静态实例复用；额外优化须有性能证据。

保持 App 现有回调顺序：Update 中更新 UI/相机、直接绑定 Color、编辑设置；Render 中调用 renderer.Update(camera)、renderer.Render、UI/Present。以下是 Renderer 内部每个实际渲染帧的处理顺序，不要求移动 App 调用：

1. 消费最新设置与 framebuffer 尺寸，安全地重建必要资源；准备相机、太阳、当前/上一帧矩阵和 jitter。
2. 更新太阳 CSM；如环境变脏，执行本帧预算内的天空 IBL/探针离屏捕获与过滤。
3. DepthPrepass：OPAQUE/MASK，写硬件深度、DeviceDepth、normal/roughness、motion。
4. AO：线性深度 mip → GTAO → 深度/法线引导降噪与上采样。
5. Forward opaque：直射 PBR + CSM + 天空/局部探针间接光 + AO + emissive，写 HDR。
6. Sky：只覆盖背景深度，使用同一太阳方向和天空函数。若实现 SSR，在此后用独立颜色副本合成，不能边采样边写同一 HDR 纹理。
7. Volumetric fog：计算散射和透射率，双边上采样，合成到独立 HDR 目标。
8. 保存 **OpaqueInput**（已含天空与雾）；Forward transparent 写另一个场景目标，并在透明表面深度计算一致的雾。没有透明物体可令 Input/OpaqueInput 引用同一有效纹理。
9. 按下面的抗锯齿/超分分支处理，得到输出尺寸的最终 LDR `Color`。
10. `Color` 转为 Sampled；App 绘制 ImGui 到交换链，再 Present。UI 不参加曝光、AA 或超分。

| 模式 | 色彩管线顺序 | jitter |
| --- | --- | --- |
| None | 内部分辨率 HDR → Bloom → 曝光/tone map/sRGB → FXAA → 必要时双线性放大到 Color | 0 |
| Spatial | 内部分辨率 HDR → Bloom → 曝光/tone map/sRGB → FXAA → SGSR 1 → Color | 0 |
| Temporal | 内部分辨率 HDR + opaque/depth/motion → SGSR 2 Quality → 输出分辨率 HDR → Bloom → 曝光/tone map/sRGB → Color | 每个渲染帧一次 |

这里 `None` 只表示不调用 SGSR；renderScale 仍有效，低于 1 时必须显式重建到输出尺寸。`Temporal + scale=1` 用作高质量原生分辨率时域抗锯齿，不再叠加 FXAA 或另一套 TAA。当前 SGSR 1 shader 含 0..1 clamp，因此必须放在 LDR 阶段；不能与 SGSR 2 一样直接喂 HDR。为三条路径提供相同的曝光和 Bloom 观感，切换时不产生突兀亮度差。

建议资源表（R=内部分辨率，D=输出分辨率；均为单采样）：

| 资源 | 格式/尺寸 | 用途/寿命 |
| --- | --- | --- |
| HardwareDepth | `D32FloatS8UInt`，R | 深度附件；尺寸变化重建，必要时验证 D32Float 替代 |
| DeviceDepth | `R32Float`，R，背景 1 | ColorAttachment + Sampled，供 AO/雾/SGSR |
| NormalRoughness | `R16G16B16A16Float`，R | view normal.xyz / perceptual roughness，背景 neutral |
| Motion | `R16G16Float`，R | 无 jitter 的 current NDC - previous NDC |
| EncodedMotion | `R16G16Float`，R | SGSR 编码；只在 Temporal 分支需要 |
| ShadowMap | `D32Float`，4 层 2D array | 每层 2048²；DepthStencilAttachment + Sampled |
| LinearDepthPyramid / AO | `R32Float` mip / `R16Float` 半分辨率 | Sampled + Storage；AO 禁用时提供全 1 默认纹理 |
| HdrA / HdrB / OpaqueInput | `R16G16B16A16Float`，R | HDR 合成 ping-pong；分清 CopySrc/CopyDst 与 Storage/附件状态 |
| FogScattering / FogTransmittance | RGBA16F / R16F，半分辨率 | 积分结果，必须带深度引导上采样 |
| Sky/Probe cubes | `R16G16B16A16Float`，6 面及 mip | 静态尺寸；按环境变更更新，不随窗口重建 |
| BRDF LUT | `R16G16Float`，256² | 启动生成一次；无效句柄不能代替缺省数据 |
| Temporal HDR output | `R16G16B16A16Float`，D | SGSR 2 输出；历史由扩展实例拥有 |
| Bloom chain | `R16G16B16A16Float`，所处分支逐级缩小 | 独立上下采样目标，最小尺寸至少 1 |
| Color | `R8G8B8A8UNorm`，D | 已编码 LDR；Sampled，加上实际写入方式所需 usage |

这里的格式是实施起点，不代表所有硬件组合已验证。资源创建失败应指出具体 format/usage/backend，并使用经过验证的替代格式；不能用宽泛 catch 静默关闭整个效果。

资源与同步规则：

- 每张纹理（含 array layer、mip）记录实际状态。当前 `Transition` 接收一个 `TextureSubresource`，`default` 只定位默认子资源；cube/array/mip 要逐个过渡，不能当作自动覆盖全部。
- `Undefined` 仅用于新建或明确丢弃内容的目标，不用于有待保留数据的历史、探针或上一阶段输出。关闭效果时绑定确定的 neutral 资源，不采样未写入的纹理。
- compute 写入后的读取/再写入按实际依赖加入 `Barrier` 和布局过渡；同一子资源不能在同一阶段作为附件/UAV 又被采样。Depth prepass 与 Forward 重用深度时保持一致的顶点变换、jitter 和 MASK 裁剪。
- 每级阴影、每个 cube face 和每个 draw 所需的常量必须拥有独立缓冲区或正确对齐的不同 offset；不能反复覆盖同一常量内存后假定已经记录的 draw 会保留旧值。
- 第一版沿用一条 GraphicsQueue 记录图形和 compute，及 App 的 `Submit().Wait()`。不在每个 Pass 中 Submit/Wait；上传批量提交。以后取消逐帧等待时才引入帧资源轮转，不能提前重用仍在 GPU 使用的内存。
- 输出 Color 在 Renderer 构造/原有 Resize 入口中按输出尺寸创建；内部 renderScale 变化只重建内部目标，不替换已经被 App 绑定的 Color。Renderer 在原有 Update 中处理设置变化，内部延迟回收仍可能被 UI/GPU 引用的资源，不为重建移动 App 的 Binding/Update 时机。
- `Renderer` 构造成功后必须提供有效且非空的 Color，不修改 Color 的非空类型合同，不在 App 加 null 检查。Dispose 逆序释放所拥有资源，局部初始化异常也要释放已创建部分。

## 7. 画质实现规格

### 7.1 PBR 和材质细节

BRDF 用 GGX 分布、Smith visibility、Schlick Fresnel、金属度工作流；roughness 是感知粗糙度，转换为 alpha 时平方且限制最小值，避免高光奇点。非金属 F0 从 0.04 起步，金属 F0 来自 base color；反射方向、法线空间和视线方向统一。加入高粗糙度能量补偿和基于法线变化的 specular AA，先检查金属球/粗糙度球阵列，再进入 Sponza。参考 [Filament 材质和 IBL 说明](https://google.github.io/filament/main/filament.html)。

直射、diffuse IBL、specular IBL、局部探针和 emissive 保持可独立查看。AO 作用于间接项，不乘到太阳直射、天空背景或 emissive；材质 occlusion 与屏幕 AO 避免简单连续相乘造成黑边。用受控融合（如取较强遮蔽）并保留强度开关。roughness mip、normal mip 和适度 specular AA 共同处理地面/织物远处闪烁，不能仅靠强 TAA 模糊。

### 7.2 太阳与稳定软阴影

`TimeOfDay` 是艺术化白昼时钟，范围 6..18，不接入真实日期经纬度。定义 `a = pi * (hour - 6) / 12`，初始 surface-to-sun 方向为 `(cos(a), sin(a), 0)`，再绕 Y 轴旋转 `SunAzimuth`；太阳投射方向为其相反数。6/18 点方向保持有限，光强平滑趋近 0；正午接近上方，默认 16 点斜射。颜色在低高度暖色与高高度中性日光间平滑变化。调整 `SunAzimuth` 使光影横跨中庭，在预设里保存实际值。

CSM：4 cascades；以未 jitter 的相机视锥计算分割，log/uniform 混合 lambda=0.7 起步，shadow distance 60 m 起步并根据场景 AABB 校准。每级固定球包围、正交投影并按 shadow texel 对齐；计算 caster 范围时包含视锥外会投进来的遮挡物。分割边界约 10% 区域平滑混合，远端淡出。

先完成固定 PCF，再做 PCSS 的 blocker search 和可变半影；High 起点为 16 blocker + 32 filter samples，限制最大半影半径。投射器为方向光，使用光空间距离/太阳角半径推导尺度，不照搬透视光的深度比值。材质遮罩参与 shadow pass。bias 结合 slope/normal offset、级联 texel 世界尺寸调节，禁止用超大 bias 隐藏 acne。PCSS 原理参考 [NVIDIA 技术文档](https://developer.download.nvidia.com/assets/gamedev/docs/PCSS_Integration.pdf)。

验收：静止时阴影不游泳；缓慢平移时无明显级联跳变；柱脚有接触感；悬空物体半影更宽；叶片/布帘影子无矩形底板。必须提供 cascade index、shadow visibility 和单级 shadow map 调试视图。

### 7.3 程序化天空与环境光

第一版最终天空采用可调的解析模型：天顶/地平线色彩梯度、Rayleigh 风格散射、前向 Mie 风格光晕、有限角半径太阳盘及 2D 程序噪声薄云。不要求大气物理精确；禁止用下载 HDRI 充当程序化天空。Hillaire 的 [天空实现](https://github.com/sebh/UnrealEngineSkyAtmosphere) 可作为方向参考，完整大气 LUT 是可选升级，不能卡住主线交付。

`SkyCommon.slang` 定义唯一的 `EvaluateSky(direction, skyParameters, includeSunDisk)`，屏幕天空和 cube 捕获共享。输入为去除相机平移后的世界方向；太阳盘边缘用像素导数平滑，地平线下方给出有限 ground color，不允许除以接近 0 的方向分量。薄云用世界方向映射到高空平面，固定种子、少量 octave，日照侧与背光侧略有明暗；默认静止或极慢移动，避免每帧重建整套 IBL。

天空 IBL：512² HDR cube 起步，漫反射卷积为 32² cube 或二阶 SH（二选一，默认 cube），镜面为完整 GGX 预过滤 mip chain，BRDF LUT 256²。GGX 预过滤按 roughness 重要性采样，不能用普通颜色 mip 冒充粗糙度积分。采样朝向、六面接缝和最粗 mip 必须验证。IBL 捕获关闭太阳盘，太阳直接照明由 directional light 负责，避免同一锐利太阳被重复积分。

时间/天空参数变化时太阳、天空和阴影同帧更新；昂贵 IBL 分批生成到备用资源，完成全部面与 mip 后整体切换，不能混用不同时间的半套结果。拖动参数时节流，停止拖动后目标 0.5 s 内开始收敛；若目标设备无法在该预算完成，显示更新中状态并记录实测。BRDF LUT 不随时间/尺寸重建。

### 7.4 局部环境探针与艺术补光

单独的天空 IBL 会让拱廊像露天一样发亮。最终至少设置中庭、左拱廊、右拱廊三个局部 probe，以场景 AABB 和真实墙体位置确定中心与影响盒。在调试视图中显示 probe 位置、盒范围和混合权重，最终保存为本场景数据。

每个 probe 从中心光栅化 6 面、捕获场景 HDR，然后分别生成 diffuse convolution 和 GGX specular chain。默认每面 256²；捕获使用太阳直射、对应阴影和天空 IBL，不读取正在生成的局部 probe，也不带主相机的 AO、雾、后期、UI，避免递归反馈。若主相机 CSM 不覆盖捕获视野，使用覆盖场景的独立 capture shadow map，不套用主相机级联选择。

diffuse probe 作为近似一次反弹和艺术间接光；specular probe 使用 box projection 和影响体积混合。以房间/拱廊盒约束贡献，必要时保存 capture depth 做可见性降权，避免穿墙混合；probe 范围外回退到天空。局部 probe 与 sky specular 按权重混合而不是全量相加。

时间变化后按完整环境快照版本更新 probe，双缓冲切换并平滑过渡；静态相机移动无需重新捕获。可加最多 8 个范围受限的点光/近似面光作为艺术补光，默认贡献小，提供独立开关。不能通过全屏常量 ambient 把阴影整体提亮。

### 7.5 AO、体积雾和可选 SSR

AO 默认 GTAO：半分辨率，线性深度 mip，High 从 3 slices × 每侧 3 samples 起步，世界半径约 0.75 m，深度/法线引导空间降噪并双边上采样。无时域模式使用固定采样旋转，Temporal 模式才使用逐帧变化噪声并让 SGSR 稳定最终图像；不要为第一版再增加独立 AO 历史链。参考 [XeGTAO 的算法和降噪实现](https://github.com/GameTechDev/XeGTAO)，移植源码时保留其许可，不能照搬示例工程的后端。强度 1 起步；验收重点是接触层次、细节保留和轮廓无光晕。

雾默认半分辨率 32 steps，按视线深度积分指数高度密度、太阳散射与 CSM 可见性，输出散射 S 和透射率 T，按 `scene * T + S` 合成。最大积分距离受相机 far 和 fog distance 限制；使用抖动减轻条带，空间双边滤波保持柱子轮廓。透明材质按其自身深度应用相同的雾模型，不能直接使用已积分到后方不透明表面的值。默认雾密度低，逆光才出现柔和光束，面板设为 0 时有完全无雾的 neutral 路径。

SSR 为全部必需里程碑完成后的可选项：基于深度金字塔和当前帧独立 HDR 颜色副本，按 roughness、边缘、厚度和命中可信度与 probe 反射混合。只替换对应 specular 间接项，不能把整幅 scene color 当纯反射再全量相加。无命中、屏幕外和粗糙表面回退 probe；不追踪三角形，不以黑色填失败区域。出现明显拖影或轮廓漏光时默认关闭，记录为可选增强未完成。

### 7.6 后期

默认固定手动曝光，`exposure = exp2(ExposureEv)`；第一版 HDR/时域历史不预乘该艺术曝光。Bloom 在 tone mapping 前处理线性 HDR，软阈值、最多 5 层 downsample/upsample，控制 firefly，强度从 0.04 起步，不扩散暗处亮度。

选择一种固定 filmic tone mapper（默认 ACES fitted 近似），保留高光滚降和材质色彩；说明其为显示近似，不宣称完整 ACES 色彩管理。饱和度从 1 起步，提供很轻的暖高光/冷阴影调色。自动曝光、景深、镜头畸变、暗角和锐化都是可选；默认关闭会掩盖材质与空间细节的效果。每个开关应有单独 neutral 输出路径，不能只改变 UI 标签。

## 8. 时域与超分适配合同

复用 `App.Context.CreateSpatialUpscaler(...)` 和 `CreateTemporalUpscaler(...)`；Temporal 默认 `TemporalUpscalerMode.Quality`。Desc 包含 input/output 尺寸且实例没有 Resize API，所以尺寸或相关模式变化时 Dispose/重建实例。`UpscalingMode` 是实验的路径选择，不要与扩展的 Speed/Quality 混用。只有通过下面验证后，才把最终 High 默认模式改为 Temporal。

`UpscalingPass` 负责适配，不要求其他 Pass 了解 SGSR 的私有编码。以下合同来自仓库当前 shader，实施时重新核对这些文件而不是从第三方引擎复制常量：

- `Shaders/Sgsr1.slang`
- `Shaders/Sgsr2ConvertQuality.slang` / `Sgsr2ConvertSpeed.slang`
- `Shaders/Sgsr2Activate.slang`
- `Shaders/Sgsr2UpscaleQuality.slang` / `Sgsr2UpscaleSpeed.slang`

| `TemporalUpscalerArgs` 字段 | 应提供的数据 |
| --- | --- |
| Input | 当前帧线性 HDR，已经合成天空/雾/透明，尚未 Bloom/tone map/UI |
| OpaqueInput | 同一帧、同一 HDR 标度、同一尺寸，在透明合成前保存；用于反应性估计，不可无条件填黑 |
| Depth | R32Float 普通 device depth，0 near / 1 far；不传线性米深度或 reversed-Z |
| MotionVectors | 下述专用编码纹理；不能直接传原始 UV/pixel velocity |
| Output | D 尺寸 RGBA16F Storage 句柄，之后显式转换供采样 |
| JitterOffsetX/Y | 当前帧输入像素单位的偏移，非 UV/NDC，和实际投影偏移一致 |
| ClipToPrevClip | 当前 clip 到上一实际渲染帧 clip 的行向量矩阵；专用于未编码像素的回退 |
| PreExposure | 第一版固定 1，HDR 不预曝光；未来更改必须同步整个历史标度 |
| CameraFovAngleHor | 从垂直 FOV 与实际输出 aspect 计算水平角，单位弧度；用实际 shader 行为验证 |
| SameCamera | 比较未 jitter 的视图、投影和相机状态；每帧 jitter 不应把静止相机判成移动 |
| MinLerpContribution | 从 0.05 起步，通过运动与细节对比调节，不作为隐藏拖影的万能参数 |
| Reset | 首帧以及确实不再兼容的历史时为 true，下一有效帧恢复 false |

主场景 motion 定义为 `currentUnjitteredNdc.xy - previousUnjitteredNdc.xy`。转换到 SGSR 编码为 `encoded = motion * (0.499 * 0.5) + 32767.0 / 65535.0`，即当前解码函数的逆；有效 motion.x 编码必须大于 0。静止有效表面的编码约为 0.5，**不是清零**；保留 0 表示回退，通过固定相机与已知平移的小场景验证符号和尺度。大幅 camera cut 直接重置历史，不用超范围运动向量修补。

重投影应满足 `previousUv = currentUv + (-0.5 * motion.x, 0.5 * motion.y)`。背景天空只含旋转运动，不用近距离几何深度伪造平移视差。优先为场景与天空都输出显式编码 motion。若使用零 motion 触发扩展的矩阵回退，令 C/P 为当前/上一帧未 jitter 的 ViewProjection，J 为当前帧 clip-space jitter 矩阵，使用 `inverse(C * J) * (P * J)`：重建和目标采用同一个当前 jitter，让 shader 的 `Position.xy - PreScreen` 消掉 jitter，只保留相机运动。这里不能把上一帧自己的 jitter 带入 P，也不能只撤销当前 jitter 后直接投影到未 jitter 的 P；静止相机跨整个序列时回退 motion 必须为 0。天空仍使用只含旋转的显式 motion。

使用 Halton(2,3) 的 8 帧确定性序列，减 0.5 得到输入像素偏移。UV 左上约定下，投影 NDC 偏移为 `(2*jx/renderWidth, -2*jy/renderHeight)`。将它加入行向量投影所需位置前先做点投影验证，不能照搬列向量矩阵下标。保存当前/上一帧未 jitter 矩阵及当前 jitter 矩阵；上一帧状态与序列 index 只在实际渲染/提交之后推进，多个 Update 或最小化不能额外推进。

Reset 条件：首次渲染、render/output 尺寸变化、None/Spatial/Temporal 切换、相机瞬移或 FOV/投影变化、模型重载、调试视图切换，以及太阳/材质/probe 出现大幅不连续变更。普通相机连续移动不 Reset。第一版用户拖动时间可每次有效变化 Reset，松手后继续累积；后期再按实测减小清空频率。曝光只在 temporal 之后应用时，无需因微小 EV 改变重建资源。

集成前必须验证两个当前扩展的边界问题。只读审计共享扩展；需要修正算法时在 Sponza 的 Pass/Shader 中局部适配并保留来源许可，不直接修改扩展源文件或生成文件：

1. SGSR 2 的现有 compute shader 使用向上取整 Dispatch，但部分入口缺少边界 early return，邻域 Load 也可能跨图像边缘。对输入和输出两种尺寸分别核查入口、Gather/Load 及坐标计算；必要时由本实验的适配 Pass 编译带有边界保护的局部 shader，不把窗口强制为 8 的倍数。
2. 首帧/重建后 history 与 luma texture 当前仅做布局初始化。核对 Reset 分支是否仍读取未定义数据；若需要局部实现时域 Pass，由本实验拥有并初始化历史，或彻底跳过首次无效历史读取，避免 NaN 即使乘 0 仍污染输出。

共享扩展的 `Shaders/Compile.cs` 和 `.g.cs` 仅作实现参考，不在本任务中重生成或修改。局部 shader 使用实验已有的 ZenithCompiler 编译路径；不能仅因 C# 编译成功宣称后端验证完成。若问题无法在实验侧解决，记录具体依赖、复现和受影响功能，不能自行扩大到共享框架修改。

## 9. 设置、UI 和资源变更

已经落地的调用链：

```csharp
// Renderer 持有公开字段，RenderSettings 为普通结构体。
public RenderSettings Settings;

// App 初始化 renderer 时设置初值。
renderer = new()
{
    Settings = new()
    {
        RenderScale = 1.0f,
        UpscalingMode = UpscalingMode.None,
        TimeOfDay = 12.0f
    }
};

// 控件仍在 App 的原有回调中，直接编辑结构体字段。
ImGuiHelper.Settings(() =>
{
    ImGui.SliderFloat("Render scale", ref renderer.Settings.RenderScale,
        0.5f, 1.0f, "%.2fx", ImGuiSliderFlags.AlwaysClamp);
});

// 保持原有 Render 回调中的调用和位置。
renderer.Update(camera);
renderer.Render(commandBuffer);
```

当前 `RenderSettings` 包含 RenderScale、UpscalingMode、TimeOfDay 三个公开字段，不使用 record、init、属性包装或不可变快照传递。App 负责初值 scale=1、mode=None、time=12，并在现有控件中限制 renderScale=0.5..1、timeOfDay=6..18。App 不再保存重复的散落字段，控件直接读写 `renderer.Settings`，不要复制结构体到局部变量后忘记回写。

当前完成的是 **App → Renderer 公开设置字段**，并没有 GPU 常量上传、内部尺寸调整、太阳计算或 SGSR Dispatch。后续阶段必须由 Renderer 从 Settings 读取并应用到下表的实际消费者。保持原有 Color 绑定；先补齐 Renderer 内的资源实现，不能靠修改调用方的空值行为或一张占位颜色图宣称完工。

扩展设置时按实际消费需要增加普通字段；确实需要分组时可增加 `SkySettings`、`ShadowSettings`、`PostProcessSettings` 等普通子结构体，仍由 App 控制。不要引入不可变子记录、泛型参数注册表或 ImGui 对象进入 renderer。Renderer 如需比较上帧值，可在内部保存值副本并逐字段比较，不改变公开可写字段的接口。

| 设置 | 建议默认/范围 | 消费者与生效方式 |
| --- | --- | --- |
| RenderScale | 1 / 0.5..1 | 改变 R；在帧边界重建 R 资源、upscaler 并 Reset；不重建 D 的 Color |
| UpscalingMode | 当前 None，最终 High 为 Temporal | 切换第 6 节路径；销毁旧 upscaler/history，更新 jitter 策略 |
| TimeOfDay | 当前初值 12；GoldenHour 预设 16 / 6..18 | 更新太阳方向/色彩/光强、阴影、天空；标记 IBL/probes dirty |
| SunAzimuth | 25° 起步 / -180..180° | 太阳方向、阴影和环境；保存构图调好的实际值 |
| SunIntensity | 6 起步 / 0..20 相对单位 | 太阳 HDR 辐射常量；触发 probe 更新 |
| SkyIntensity / IndirectIntensity | 1 / 0..3 | 对应天空/间接项；明确是否影响捕获及历史 |
| CloudCoverage | 0.25 / 0..1 | EvaluateSky；触发环境更新 |
| ShadowResolution / Softness | High=2048 / 1024、2048、4096 | 分辨率只重建 shadow 资源；柔度只更新 shader 常量 |
| AoRadius / AoStrength | 0.75 m、1 / 0.05..3 m、0..2 | AO 常量；0 强度使用 neutral AO |
| FogDensity / FogHeightFalloff | 0.01、0.2 / 0..0.1、0..2（米尺度） | 雾和透明雾常量；不是全屏透明灰色叠层 |
| ExposureEv / BloomStrength | 0、0.04 / -5..5、0..0.2 | 后期常量；无需重建场景资源 |
| DebugView | Final | 渲染对应真实中间结果并保持可读；切换时 Reset |

这些光强、雾和 AO 数字是以米尺度为前提的调画起点，不是物理测量值；最终在 Sponza 上验收后保存。新增控件在 App 的原有 Settings(Action) 回调中按 Rendering、Lighting & Sky、Shadows、Ambient & Fog、Post Processing、Debug 分组，不修改 ImGuiHelper 的窗口布局/折叠行为。该回调中可显示 R/D 实际尺寸、当前 AA 模式、环境更新状态与 GPU 耗时；参数不可用时禁用并解释缺少哪一个 Pass。

renderer 比较新旧字段后分别处理常量变化、尺寸变化、环境失效、历史失效。禁止为每次 slider 编辑重新加载模型、重编译 shader 或重建全部纹理。后续如增加 Reset/预设，统一在 App 中写入 renderer.Settings，不直接操作 Pass，不在 helper/renderer 内隐藏另一套默认值。

保留现有相机和输入 Handler。若验证时发现 UI 捕获与相机输入冲突，记录复现，不将设置重构扩大成输入框架修改。

## 10. 实施里程碑

每阶段完成实现后执行构建、与本阶段相关的小场景/窗口验证，并记录结果再继续。只为关键数值合同和已发现的回归编写有意义的测试，不为每个 getter 或私有实现机械铺设测试工程。临时调试几何可保留为诊断模式，不能替代最终资产。

### M0：设置链路与可运行基础

- [x] RenderSettings 使用普通结构体，Renderer 持有公开 Settings 字段，由 App 初始化和直接编辑。
- [x] 保持 ImGuiHelper、Color 直接绑定及 App 原有 Update/Render/Resize 流程。
- [ ] 在 Renderer 内实现 Color 创建、清屏/输出、内部尺寸资源、异常和 Dispose。
- [ ] 在 App 原有设置回调中显示实际设置/尺寸；Renderer 仅在实际渲染时推进帧状态。
- [ ] 完成 Renderer 的资源初始化后可见原有 UI；编辑控件有效，缩放/最小化不崩溃。此阶段不要求场景效果。

### M1：资产和材质基线

- [ ] 完成第 4 节的资产取得、来源记录和 glTF 导入；替换模型目录占位文件。
- [ ] 生成 vertex/index/material buffer，批量上传；按用途加载贴图/mip/default textures。
- [ ] 实现 DepthPrepass 和基础 Forward，输出 BaseColor/Normal/Roughness/Metallic/UV/Depth 调试视图。
- [ ] 以真实 Sponza 验证节点实例、比例、法线、MASK、双面材质，设置默认相机。
- [ ] 验收：材质没有错位、纯黑缺图、镜像法线、植被矩形、全场景塑料高光；缺失路径错误可诊断。

### M2：直接光、PBR 和基础后期

- [ ] 完成 GGX PBR、材质常量和 HDR；先用小型球阵列校验，再切回 Sponza。
- [ ] TimeOfDay/SunAzimuth/Intensity 更新真实 GPU 常量；实现基础 PCF 阴影。
- [ ] 完成手动曝光、tone mapping 和唯一 sRGB 编码；None 路径的 FXAA/双线性输出先跑通。
- [ ] 验收：改时间能移动太阳阴影，改粗糙度能改变高光宽度，过曝高光有滚降，UI 颜色正常。

### M3：程序化天空和 IBL

- [ ] 实现共享天空函数、太阳盘、薄云与天空输出，不加载 HDRI 天空。
- [ ] 完成 sky cube、diffuse convolution、GGX prefilter、BRDF LUT，按 dirty 更新。
- [ ] 接入天空参数、环境状态显示和完整资源切换；时间变化后材质环境光随之更新。
- [ ] 验收：六面无接缝，金属球能反射天空，粗糙材质反射更模糊；6/12/16/18 点无 NaN、闪黑或突变。

### M4：高级阴影、AO 和局部间接光

- [ ] 将阴影升级为稳定四级 CSM + PCSS，并提供级联、bias、softness 调试。
- [ ] 完成 GTAO/降噪/上采样；静止和移动时检查细柱、叶片、拱顶轮廓。
- [ ] 完成至少三个局部 probe 的光栅捕获、卷积、box projection、范围混合与时间失效。
- [ ] 完成受控补光；关闭补光仍能辨识材质，不以过量 ambient 掩盖缺失间接光。
- [ ] 验收：阴影不游泳/脱离地面，拱廊无明显天空漏光，探针交界不硬切，间接光具有冷暖和空间层次。

### M5：空气感和完整合成

- [ ] 完成体积雾与 CSM 阴影采样、透明材质及透明雾；保存正确的 OpaqueInput。
- [ ] 实现 HDR Bloom，完善曝光/tone mapping；各效果都能独立开关比较。
- [ ] 校准 GoldenHour/Noon/Overcast 外观预设；Overcast 可降低太阳、增加薄云和柔和 sky，不要求天气模拟。
- [ ] 验收：逆光有空气感，顺光不灰；透明物体边缘无黑边，亮部不过度泛白，石材细节保持清楚。

### M6：空间/时域超分和稳定性

- [ ] 完成 Spatial LDR 路径与 Temporal HDR 路径；只读审计第 8 节问题，必要时在 Sponza 中适配边界与首帧历史。
- [ ] 校验 motion 编码、jitter、深度、透明反应性、SameCamera/Reset，完成相机 cut 和尺寸切换处理。
- [ ] 将最终 High 默认改为 Temporal/scale=1，保留 None 作为 FXAA 对比路径。
- [ ] 验收：三模式与全部 scale 均真实生效；细柱/高光稳定，快速转头/新显露区域无持续重影，天空不随平移产生视差。

### M7：性能、后端与最终画面

- [ ] 完成第 11 节的对比截图、帧时间记录、稳定性和后端验证；解决验证层错误。
- [ ] 完成 High/Medium/Ultra 预设；根据实际 GPU 成本调节采样数，不自动改用户选定的模式。
- [ ] 删除被实现替代的 Pass/Shader 占位文件、无用路径和假开关；不删除尚无替代内容的资产。
- [ ] 编写最终 `README.md`：运行命令、模型来源、操作、渲染架构简述、截图、硬件与未验证项。
- [ ] 按构图和材质细节做最后调画；只有本节和必需验收通过，才报告整个实验完成。

M0 中已勾选项仅表示本次设置重构，不表示 M0 整体或完整渲染器已完成。可选 SSR/完整大气 LUT/自动曝光等在 M7 之后处理；不能扩大可选范围导致必需项长期未交付。

## 11. 质量预设和验收

画质默认 High；分辨率/模式仍允许手动设置，并显示修改后的状态。预设从同一份设置模型生成，不散落在 UI 和 shader 中。

| 项目 | Medium | High（最终默认） | Ultra（截图/余量充足时） |
| --- | --- | --- | --- |
| RenderScale / AA | 0.75 / Temporal Quality | 1 / Temporal Quality | 1 / Temporal Quality |
| CSM | 4×1024，PCF | 4×2048，PCSS 16+32 | 4×4096，PCSS 32+64 |
| AO | 半分辨率，减少样本 | 半分辨率，3×每侧3 | 全分辨率，增加样本 |
| Probe face / Sky face | 128 / 256 | 256 / 512 | 512 / 1024 |
| 体积雾 | 半分辨率，16 steps | 半分辨率，32 steps | 半分辨率，64 steps |
| Bloom | 4 层 | 5 层 | 5 层 |

目标是桌面 GPU 在 1920×1080 High 下争取 GPU frame ≤16.7 ms；这是设计目标，不是未经测量的性能承诺。至少记录设备名、OS/API、输出/内部像素、预设、资产版本、build configuration、validation 状态及是否受 VSync 限制。Warm-up 至少 120 帧后采集至少 300 帧的 GPU 中位数/P95；全帧时间和各 Pass 使用同一队列的 timestamp，借助 `CommandQueue.GetElapsedNanoseconds` 换算，不能把 ticks 当纳秒或把 ImGui FPS 当 GPU 时间。

性能目标未达时按 GPU 数据定位，先调雾/AO/阴影过滤与环境更新预算，再考虑 renderScale；保留正确材质、色彩和时域合同。Report 明确实际结果和妥协，不能只给理论采样数。不把离屏 probe 的完整重建成本隐藏在计时范围外；分别记录稳态和环境刷新峰值。

固定三个相机位置：A 中庭全景、B 拱廊近景、C 逆光向上看天空；在模型实际加载后保存精确位置/朝向/FOV。以 12:00、16:00 各生成三张最终输出 PNG，共六张；固定参数/噪声种子，等待至少 32 个稳定帧并等环境更新完成。另保存 AO、shadow visibility、normal/roughness、motion 调试截图及关键效果开关对比。截图回读只在按需时执行，保存到默认 git 忽略的本地输出目录，README 记录路径；需要提交展示图时只选少量压缩结果。

| 验收主题 | 必须执行的操作 | 通过标准 |
| --- | --- | --- |
| 资产 | 默认路径启动；缺贴图/错误路径；修正后重启 | 正确场景/明确路径错误，无静默替换 |
| 参数 | 每个 slider、mode、预设、Reset | renderer 实际常量/资源/画面变化；无假控件 |
| 光影 | 6→12→16→18 点，缓慢走动，靠近柱脚 | 有限数值、稳定级联、接触阴影和合理过渡 |
| 材质 | 石材、旗帜、金属近看/远看，调试通道 | 通道、alpha、法线方向和 mip 正确，无闪点 |
| AA | 每个 mode × scale 0.5/0.75/1 | 输出铺满窗口，UI 清晰，分支颜色一致 |
| 时域 | 静止、慢移、快速转头、camera cut、切 FOV | 细节稳定，无持续拖影；cut/尺寸变化正确 Reset |
| 环境 | 改时间/薄云，跨 probe 边界 | 更新有进度，完整资源切换，无接缝和明显穿墙光 |
| 生命周期 | 连续至少 20 次 Resize/模式切换，最小化恢复 | 无 use-after-free、无描述符失效，无持续内存增长 |
| 边界尺寸 | 1279×719、853×479、极小有效窗口及 HiDPI | 无越界线程/采样、比例失真、黑边或旧历史 |
| 色彩 | 同一相机三模式/曝光阶梯，检查 UI | 无双重 gamma；天空/材质曝光一致 |
| 平台 | Windows D3D12、macOS Metal、Linux Vulkan | 各目标分别运行验证；没有硬件的明确标为未验证 |

最少加入或执行针对实际算法的数值检查：矩阵投影/深度重建往返、motion 编码解码与已知平移重投影、非均匀/负缩放法线、尺寸计算和 history reset 状态机。每次新增这类逻辑时验证一次，修复失败后重跑相关项；不以大量重复构建代替画面验收。

从仓库根目录执行：

```sh
dotnet build sources/Experiments/Sponza/Sponza.csproj
dotnet build sources/Experiments/Sponza/Sponza.csproj -c Release
dotnet run --project sources/Experiments/Sponza/Sponza.csproj -c Release --no-build
```

已有还原结果时可加 `--no-restore`；若本机 MSBuild 并行节点挂起，使用 `-m:1 -nr:false -p:UseSharedCompilation=false` 验证，而不是更改项目依赖。本任务保持其他项目和共享扩展不变，不要求整套包含移动/UI workload 的 solution 构建。Slang 是运行时编译，C# build 成功不代表 shader 或 GPU 功能验证成功。

交付时提供实际截图和验证记录，列清未验证后端、仍存在的视觉缺陷和可选增强状态。没有可用 GPU 时可以继续导入/数值/构建工作，但 GPU 与视觉验收必须保留未完成；不能用“代码看起来正确”勾掉这些项。
