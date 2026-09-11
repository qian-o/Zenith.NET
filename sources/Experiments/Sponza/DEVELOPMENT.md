# Sponza 光线追踪渲染设计

## 1. 目标与边界

实现可自由移动、实时降噪的 Sponza 路径追踪场景，并提供原生、空间超分和时域超分输出。主可见性、直接光、间接反弹、反射和阴影统一通过硬件光追求解。接触遮蔽包含在光传输结果中，不额外乘 AO。渲染范围为静态 PBR 表面与动态日光天空，不引入局部参与介质。

采用公共 BLAS/TLAS、Compute RayQuery、材质与光源重要性采样、SVGF 实时降噪、自动曝光、Bloom、色调映射及共享 SGSR。每帧产生新路径样本，移动和静止使用同一实时渲染管线。

光传输以 PBRT 的渲染方程与蒙特卡洛估计为依据，材质采用明确的线性 RGB 反射模型。物理正确性针对所定义的材质和光源验证；RGB、法线贴图、天空 LUT 与纹理足迹均为建模近似，不等同于光谱测量或 PBRT 的全部功能。

| 范围 | 约定 |
| --- | --- |
| 实现 | 完成 Renderer.cs，并在现有 Passes、Models、Helpers、Assets/Shaders 目录内添加所需类型和着色器 |
| 保持不变 | App、Program、Handlers、CocoaHelper、ImGuiHelper、RenderSettings、UpscalingMode、模型及字体资产 |
| 项目边界 | 不修改实验外代码、RHI、后端、共享扩展、工程、依赖或现有目录结构；需要扩展这些范围时告知用户 |
| 接口 | 只使用现有公共 RHI 与扩展，不访问原生图形句柄、不强转后端、不使用反射或原生调用补充渲染能力 |
| 文档与验证 | 设计维护于本文；验证程序、日志和截图放仓库外，不加入项目或解决方案 |

保留 Renderer 的无参构造、外部只读 `Color`、`public RenderSettings Settings;`、`Update(CameraHandler camera)`、`Render(CommandBuffer commandBuffer)`、`Resize(uint width, uint height)` 和 `Dispose()`。App 在构造 Renderer 后通过对象初始化器设置 `None / RenderScale=1 / TimeOfDay=12`，并保持现有相机、WASDQE、鼠标、窗口及 UI 操作。

代码遵循 [Zenith.NET 代码风格与规范](<../../../Zenith.NET 代码风格与规范.md>)。实现前阅读核心 RHI、DirectX12/Metal/Vulkan、相关扩展及 CornellBox 光追调用，核对实际接口、同步和生命周期。普通数据采用公开字段的 `struct`；资源所有者采用 `IDisposable`；C# 不用 `var`、`record` 或 `in/scoped` 参数，不引入通用渲染框架。代码单行优先，过长续行对齐。

## 2. 管线与职责

```text
场景资源（几何、材质、BLAS/TLAS） ─┐
                                 ├─ 路径追踪 ── SVGF 降噪 ── 输出分支 ── Color / UI
太阳与天空 ──────────────────────┘                   │
                                                    └─ 曝光测光
```

Renderer 统一组装输入并按依赖录制命令。Pass 不调用或持有其他 Pass，不读取 App、Settings 或相机，不保存、提交、等待或释放借用的 CommandBuffer。

SceneResources 与重复的资源创建辅助放 Helpers；各 Pass 放 Passes；跨文件的数据、Args 和 Output 放 Models；Slang 放 Assets/Shaders。RenderSettings、UpscalingMode 和宿主辅助类型保持不变。Passes 与 Assets/Shaders 的占位文本分别在加入实际类和着色器时删除，不作为交付内容。

