# Sponza 实时 PBR 渲染设计

## 1. 目标与边界

通过光栅化呈现材质清晰、明暗有层次、阴影稳定的 Sponza 场景。采用金属度/粗糙度 PBR、太阳阴影、天空/地面双色环境光、预过滤 IBL 和 GTAO，提供抗锯齿与超分输出。间接光采用全场统一的环境近似，不计算物体之间的光照反弹。

| 范围 | 约定 |
| --- | --- |
| 实现 | 完成 Renderer.cs，在现有 Passes、Models、Helpers、Assets/Shaders 中添加必要文件 |
| 保持不变 | App、Program、Handlers、CocoaHelper、ImGuiHelper、RenderSettings、UpscalingMode、模型与字体资产；保留相机、输入、窗口和 UI 操作 |
| 项目边界 | 不修改 Sponza 外代码、RHI、后端、共享扩展、工程、依赖或目录结构；范围外阻挡须提供证据并告知用户 |
| 接口 | 只用公共 RHI 与现有扩展，不通过原生句柄、后端强转、反射或原生调用补充能力 |
| 验证文件 | 验证程序、日志、截图放仓库外，不加入项目或解决方案 |

保留 Renderer 的无参构造、外部只读 Color、`public RenderSettings Settings;`、`Update(CameraHandler camera)`、`Render(CommandBuffer commandBuffer)`、`Resize(uint width, uint height)` 和 `Dispose()`。App 构造后设置 `None / RenderScale=1 / TimeOfDay=12`。

遵循 [Zenith.NET 代码风格与规范](<../../../Zenith.NET 代码风格与规范.md>)。实现前核对核心 RHI、三个后端、相关扩展、Sponza 宿主和 CornellBox 光栅化用例的接口、同步与生命周期。

## 2. 四个 Pass

```text
ShadowPass → ScenePass → AmbientOcclusionPass → OutputPass → Color
```

| Pass | 输入 → 输出 | 职责 |
| --- | --- | --- |
| ShadowPass | 静态场景、太阳方向 → 阴影纹理与光源矩阵 | 生成固定场景范围的太阳阴影，光照未变化时复用 |
| ScenePass | 场景、相机、阴影、IBL → 光照分量、深度、法线、motion | 前向 PBR 着色，随后绘制天空盒背景 |
| AmbientOcclusionPass | 场景深度、法线、光照分量 → 合成 HDR | GTAO、保边滤波及环境漫反射合成 |
| OutputPass | 合成 HDR、深度、motion、输出设置 → Color | FXAA、所选 SGSR、固定曝光和显示转换 |

Renderer 管理设置、尺寸、帧状态、调用顺序和 Color。SceneResources 位于 Helpers，加载场景并初始化 IBL。

Pass 放 Passes，着色器放 Assets/Shaders；内部可包含多次绘制或计算。context 与输入显式传入，Pass 不读取 App、Settings 或 Camera，也不相互调用。Models 只存放跨模块数据。加入实际文件时删除对应目录的占位文本。

## 3. 场景与材质

通过 `AppContext.BaseDirectory` 定位 Assets，使用现有 SharpGLTF.Core 加载 Sponza.gltf。按 accessor、primitive、材质与节点变换读取数据；保留物体空间位置、法线、切线、UV 和局部索引，绘制时应用节点变换与顶点基址。世界法线使用逆转置，切线处理手性与镜像变换；缺失或退化切线由几何和 UV 构造。场景包围盒由变换后的几何计算。

纹理使用现有 ImageSharp 扩展加载并生成 mip。基础色使用 `compand=true`，法线和金属粗糙度使用 `compand=false`，缓存键包含图像引用与颜色语义。基础色只做一次 sRGB 解码，alpha 保持线性；粗糙度取 G、金属度取 B，并应用材质因子。材质采样遵循 glTF 的寻址方式，颜色贴图使用三线性及适当的各向异性过滤。

材质依据 [glTF 金属度/粗糙度模型](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html#appendix-b-brdf-implementation)，采用 Cook–Torrance GGX、Smith 高度相关可见性、Schlick Fresnel 和 Lambert 漫反射。N、视线及光照方向均为世界空间单位向量；NoV、NoL、VoH 分别为法线与视线、法线与光照、视线与半程向量的夹角余弦。

线性基础色 C、金属度 m 对应 `F0=lerp(0.04,C,m)`。为控制尖锐高光，着色粗糙度取 `r=clamp(roughness,0.045,1)`，`alpha=r²`。直接光使用 `F=F0+(1-F0)*(1-VoH)^5`、`fd=(1-F)*(1-m)*C/π`、`fs=D_GGX*V_Smith*F`；V_Smith 已包含 `1/(4*NoL*NoV)`。NoL 或 NoV 非正时直接光贡献为零；BRDF 不裁到 1，纯金属没有漫反射。

ScenePass 将颜色附件清零、深度清为 1，使用深度读写和 Opaque 混合。片元中应用 NormalScale、正交化 TBN 并归一化法线。单面材质背面剔除，双面材质按朝向翻转着色基底；镜像节点同步调整绕序。OPAQUE 忽略 alpha，MASK 在场景和阴影绘制中使用相同基础色 alpha、材质因子与 cutoff，各自按纹理足迹选择 mip。

## 4. 光照、环境反射与天空盒

世界单位为米、Y 向上。太阳是唯一的直接光源，`θ=π(TimeOfDay-6)/12`，指向太阳的方向为 `L=(0,sinθ,-cosθ)`；6/12/18 时对应 −Z/+Y/+Z。直接光为 `Ldirect=(fd+fs)·Esun·max(dot(N,L),0)·shadowVisibility`。灯光强度使用统一的相对线性尺度。

环境由天空色 S 和地面色 G 定义：`Lenv(w)=lerp(G,S,(w.y+1)/2)`。背景和镜面 IBL 共用此环境。漫反射使用其余弦卷积的解析值 `H(N)=(S+G)/2+(S-G)·N.y/3`，H 已包含除以 π 的因子；环境漫反射为 `Lambient=C·(1-m)·(1-Fview)·H(N)`，Fview 为按 NoV 求值的 Schlick Fresnel，不重复除以 π。

默认线性参数为 `Esun=(3.0,2.9,2.7)`、`S=(0.12,0.16,0.22)`、`G=(0.035,0.025,0.02)`、曝光 1，集中在 Renderer 内。TimeOfDay 控制太阳方向，环境颜色固定。

镜面 IBL 采用 [split-sum 的单次散射形式](https://google.github.io/filament/main/filament.html#lighting/imagebasedlights)。加载时生成 128×128×6 环境立方体的 8 个 mip：第 i 层对应感知粗糙度 i/7，首层直接写 Lenv，其余层按 GGX 重要性采样并以 NoL 归一化加权。另生成 128×128 RG16Float BRDF LUT，坐标为 `(NoV,r)`；预过滤和 LUT 各使用 256 个固定 Hammersley 样本，均以 alpha=r² 计算。

LUT 两通道存储 Schlick 的系数 A、B，沿用与直接光一致的 Smith 可见性；`Lspecular=PrefilteredEnv(reflect(-view,N),lod=r*7)*(F0*A+B)`。生成和查表必须使用同一通道定义，不混用多次散射 LUT。环境不含太阳圆盘及局部场景几何，只在加载时生成；背景与材质使用一致的立方体方向约定。

### 天空盒绘制

ScenePass 在几何之后用全屏三角形绘制天空，复用附件；使用 CullNone、Opaque 和 DepthStencilState.DepthRead()（LessEqual、深度只读），clip z=w、depth=1，仅填充背景。

由实际投影（含 Temporal jitter）和视图逆旋转重建世界方向 w，求值 Lenv(w)，排除相机平移。背景写入 BaseHdr，AmbientHdr 与法线写零；motion 写入第 6 节定义的旋转位移，与场景共用输出流程。

## 5. 阴影与环境遮蔽

### 5.1 太阳阴影

采用覆盖完整静态场景的 4096×4096 D32Float 正交阴影图。光源视图朝向场景中心，up 取 +X；按变换后的场景包围盒确定 XY 范围及完整投影深度范围，并为过滤保留边界。投影不随主相机变化，首帧或太阳方向变化时更新。

深度清为 1，使用普通 0～1 深度。光源 NDC 转 UV 为 `(0.5*x+0.5,0.5-0.5*y)`，比较深度直接使用 z；以 `z-bias <= storedDepth` 得到可见性，再做固定 3×3 PCF。正偏移考虑斜率、阴影 texel 世界尺寸及光源深度跨度，校准时同时检查自阴影和接触脱离。

阴影绘制使用 CullNone，MASK 执行 alpha-test；所有遮挡物参与，不能用主相机可见物列表裁掉投影来源。阴影投影外视为无遮挡，投影内的过滤核必须落在预留边界中。

### 5.2 GTAO 与合成

以 [XeGTAO 固定提交 a5b1686c7ea37788eeb3576b5be47f7c03db532c](https://github.com/GameTechDev/XeGTAO/tree/a5b1686c7ea37788eeb3576b5be47f7c03db532c/Source/Rendering/Shaders) 的 XeGTAO.h、XeGTAO.hlsli 和 vaGTAO.hlsl 为参考，在公共 Compute RHI 内移植，保留来源与许可。内部完成深度预过滤、GTAO 求值、一次空间保边滤波和光照合成。

AO 在内部渲染分辨率运行，使用 High 预设、Radius=0.5 米、DenoisePasses=1，其余核与启发式沿用固定参考。深度链最多 5 个有效 mip，处理小尺寸与非整除调度边界。None/Spatial 的 NoiseIndex 固定为 0；Temporal 使用 frameIndex % 64，由 SGSR2 处理最终图像的时间重建，不另设 AO 历史。

ScenePass 的法线附件存储右手视空间单位法线。XeGTAO 使用向前为正 Z 的视空间，接入时将重建位置和法线同时转换为 `(x,y,-z)`；深度解包与 NDCToView 常量对应当前实际投影，包含 Temporal jitter。背景 depth=1，AO 可见度为 1，邻域不能将背景当作遮挡。

沿用参考的 AO 编码、滤波和最终解码，获得 0～1 的可见度 A。ScenePass 将直接光、镜面 IBL、背景写入 BaseHdr，将环境漫反射单独写入 AmbientHdr；合成为 `SceneHdr=BaseHdr+A·AmbientHdr`。AO 只调制环境漫反射，不能将整幅图像统一压暗。

## 6. 抗锯齿、超分与显示

D 为 framebuffer 像素尺寸，`R=max(1,floor(D*RenderScale))`，逐维计算。场景和 AO 使用 R，Color 使用 D；相机投影沿用宿主矩阵，不按 R 重建。

光照在线性 HDR 中计算。显示转换统一为：`x=max(Exposure*HDR,0)`，应用 [Narkowicz 的 ACES 风格拟合曲线](https://knarkowicz.wordpress.com/2016/01/06/aces-filmic-tone-mapping-curve/) `T(x)=saturate(x*(2.51*x+0.03)/(x*(2.43*x+0.59)+0.14))`，再做一次标准分段 sRGB 编码。LDR 使用 UNorm 纹理保存已编码值，避免采样或写入时再次转换。

| 模式 | OutputPass 内部流程 |
| --- | --- |
| None | SceneHdr(R) → 双线性采样到 D 并做显示转换 → FXAA(D) → Color |
| Spatial | SceneHdr(R) → 显示转换到 LDR(R) → FXAA(R) → SGSR1 → Color(D) |
| Temporal | SceneHdr(R)＋深度/motion/jitter → SGSR2 Quality → HDR(D) → 显示转换 → Color |

None/Spatial 使用 [FXAA 3.11](https://docs.nvidia.com/gameworks/content/gameworkslibrary/graphicssamples/d3d_samples/fxaa311sample.htm) 的 Quality 路径，不施加投影 jitter。输入采用 RGBL：RGB 为显示颜色，alpha 为 `dot(RGB,(0.299,0.587,0.114))`，配置 FXAA 读取 alpha 亮度；输入与输出分离，边界使用 Clamp，最终 Color alpha=1。FXAA 平滑单帧轮廓；纹理与高光还依赖 mip、过滤及粗糙度处理。

Temporal 由 SGSR2 同时完成时域抗锯齿和超分。在内部投影副本上施加 Halton(2,3) 八相减 0.5 的 jitter，单位为 R 像素，NDC 偏移 `(2*jx/Rw,-2*jy/Rh)`。颜色、深度、AO 和 motion 的像素位置对应同一投影；motion 的位移值仍排除 jitter。阴影图不抖动，输出不叠加 FXAA 或独立 TAA。

使用现有 CreateSpatialUpscaler、CreateTemporalUpscaler，保持共享扩展算法不变。SGSR1 接收 [0,1] 显示颜色；SGSR2 接收非负线性 HDR。UI 由 App 在最终 Color 上以原生分辨率绘制。

| SGSR2 输入 | 契约 |
| --- | --- |
| Input / OpaqueInput | 同一 SceneHdr，场景只有 OPAQUE/MASK |
| Depth | ScenePass 的普通 device Z，near=0、far=1，背景为 1 |
| MotionVectors | 当前减上一帧的未 jitter NDC 位移 m，编码 `m*0.2495+32767/65535` 到 RG16UNorm；超出范围时写零编码供矩阵回退，静止为编码中值 |
| JitterOffsetX/Y | 当前 R 像素单位 jitter |
| ClipToPrevClip | `inverse(currentJitteredVP)*(previousUnjitteredVP*currentJitterMatrix)`，row-major、行向量 |
| PreExposure / CameraFovAngleHor | 1 / `1/unjitteredProjection.M11`，后者为水平半视角正切 |
| SameCamera / MinLerpContribution / Reset | 未 jitter 相机是否相同 / 0 / 相关历史是否失效 |

Motion 在片元中由同一表面位置的当前/前帧 clip 坐标透视除法后计算，不能在线性插值前逐顶点完成除法。背景使用方向的旋转 motion，不引入相机平移。首帧使用零位移并重置历史。

## 7. 资源与生命周期

| 所有者 | 资源与语义 |
| --- | --- |
| SceneResources | 几何、材质、纹理、场景边界、环境立方体与 BRDF LUT |
| ShadowPass | 固定尺寸深度纹理、光源矩阵及阴影绘制状态 |
| ScenePass | R 尺寸的 BaseHdr、AmbientHdr、视空间法线、device Z 和 RG16UNorm motion；深度同时可采样，motion 供 Temporal 使用 |
| AmbientOcclusionPass | 预过滤深度 mip、AO/边缘工作纹理、合成 SceneHdr；不持有跨帧颜色历史 |
| OutputPass | 所选流程必需的 LDR/HDR 中间图、FXAA 与共享超分实例；超分历史归扩展所有 |
| Renderer | D / RGBA8UNorm Color，存 sRGB 编码值；仅 D 改变时替换 |

场景与输出中间 HDR 使用 RGBA32Float，法线使用 RGBA16Float，着色运算使用 float；主场景深度为 D32Float，GTAO 的正线性深度工作图为 R32Float。AO 工作图遵循参考编码，可用 R32UInt 承载整数 AO、R8UNorm 承载边缘信息。所有模式的场景附件均为 SampleCount.Count1，ScenePass 使用同一组 MRT 输出；背景 AmbientHdr=0。

- 无参构造返回前创建有效 Color。Settings 在构造后赋值，设置相关内部资源在首次 Update 建立。App 在 Renderer.Update 前绑定 Color，旧 Color 保留至使用它的提交完成。
- 模式与 RenderScale 改变复用 Color，只重建所需内部资源。Resize、模式、比例、光照、相机或投影不连续使 SGSR2 历史失效；普通移动保留历史。最小化不推进帧号，恢复时处理失效。
- 加载期完成上传、IBL 初始化及必要同步。逐帧由 Pass 录制命令，App 单次 Submit/Wait；借用 CommandBuffer 不保留、不释放。固定阴影与 IBL 不随输出尺寸重建。
- 资源以具名引用借用，所有权不转移。采样输入与写目标分离，按真实 layout 记录绘制、计算及采样依赖，包括 GTAO 的 mip 和工作图。
- GPU 常量采用与 Slang 对齐的显式布局 file struct，布尔使用 uint。SetConstantBuffer 的第二参数为字节偏移，同帧不同绘制或调度使用独立常量区域。
- 稳态不创建管线、纹理或场景数组；按实际 Texture.Desc 管理尺寸，记录峰值内存。最后一次使用完成后释放所有自有资源，视图先于纹理；不释放借用对象。

## 8. 验收

固定光照和机位，对照直接光、环境光、阴影、AO 及最终输出。亮度、偏移与 AO 参数以同一组图像校准，验收依据运行结果。

- glTF 变换、颜色空间、金属度、粗糙度、法线、MASK 和双面材质正确。对照标准材质球验证金属/非金属、粗糙度变化与掠射高光，纯金属不出现漫反射。
- 常量环境下，环境卷积和各层预过滤保持常量，镜面 LUT 与相同 BRDF 的数值积分在采样误差内一致；检查 LUT 通道、粗糙度 mip、立方体方向和色彩编码，避免重复 Fresnel、重复除以 π 或额外曝光。
- 主光方向清晰，侧廊和拱顶有层次；无大面积过曝、塑料感高光或过亮环境反射。接受环境光近似，不以增加补光点或加重 AO 掩盖材质与阴影错误。
- 天空覆盖背景并被建筑、叶片和链条正确遮挡；平移相机时无视差，转动及 Temporal 抖动时与场景坐标一致，无背景黑边或遮蔽污染。
- 阴影完整覆盖场景，移动相机时稳定；无明显漏光、自阴影条纹、接触脱离，叶片与链条的阴影轮廓合理。
- AO 加强墙角、柱脚和接触处，不形成大范围黑边、光晕或脏斑。其屏幕空间边界及显露区域表现稳定。
- None/Spatial 平滑轮廓，Temporal 重点验收细线、纹理、显露区域和运动高光；无明显重影、过度模糊或重复锐化，三种模式的整体亮度与颜色一致，UI 清晰。
- 覆盖 None/Spatial/Temporal、1/.75/.5 比例、6/9/12/18 时刻、奇数尺寸、连续 Resize、最小化恢复、模式切换和退出。
- Apple M4、D=2560×1440、Temporal/.5 持续移动时目标至少 30 FPS。记录整帧及模块耗时、内存和启动开销，区分 CPU/提交等待与 GPU 计时。
- 构建与着色器编译无警告、错误；无 NaN/Inf、越界、失效句柄或持续资源增长。记录已验证后端，遇到接口或性能阻挡时报告证据与影响。

## 9. 参考

- [阴影深度图、投影范围与偏移](https://learn.microsoft.com/en-us/windows/win32/dxtecharts/common-techniques-to-improve-shadow-depth-maps)
- [SGSR1 输入与接入](https://github.com/SnapdragonGameStudios/snapdragon-gsr/tree/main/sgsr/v1)
- [SGSR2 时域重建](https://github.com/SnapdragonGameStudios/snapdragon-gsr/tree/main/sgsr/v2)
- 仓库用例：Experiments/CornellBox/Renderers/RasterizationRenderer.cs、Zenith.NET/CommandBuffer.cs、Zenith.NET/Structs、Extensions/Zenith.NET.Extensions.Upscaling。