| 模块 | 输入与职责 | 输出及所有权 |
| --- | --- | --- |
| SceneResources | 加载场景及材质，上传几何并构建 BLAS/TLAS | 拥有几何、材质、实例表、纹理、视图及加速结构；完成加载后借出 SceneData |
| EnvironmentPass | TimeOfDay、太阳与大气参数 | 拥有大气 LUT、天空环境图和重要性分布；借出同版本 EnvironmentData |
| PathTracingPass | FrameData、含 TLAS 的 SceneData、EnvironmentData、采样参数 | 拥有两路原始辐亮度、首次命中引导和镜面重投影引导；借出 PathTracingOutput |
| DenoisePass | 原始两路信号、当前/上一帧引导、FrameData | 拥有 SVGF 历史、亮度矩、方差、滤波中间图和 DenoisedHdr |
| ExposurePass | DenoisedHdr、深度、实际帧间隔 | 拥有 GPU 内 1×1 曝光状态 |
| BloomPass | 输出分支中的 HDR、曝光、显示尺寸 | 拥有辉光金字塔；输出未曝光 Bloom |
| ToneMappingPass | HDR、Bloom、曝光、借用的 LDR 目标 | 应用曝光、色调映射及 sRGB 编码，写满目标 |
| UpscalingPass | 具名颜色、深度、motion、jitter、尺寸及 Reset | 拥有共享超分实例和时域 HDR 输出；空间超分及双线性写借用 Color |

跨 Pass 使用具名 Args/Output 和借用的 Texture、Buffer、TextureView 引用，不用字符串资源表或靠数组下标区分职责。Renderer 拥有各 Pass、场景、最终 Color 及必要的 R 尺寸 LDR 目标；GPU 句柄只在常量布局和扩展参数中提取。

## 3. 场景、材质与加速结构

模型与着色器路径均基于 `AppContext.BaseDirectory`，由现有工程复制 Assets 到输出目录。通过 SharpGLTF.Core 加载 `Assets/Models/Sponza.gltf`，按 accessor、primitive、材质引用和节点变换读取，不按文件名推断数据。顶点保留物体空间位置、法线、切线及 UV；节点变换只在 TLAS 实例和对应着色变换中应用一次。法线使用逆转置，切线保留手性并处理镜像变换。

图像使用现有 ImageSharp 扩展。基础色传 `compand=true`，法线与金属粗糙度传 `compand=false`，均生成 mip；缓存键包含图像引用和颜色语义。基础色由 sRGB 采样解码一次，alpha 保持线性；粗糙度取 G、金属度取 B，材质因子遵循 glTF。法线贴图解码后应用 NormalScale、TBN 并归一化；缺失或退化切线由有效几何和 UV 构造，不修改资产。

每个共享 primitive 对应一个 BLAS，每个节点中的 primitive 对应一个 TLAS 实例，使用 `PreferFastTrace`，实例可见性掩码为 `0xFF`。实例 ID 索引实例表，表中保存材质、顶点/索引范围、世界变换与法线变换。命中通过实例 ID、primitive index 和重心坐标重建属性；局部 UInt16 索引与顶点基址分别处理，不能把 primitive index 当全场索引。

OPAQUE 几何标为 opaque，MASK 几何标为 non-opaque。主射线、反弹射线和阴影射线共用 alpha-test：在 RayQuery 候选命中时重建 UV，读取相同的 alpha 与 cutoff，通过后才确认命中。双面材质按实际入射侧构造着色基底；单面材质的背面命中吸收并终止路径，阴影射线仍视其为遮挡，不透过它查找后方亮面。实例绕序与 glTF 及镜像变换一致。

BLAS/TLAS 是 SceneResources 持有的静态资源，不参与逐帧 Pass 调度。几何上传完成后，加载代码借用公共队列的 CommandBuffer，在同一构建批次中依次建立 BLAS、TLAS，并完成一次 `Submit().Wait()` 后发布 SceneData。现有纹理和缓冲加载器的同步仅发生在加载期，借用的 CommandBuffer 不释放。

相机、太阳、窗口、RenderScale 及普通材质参数变化不重建加速结构。只有几何、索引、实例变换或 OPAQUE/MASK 分类改变时才重新建立场景资源。SceneData 借出 TLAS 对象引用，PathTracingPass 录制常量时读取 Handle；最后一次使用完成后由 SceneResources 按 TLAS、BLAS、几何缓冲的顺序释放。运行设备要求 `RayTracingSupported=true`；能力或正确调用存在阻挡时报告。

## 4. 光传输

### 4.1 表面、估计器与采样

所有方向采用从表面向外的约定，辐亮度保持非负线性 RGB，使用同一辐射尺度。积分目标为 `Lo=Le+∫ f(wo,wi) Li(wi) |n·wi| dω`；几何可见性包含在入射光中。BSDF 的求值、采样、PDF 与反射/delta 标志必须相互一致。

材质采用 [PBRT FresnelBlend](https://pbr-book.org/3ed-2018/Reflection_Models/Fresnel_Incidence_Effects) 的漫反射与镜面耦合，微表面分布使用 Trowbridge–Reitz（GGX）。令线性基础色为 C、金属度为 m，`Rd=(1-m)C`、`Rs=lerp(0.04,C,m)`；二者是 [0,1] 反射参数，BRDF 值本身不裁到 1。令 `ci=|Ns·wi|`、`co=|Ns·wo|`、`h=normalize(wi+wo)`，同侧非 delta 求值为：

```text
F(c) = Rs + (1-Rs)(1-c)^5
fd = 28/(23π) · Rd(1-Rs) · [1-(1-ci/2)^5] · [1-(1-co/2)^5]
fs = D_GGX(h) F(|wo·h|) / [4|wo·h| max(ci,co)]
f  = fd + fs
```

镜面按上述分母求值，GGX 可见法线采样中的 G1 仅用于采样 PDF；Fresnel 采用 Schlick 近似。glTF 感知粗糙度映射为 `alpha=roughness²`，与 PBRT 对照时直接匹配 alpha，不再施加第二次 roughness 映射。零粗糙度仅将镜面项变为权重 `F(co)` 的 delta，漫反射项仍保留；纯金属的漫反射为零。

几何法线 Ng 由三角形确定，用于朝向、射线偏移和反射支撑域；着色法线 Ns 用于 BSDF 正交坐标系，采用 Radiance 传输约定。求值、采样和 PDF 使用一致的合法半球；非法样本贡献为零，不反复重采样后沿用未归一化的 PDF。

每像素共享一次主命中，对非零的漫反射、镜面各抽取一个延续样本。漫反射使用余弦半球提议，`pd=ci/π`；镜面使用 GGX 可见法线提议，`ps=p_h(h|wo)/(4|wo·h|)`。主分量 k 的吞吐量为 `βk=fk·ci/pk`；其余顶点按完整 BSDF 采样，`pB=Σ qj pj`、`β←β·f·|Ns·wi|/pB`，所有非零分量都有正的选择概率。两路标签由主分量决定，随后散射类型变化不更换标签；最终两路相加，不除以二。

每个命中先计入到达处的发光，未命中则计入环境；本资产表面 Le 为零。非 delta 顶点显式采样光源并追踪遮挡射线。光源与 BSDF PDF 均以单位立体角表示，采用 power heuristic：`wL=pL²/(pL²+pB²)`，BSDF 命中光源使用互补权重。主顶点分别使用本分量的 pk，其他顶点使用实际混合 pB；保存上一散射处的 PDF 和光采样上下文，不能用新命中处的 PDF 替代。Delta 使用离散概率质量，不参加连续光源 NEE，直接可见及 delta 路径命中环境的权重为 1。

路径通过逃逸、零吞吐量或俄罗斯轮盘赌结束，不设固定散射深度截断。从第三次散射更新吞吐量后，以 `s=clamp(maxComponent(β),0.05,0.95)` 和独立随机维度决定存活，存活路径执行 `β/=s`。不裁剪高能量贡献，也不按帧超时丢弃仍存活的路径；每帧工作量通过样本数量与实现效率控制。随机维度按像素、样本序号、路径分支及散射次数分配，不能在各反弹重复使用同一随机数。

主射线从相机位置按当前投影反推像素方向，按相机近、远平面换算 T 范围；颜色与首次命中引导共用该射线。反弹和阴影射线访问完整场景，不受主相机裁面或屏幕边缘限制。命中位置误差结合几何法线确定原点偏移，避免自交，同时保留薄片和近接触遮挡。

颜色与法线纹理通过 ray cone 估计 footprint 并显式选择 mip：由像素角尺寸初始化，随传播与粗糙散射扩展，经三角形 UV 映射转换为纹理足迹。MASK 交集使用基础层 alpha 和统一采样/cutoff，保证各类射线看到同一遮挡几何；拒绝候选不消耗散射次数、不再乘一次 alpha，也不改为连续透明度的随机接受。

### 4.2 太阳与天空

世界单位为米、Y 向上。`TimeOfDay=6..18` 对应表面指向太阳的方向 `-Z → +Y → +Z`，太阳角半径为 0.27°。太阳使用有限角直径，半影由光源方向采样和几何遮挡形成。

天空采用 Rayleigh/Mie 散射及多次散射近似的大气 LUT，在场景中心的固定观察高度生成 512×256 经纬环境图。该环境作为场景共同的远场照明边界；相机旋转或移动不改变它。环境图保存散射天空，不含锐利太阳盘。太阳盘辐亮度由同一太阳辐照度、角面积和大气透射率推导，所有路径与相机可见背景使用同一太阳/天空模型。

光源采样使用太阳圆锥与天空亮度重要性分布的混合提议，混合概率由两者的积分亮度确定。天空 texel 概率包含其立体角，GPU 生成行、列 CDF；选中 texel 后在其方位角与 cos(天顶角)区间内均匀采样，方向密度为 texel 概率除以其立体角。CDF 与 PDF 使用同一分布，不能把经纬 UV 均匀密度当作立体角密度。无论从哪一提议取样，求值均为该方向完整的太阳加天空辐亮度，混合 PDF 与 BSDF PDF 用于 MIS。一条遮挡射线判定该方向的场景可见性。

太阳或大气参数改变时，在同一帧更新环境图、分布和光照版本后再追踪，避免画面与采样分布属于不同时刻。大气基础 LUT 只随其物理参数更新。

### 4.3 信号与引导

`PathTracingOutput` 提供 DiffuseRadiance、SpecularRadiance、首次命中的几何/材质信息、motion，以及镜面第一段延续路径的命中位置、法线、距离和有效性。两路 RGB 均为非负、未曝光的线性出射辐亮度，直接光与间接光归入对应分量。

漫反射降噪前解调接收面的基础色与非金属系数，合成时乘回相同因子；零贡献分量保持零。镜面保存辐亮度，以材质、粗糙度、反射距离和反射命中引导过滤。主射线未命中时，DiffuseRadiance 存背景 RGB、SpecularRadiance 为零，SurfaceIdentity 标为背景；降噪直接复制该 RGB，不解调、不执行表面滤波。背景不伪造表面、反弹或接触遮蔽。

## 5. 实时降噪

采用 [SVGF：Spatiotemporal Variance-Guided Filtering](https://research.nvidia.com/labs/rtr/publication/schied2017spatiotemporal/)，在公共 Compute RHI 中实现双路方差引导时空滤波。每帧使用新样本、有效历史与当前帧空间邻域输出完整画面，不要求相机静止，也不使用无限增长的平均累积作为交互输出。

DenoisePass 内部执行以下固定处理：

1. 重投影与历史验证：检查视口、线性深度、实例/材质、几何法线、着色法线、粗糙度和历史版本。显露及不兼容位置使用当前帧，不跨表面搬运颜色。
2. 更新颜色与亮度一、二阶矩，计算有效历史长度和方差。漫反射历史最多 32 帧，镜面最多 8 帧；根据运动、方差与照明差异缩短历史，当前样本权重不得为零。亮度矩与对应滤波信号同域：漫反射使用解调信号，镜面使用辐亮度。短历史使用当前 3×3 兼容邻域估计方差，不将显露像素标为零方差。
3. 执行 5×5 核的 à-trous 方差引导滤波，步长为 1、2、4、8、16。权重结合深度梯度、法线、材质、亮度方差和镜面粗糙度；镜面的有效半径受粗糙度约束，近镜面不扩散成宽光斑。每层同时更新颜色和方差，`Vout=Σ(w²Vin)/(Σw)²`；无兼容邻域时保留当前有效样本。
4. 漫反射还原材质调制，与镜面及背景合成 DenoisedHdr。保存时间滤波结果及亮度矩作为历史，空间滤波结果不递归反馈以扩大模糊。

漫反射使用首次命中的表面 motion。镜面 GGX 命中是随机样本，不直接视为稳定光流。低粗糙度且局部平面近似、命中引导可信时，使用反射命中点相对切平面映射出的虚拟位置重投影；环境反射使用方向重投影。按各自坐标验证主表面、反射距离/法线和粗糙度，曲率或随机命中变化造成失配时拒绝或缩短历史。高粗糙度使用受限的表面重投影，两类权重连续过渡，实际移动验收须排除镜面粘连与拖影。

SVGF 与超分是有偏的图像重建，不反馈到 BSDF、光源或路径吞吐量。物理正确性使用未降噪的线性 HDR 样本统计验证，显示稳定性另行验收。火花点通过采样正确性与方差引导处理，不靠固定裁剪路径贡献。降噪适用于三种模式，SGSR2 不承担路径追踪降噪。

## 6. 尺寸、运动与输出

D 为 framebuffer 像素尺寸；`R=max(1,floor(D*RenderScale))`，逐维计算。路径追踪和降噪工作在 R，最终 Color 工作在 D。Renderer 直接读取 `camera.View` 和 `camera.Projection`；相机尺寸由 App 更新，不因 RenderScale 或内部资源尺寸重新生成主相机投影。

| 模式 | 输出链 |
| --- | --- |
| None | DenoisedHdr(R) → Bloom/曝光/色调映射/sRGB；R=D 直写 Color，否则先写 R/LDR，再双线性至 D |
| Spatial | DenoisedHdr(R) → Bloom/曝光/色调映射/sRGB → SGSR1 → Color(D) |
| Temporal | DenoisedHdr(R) → SGSR2 Quality → HDR(D) → Bloom/曝光/色调映射/sRGB → Color(D) |

测光统一读取 Bloom 前的 DenoisedHdr(R)，在 GPU 内归约对数亮度，以实际帧间隔平滑调整曝光，保留室内暗部与室外纹理。Bloom 采用软阈值、多级降采样和上采样，半径按 D 换算；Bloom 仍以未曝光 HDR 表示。ToneMapping 合成后应用曝光、ACES fitted 映射和 sRGB 编码各一次。

Temporal 使用现有 `CreateTemporalUpscaler`，Spatial 使用 `CreateSpatialUpscaler`；使用原扩展算法及其历史，不叠加独立全屏 TAA。SGSR1 输入为 [0,1] 显示颜色；SGSR2 输入为非负线性 HDR，其内部压缩不改变调用方颜色域。降噪输出保持当前 jittered R 采样网格，交由 SGSR2 使用对应 jitter 重建。

| SGSR2 字段 | 数据契约 |
| --- | --- |
| Input / OpaqueInput | 同一张 DenoisedHdr(R)；本场景只有 OPAQUE 与 MASK，没有独立透明合成 |
| Depth | 首个通过 alpha-test 的命中点经当前投影得到 z/w，普通 device Z：near=0、far=1；未命中为 1，不传米深度或 ray T |
| MotionVectors | 当前减上一帧的未 jitter NDC 位移，编码 `m*0.2495+32767/65535`，RG16UNorm；静止为编码中值，零编码表示矩阵回退 |
| JitterOffsetX/Y | 当前输入像素 jitter，单位 R 像素 |
| ClipToPrevClip | `inverse(currentJitteredVP) * (previousUnjitteredVP * currentJitterMatrix)` |
| PreExposure | 固定 1；显示曝光不乘入路径、降噪或 SGSR2 历史 |
| CameraFovAngleHor | `1 / unjitteredProjection.M11`，即水平半视角正切 |
| SameCamera / MinLerpContribution | 未 jitter 相机是否相同 / 0；按扩展契约提供，不用于补偿降噪 |
| Reset | 与渲染历史失效一致，尺寸或模式变更时重建对应扩展 |

矩阵采用 row-major、行向量，`ViewProjection=View*Projection`。Temporal 使用 Halton(2,3) 八相序列减 0.5，投影 NDC 偏移为 `(2*jx/Rw,-2*jy/Rh)`；None/Spatial 不改变投影。只调整 Renderer 的 GPU 投影副本，不写回相机。

降噪内部 motion 定义为 `previousUnjitteredUv-currentUnjitteredUv`；查上一帧纹理时额外加 `previousJitter/R-currentJitter/R`。转换给 SGSR2 时 `m=(-2*motion.x,2*motion.y)`。天空用方向 w=0 计算旋转 motion。超出编码范围的运动按扩展协议使用零编码回退，不依赖 UNorm 饱和。

## 7. 数据、资源与生命周期

| 数据 | 内容 |
| --- | --- |
| SceneData | SceneResources 拥有的几何、索引、材质、实例表、纹理及 TLAS 借用引用；场景内容版本 |
| EnvironmentData | 太阳参数、天空辐亮度、重要性分布、大气参数与光照版本 |
| FrameData | 当前/上一帧矩阵及逆矩阵、相机位置/朝向、R/D、jitter、帧序号、实际帧间隔、历史有效性与内容版本 |
| PathTracingOutput | 两路原始信号、当前表面引导、镜面命中引导、背景有效性 |
| DenoiseOutput | 降噪后的线性 HDR；历史与中间资源由 DenoisePass 自行拥有 |

工作纹理采用以下统一语义，三后端不另设格式或算法分支：

| 资源 | 格式与尺寸 |
| --- | --- |
| 原始/时间滤波/空间滤波两路 RGB | R / RGBA32Float；RGB 为辐亮度或明确解调的漫反射信号，A 为对应命中距离 |
| 亮度矩与方差 | R / RG32Float（一、二阶矩）、R32Float（方差），漫反射与镜面分开 |
| 历史长度 | R / R16UInt，两路分开；当前与上一帧双缓冲 |
| DeviceDepth / ViewDepth | R / R32Float；分别为普通 device Z 和正的相机空间米深度；背景为 1 / 相机 far，由身份标志区分 |
| SurfaceNormal / ShadingNormalRoughness | R / RGBA16Float；世界空间几何法线、世界空间着色法线与粗糙度 |
| BaseColorMetallic | R / RGBA16Float；线性基础色与金属度 |
| SurfaceIdentity | R / RGBA32UInt；实例、primitive、材质与命中/朝向标志 |
| Motion / EncodedMotion | R / RG32Float（有符号 UV）、RG16UNorm（SGSR2 编码） |
| SpecularHitPositionDistance / Normal | R / RGBA32Float、RGBA16Float；表面存位置/距离及法线，天空存方向/零距离；Normal.w 标记无效、表面或天空 |
| DenoisedHdr / 时域超分 HDR | R / RGBA32Float、D / RGBA32Float；扩展内部资源由扩展所有 |
| Color / R 尺寸显示中间图 | D / RGBA8UNorm、R / RGBA8UNorm；内容已 sRGB 编码 |
| 天空环境图 / 采样分布 | 512×256 / RGBA32Float；R32Float 条件 CDF 与行边缘分布 |
| 曝光 | 1×1 / R32Float；归约中间图由 ExposurePass 所有 |
| Bloom 金字塔 | 从 ceil(D/4) 开始 / RGBA16Float；所选输出分支分辨率用于足迹换算 |

上一帧引导只保留重投影验证实际需要的深度、身份、法线/粗糙度与镜面命中信息。空间滤波采用固定 ping-pong，临时方差与颜色不为每次 Dispatch 单独常驻一套。资源尺寸以 Texture.Desc 为准；内存预算覆盖这些常驻图、历史、加速结构、材质、扩展内部资源和构建临时峰值。

GPU 常量使用显式布局的文件尾 `file struct`，字段偏移、大小及 padding 与 Slang 对齐，GPU 布尔用 uint。`SetConstantBuffer` 第二参数是字节偏移；同帧不同用途使用独立缓冲或独立对齐区域，不能覆盖尚未消费的数据。

- 无参构造从 App 获取 GraphicsContext 和 D，返回前建立可供 UI 绑定的 Color；构造时不读取尚未由对象初始化器赋值的 Settings。首个 Update 根据设置快照建立 R 尺寸资源，Render 写完 Color 并使其可采样，再由 App 绘制 UI；不要求宿主添加空值判断或额外初始化调用。
- Update 获取设置和相机快照；完整记录一帧后才交换历史、推进帧号及 jitter。
- 几何、材质、光照、分辨率、模式或投影不连续时重置相关降噪与超分历史；连续移动通过重投影处理。普通曝光适应不重置未曝光历史。
- Resize 只重建尺寸相关资源；最小化不推进历史，恢复时处理时间间隔和历史失效。静态场景加速结构与天空数据不随窗口大小重建。
- 逐帧渲染由 App 单次 `Submit().Wait()`；Pass 只录制计算、Transition 与 Barrier。SceneResources 的上传及加速结构构建等待属于一次性加载，不放入逐帧调度。
- 新纹理从 Undefined 开始，记录真实 layout；跨 Pass 采样前进入 Sampled。采样输入和写目标不为同一子资源，当前与历史不混用。
- UI 使用本帧开始时绑定的 Color；替换 Color 后须等待该绑定对应的提交完成再释放。视图先于底层资源释放，TLAS 先于 BLAS 释放，几何缓冲最后释放；借用输入及 CommandBuffer 不释放。
- 稳态帧不创建管线、纹理或场景数组；完整释放降噪历史、超分实例、加速结构及场景资源。

## 8. 验收

- 几何命中与 glTF 变换、材质、法线和 MASK 一致，叶片与链条的主可见性、反射及阴影轮廓正确。
- 直接光、天空、反弹和反射遵守同一能量与颜色语义；无漏光、重复计光、自交、悬浮阴影或材质引起的错误亮斑。
- 验证 BSDF 非负、互易及半球积分不超过 1；检查白炉子、纯漫反射解析值、纯金属零漫反射、delta 极限、掠射与法线扰动。PDF 归一化检查须包含无效样本和 delta 概率质量。
- 对相同光源与材质分别使用 BSDF-only、NEE+MIS、不同提议概率及 RR 参数，高样本均值应在统计误差范围内一致。采样直方图须匹配所报告 PDF，两路分量之和须匹配完整估计。
- 使用 PBRT 官方实现的对应模型及独立解析用例核对线性 HDR，匹配参数、色彩空间、纹理过滤和相机采样。室内外、侧廊、曲面及镜面比较记录均值、方差、置信区间和误差；同一实现的高样本图不能单独证明自身正确，高样本累积不替代实时输出。
- 缓慢移动、快速转向、停止和显露时实时降噪有效，无持续颗粒、明显拖影、过度模糊或曝光抽动；降噪与超分分别对照，不能用后处理掩盖光传输错误。
- 覆盖 None/Spatial/Temporal 与 1/.75/.5 比例、6/9/12/18 时刻、奇数尺寸、连续 Resize、最小化恢复、模式切换和退出。
- 性能目标为 Apple M4、D=2560×1440、Temporal/.5 在持续移动时达到 30 FPS。记录真实整帧时间、样本与 RR 参数、散射次数分布、内存和启动开销；静止读数不代替移动结果，FPS 不代替 GPU 时间。
- 构建无警告和错误；着色器编译、资源同步、加速结构与 RayQuery 在可用设备上运行验证。图像与历史无 NaN/Inf、越界、失效句柄或持续资源增长；未运行的后端不宣称通过。

## 9. 参考

- [PBRT：路径追踪、MIS 与俄罗斯轮盘赌](https://pbr-book.org/4ed/Light_Transport_I_Surface_Reflection/A_Better_Path_Tracer)
- [PBRT：FresnelBlend 材质耦合](https://pbr-book.org/3ed-2018/Reflection_Models/Fresnel_Incidence_Effects)
- [PBRT：微表面分布与采样](https://pbr-book.org/4ed/Reflection_Models/Roughness_Using_Microfacet_Theory)
- [SVGF：实时路径追踪重建](https://research.nvidia.com/labs/rtr/publication/schied2017spatiotemporal/)
- [glTF 2.0 材质规范](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html#materials)
- [天空大气模型](https://github.com/sebh/UnrealEngineSkyAtmosphere)
- 公共接口与用例：`Zenith.NET/CommandBuffer.cs`、`Zenith.NET/ZenithCompiler.cs`、`Experiments/CornellBox/Renderers/PathTracingRenderer.cs`、`Extensions/Zenith.NET.Extensions.Upscaling`。
