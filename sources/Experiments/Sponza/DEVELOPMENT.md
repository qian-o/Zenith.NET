# Sponza 路径追踪渲染设计

## 1. 目标与边界

实现可自由移动的 Sponza 实时路径追踪场景，采用经典 SVGF 降噪，提供原生、空间超分和时域超分输出。主可见性、直接光、多次反弹、反射与阴影统一通过硬件光追计算；接触遮蔽包含在光传输中。场景为静态 glTF PBR 表面、太阳和简单天空，显示使用固定曝光与色调映射。

光传输遵循 PBRT 的渲染方程与蒙特卡洛估计，正确性针对本文定义的 RGB 材质和光源验证。法线贴图、纹理足迹和天空模型属于建模近似，不等同于光谱渲染或 PBRT 的全部功能。每帧产生新样本，普通移动通过有效历史重投影继续出图。

| 范围 | 约定 |
| --- | --- |
| 实现 | 完成 Renderer.cs，在现有 Passes、Models、Helpers、Assets/Shaders 中添加必要文件 |
| 保持不变 | App、Program、Handlers、CocoaHelper、ImGuiHelper、RenderSettings、UpscalingMode、模型与字体资产，以及现有相机、输入、窗口和 UI 操作 |
| 项目边界 | 不修改 Sponza 外代码、RHI、后端、共享扩展、工程、依赖或目录结构；遇到范围外阻挡时提供证据并告知用户 |
| 接口 | 只用公共 RHI 与现有扩展，不获取原生图形句柄、不强转后端、不通过反射或原生调用补充能力 |
| 文档与验证 | 本文为项目设计；验证程序、日志、截图放仓库外，不加入项目或解决方案 |

保留 Renderer 的无参构造、外部只读 Color、`public RenderSettings Settings;`、`Update(CameraHandler camera)`、`Render(CommandBuffer commandBuffer)`、`Resize(uint width, uint height)` 和 `Dispose()`。App 在构造后设置 `None / RenderScale=1 / TimeOfDay=12`。

遵循 [Zenith.NET 代码风格与规范](<../../../Zenith.NET 代码风格与规范.md>)，重点包括公共字段数据结构、GPU 常量布局、单行优先及实验项目的 IDisposable。实现前阅读核心 RHI、三个后端、相关扩展、Sponza 宿主和 CornellBox 光追调用，核实 API、同步与资源生命周期。CornellBox 用作调用与组织方式参考，光传输依据本文及 PBRT。

## 2. 三个 Pass

```text
PathTracingPass → DenoisePass → OutputPass → Color
```

| Pass | 输入 → 输出 | 实现 |
| --- | --- | --- |
| PathTracingPass | 场景、相机、光照 → 单路原始 HDR 与主命中引导 | 一次 Compute Dispatch，通过 RayQuery 完成每个样本的一条完整路径 |
| DenoisePass | 原始 HDR、引导、历史 → 降噪 HDR | 经典 SVGF 的重投影、方差估计和四轮空间滤波 |
| OutputPass | 降噪 HDR、输出设置 → Color | 调用现有 SGSR，完成固定曝光、色调映射和显示转换 |

SceneResources 位于 Helpers，负责模型加载、纹理、几何、材质与 BLAS/TLAS 所有权。Renderer 负责设置、尺寸、历史失效与三个 Pass 的调用顺序，并拥有最终 Color。场景资源初始化不列入逐帧 Pass。

三个 Pass 放 Passes，其内部步骤用私有方法与着色器入口组织。SVGF 和超分保留算法所需的多次 GPU 调度。Pass 只录制命令，不读取 App、Settings 或 Camera，不调用其他 Pass，不自行提交或等待；所需 context 和数据显式传入。

Models 只承载跨模块共享的 SceneData、FrameData、PathTracingOutput 及实际所需的数据结构，不为每次调度增加包装类型。资源以具名引用借用，接收方不释放；DenoisePass 直接返回纹理。加入实际文件时删除对应目录的占位文本，不建立通用渲染框架。

## 3. 场景资源

以 `AppContext.BaseDirectory` 定位 Assets，使用现有 SharpGLTF.Core 加载 Sponza.gltf。按 accessor、primitive、材质引用与节点变换读取数据；顶点保留物体空间位置、法线、切线和 UV，节点变换只应用一次。法线用逆转置，切线保留手性并处理镜像变换；缺失或退化切线由有效几何和 UV 构造。

纹理通过现有 ImageSharp 扩展加载并生成 mip。基础色使用 `compand=true`，法线与金属粗糙度使用 `compand=false`，缓存键包含图像引用和颜色语义。基础色只做一次 sRGB 解码，alpha 保持线性；粗糙度取 G、金属度取 B，并应用 glTF 材质因子。法线贴图应用 NormalScale、TBN 与归一化。

每个共享 primitive 建一个 BLAS，每个节点中的 primitive 建一个 TLAS 实例，使用 PreferFastTrace 和 `0xFF` 可见性掩码。实例表保存材质、顶点与索引范围、世界与法线变换；按实例 ID、primitive index、重心坐标重建命中属性，分别处理局部 UInt16 索引和顶点基址。

OPAQUE 几何标为 opaque，MASK 标为 non-opaque。主射线、反弹射线和阴影射线共用候选命中的 alpha-test，使用基础 mip 的相同 alpha、采样方式和 cutoff；拒绝候选不消耗散射次数，不再乘一次 alpha。双面材质按入射侧构造着色基底；单面背面吸收并终止路径，阴影射线仍视其为遮挡。实例绕序正确处理镜像变换。

几何上传后，在加载期借用公共队列 CommandBuffer，依次构建 BLAS、TLAS，同一构建批次 `Submit().Wait()` 完成后发布 SceneData。现有加载器的上传同步仅发生在加载期。相机、太阳、窗口、RenderScale 与普通材质参数变化不重建 AS；只有几何、实例变换或 OPAQUE/MASK 分类改变才重新建立场景资源。要求 `RayTracingSupported=true`。

## 4. 路径追踪

### 4.1 材质与采样

使用非负线性 RGB 和一致的相对辐射单位，方向均从表面向外。积分目标为 `Lo=Le+∫ f(wo,wi) Li(wi) |n·wi| dω`，可见性由射线求解。BSDF 求值、采样、PDF 和 delta 标志保持一致。

材质采用 [PBRT FresnelBlend](https://pbr-book.org/3ed-2018/Reflection_Models/Fresnel_Incidence_Effects)，分布为 Trowbridge–Reitz（GGX）。线性基础色 C、金属度 m 对应 `Rd=(1-m)C`、`Rs=lerp(0.04,C,m)`，均在 [0,1]；感知粗糙度映射 `alpha=roughness²`。令 `ci=|Ns·wi|`、`co=|Ns·wo|`、`h=normalize(wi+wo)`，同侧非 delta 求值为：

```text
F(c) = Rs + (1-Rs)(1-c)^5
fd = 28/(23π) · Rd(1-Rs) · [1-(1-ci/2)^5] · [1-(1-co/2)^5]
fs = D_GGX(h) F(|wo·h|) / [4|wo·h| max(ci,co)]
f  = fd + fs
```

镜面项沿用此分母，不额外乘 Smith G；可见法线采样的 G1 属于采样 PDF，BRDF 值不裁到 1。与 PBRT 对照时直接匹配 alpha，不再次映射粗糙度。零粗糙度镜面使用权重 F(co) 的 delta，保留非零漫反射；纯金属没有漫反射。Ng 用于几何朝向、支撑域和原点偏移，Ns 用于 Radiance 模式的着色坐标系；非法半球样本贡献为零，不重采样后沿用原 PDF。

每个内部像素每帧产生一个路径样本。每个顶点只选择一条散射延续，完整 BSDF 使用漫反射与镜面提议的混合采样；两项非零时各以 1/2 概率选择，仅一项非零时选择概率为 1。漫反射提议 `pd=ci/π`，GGX 可见法线提议 `ps=p_h(h|wo)/(4|wo·h|)`；连续密度 `pB=qd·pd+qs·ps`，吞吐量更新为 `β←β·f·ci/pB`。Delta 用离散选择概率更新吞吐量，不能代入连续密度。

命中发光表面或逃逸到环境时计入相应贡献；本资产表面 Le 为零。存在非 delta 分量的顶点采样一个光源方向并追踪遮挡射线，与 BSDF 采样采用 power MIS。两类 PDF 均为单位立体角密度；BSDF 命中光源使用上一散射点的实际 PDF 与光源采样上下文计算互补权重。直接可见光源和经 delta 到达光源的权重为 1。直接光和后续反弹累加到同一 HDR，不分裂成两条路径。

路径通过逃逸、零吞吐量或俄罗斯轮盘赌结束，不设固定散射深度截断。从第三次散射更新吞吐量后，以 `s=clamp(maxComponent(β),0.05,0.95)` 决定存活，存活时 `β/=s`。不裁剪路径贡献，不通过增大材质粗糙度或丢弃超时路径控制噪声。BSDF、光源与轮盘赌使用独立随机维度，随机状态按像素、帧号和散射次数区分。

### 4.2 主射线与纹理

主射线由相机矩阵和亚像素位置生成，颜色、深度、法线和 motion 共用同一次主命中。近远平面只约束主可见性，反弹与阴影射线访问完整场景。原点偏移根据命中位置误差与 Ng 确定，保留薄片和近接触遮挡。

颜色与法线贴图采用 ray cone 估计纹理足迹：由像素角尺寸初始化，随距离和粗糙散射扩展，经三角形 UV 映射选择 mip。主射线的抗锯齿采样遵循第 6 节；MASK 的遮挡判定保持第 3 节约定。

### 4.3 太阳与天空

世界单位为米、Y 向上。`θ=π(TimeOfDay-6)/12`，`sunDirection=(0,sinθ,-cosθ)`，6/12/18 时对应 −Z/+Y/+Z。太阳为半角 `α=0.27°` 的均匀圆盘，法平面辐照度 E 对应 `Lsun=E/(π sin²α)`，半影由遮挡射线自然产生。

天空为 `Lsky(ω)=lerp(Lbottom,Ltop,(ω.y+1)/2)`；Lbottom、Ltop、E 是固定非负光照参数。主背景、路径逃逸和光源采样共用天空加太阳的完整环境求值，不建立环境预计算资源。

以均匀球面和均匀太阳圆锥混合采样。`Ωsun=4π sin²(α/2)`，`pL(ω)=(1-p)/(4π)+p·1cone/Ωsun`；天空权重 `Wsky=2π[Y(Lbottom)+Y(Ltop)]`，太阳权重 `Wsun=Y(Lsun)Ωsun`，`p=Wsun/(Wsky+Wsun)`。全零环境返回零贡献。两种提议均按方位角与 cos(极角)均匀采样，并使用完整环境值和完整混合 PDF 做 MIS。光照变化使历史失效，不重建 AS。

## 5. SVGF 实时降噪

算法依据 [SVGF 论文](https://research.nvidia.com/labs/rtr/publication/schied2017spatiotemporal/)，代码参考固定为 [Falcor SVGFPass，提交 eb540f6748774680ce0039aaf3ac9279266ec521](https://github.com/NVIDIAGameWorks/Falcor/tree/eb540f6748774680ce0039aaf3ac9279266ec521/Source/RenderPasses/SVGFPass)。在公共 Compute RHI 中移植滤波流程，保留源码来源和必要许可。该参考不作为新增工程或 SDK 依赖。

过滤信号为单路、未曝光的完整 HDR 辐亮度。参考接口中的 Albedo 固定为 1，作为常量省略纹理；本场景表面 Emission 为零。滤波前后不进行材质颜色解调，纯金属的反射保留在同一信号中。主射线未命中时标为背景，直接输出解析环境颜色，不参与表面滤波。

DenoisePass 保留以下处理链；各项为内部计算步骤，不新增 Pass 类型：

1. 准备几何引导：由主命中深度、法线和身份生成局部深度与法线变化量，重建当前命中在前帧的预期线性深度。当前邻域差分只读取已完成的主命中结果，不跨轮廓扩大容差。
2. 重投影并验证历史：四个双线性 tap 分别检查实际坐标、表面身份、法线及前帧深度，有效权重重新归一；无有效 tap 时按参考流程检查前帧 3×3 兼容邻域，仍无有效样本则使用当前帧。深度比较为“前帧存储深度与当前命中在前帧的预期深度”，容差也在前帧深度坐标系计算。
3. 时间累积与方差：历史计数 `h=min(32,previousH+1)`，无历史时 h=1；颜色当前权重 `max(0.05,1/h)`，亮度一、二阶矩当前权重 `max(0.2,1/h)`。方差为 `max(0,M2-M1²)`；h<4 时按参考的 7×7 保边邻域滤颜色与矩，以 `4/h` 放大所得非负方差。这里的 32 是指数累积的计数上限，不是固定窗口平均。
4. 空间滤波与反馈：四轮 5×5 à-trous，步长 1、2、4、8；核、方差预滤和深度/法线/亮度权重沿用固定参考，PhiColor=10、PhiNormal=128。每轮同时传播颜色与方差，`Vout=Σ(w²Vin)/(Σw)²`。第四轮输出当前图像；FeedbackTap=1，即第二轮颜色用于下帧历史。亮度矩保存时间累积结果，不以空间滤过的矩覆盖。

移植需正确处理 Compute 邻域差分、像素中心、jitter 和深度坐标，不直接复制参考的像素导数调用。所有 tap 先检查边界与有效性，历史长度从实际通过验证的位置取得；历史双缓冲，读写分离。调参与改动以固定图像对照和参考流程为依据。

SVGF 与超分均为有偏图像重建，不改变原始 BSDF、光源与路径吞吐量。单路辐亮度滤波会同时处理纹理颜色，低采样下存在细节变软、运动镜面失配和暗部残余噪声的限制；这些列入实际验收，不能用延长历史或增强模糊掩盖。本文不把参考论文的画质或性能当成本项目已验证结果，也不引入反射专用重投影系统。

## 6. 分辨率、运动与输出

D 为 framebuffer 像素尺寸，`R=max(1,floor(D*RenderScale))`，逐维计算。路径追踪和 SVGF 工作在 R，Color 工作在 D。Renderer 读取相机矩阵，在内部副本上施加 jitter，不修改 Camera 或按 R 重建相机投影。

三种模式均使用 Halton(2,3) 八相减 0.5 的亚像素采样，jitter 单位为 R 像素，投影 NDC 偏移 `(2*jx/Rw,-2*jy/Rh)`。颜色与引导保留当前 jittered R 网格；Temporal 将同一 jitter 传入 SGSR2，不叠加第二次像素抖动或独立 TAA。

矩阵使用 row-major 与行向量。主命中 motion 为 `previousUnjitteredUv-currentUnjitteredUv`；降噪读取历史时加 `previousJitter/R-currentJitter/R`，重建世界位置时使用当前实际采样坐标和对应矩阵。天空 motion 只包含方向的旋转变化。相机切换、投影不连续、内容或尺寸变化重置历史，普通相机移动使用重投影。

OutputPass 固定曝光默认为 1，ACES fitted 色调映射和 sRGB 编码各做一次；曝光只影响显示，不改变路径和历史的辐射单位。None/Spatial 在显示转换时以 `sourceUV=outputUV+currentJitter/R` 双线性采样 HDR，恢复稳定网格，边界使用 ClampToEdge；R=D 时也进行此采样。Temporal 保留 jittered 输入，由 SGSR2 使用同一 jitter 完成重建。输出顺序如下：

| 模式 | 输出链 |
| --- | --- |
| None | 降噪 HDR(R) → 按稳定 D 网格采样、曝光/色调映射/sRGB → Color(D) |
| Spatial | 降噪 HDR(R) → 按稳定 R 网格采样、曝光/色调映射/sRGB → R/LDR → SGSR1 → Color(D) |
| Temporal | 降噪 HDR(R) → SGSR2 Quality → HDR(D) → 曝光/色调映射/sRGB → Color(D) |

直接调用现有 CreateSpatialUpscaler、CreateTemporalUpscaler，扩展原样使用。SGSR1 接收 [0,1] 显示颜色；SGSR2 接收非负线性 HDR，内部按 PreExposure 进行压缩与反压缩，不承担路径追踪降噪。

| SGSR2 输入 | 契约 |
| --- | --- |
| Input / OpaqueInput | 同一降噪 HDR；场景只有 OPAQUE/MASK |
| Depth | 主命中的普通 device Z，near=0、far=1，背景为 1 |
| MotionVectors | 当前减上一帧的未 jitter NDC 位移 m；由上述 UV motion 转换为 `m=(-2*motion.x,2*motion.y)`，编码 `m*0.2495+32767/65535` 到 RG16UNorm；超出编码范围时使用零编码触发矩阵回退，静止为编码中值 |
| JitterOffsetX/Y | 当前 R 像素单位 jitter，与主射线一致 |
| ClipToPrevClip | `inverse(currentJitteredVP)*(previousUnjitteredVP*currentJitterMatrix)` |
| PreExposure / CameraFovAngleHor | 1 / `1/unjitteredProjection.M11`，后者为水平半视角正切 |
| SameCamera / MinLerpContribution / Reset | 未 jitter 相机是否相同 / 0 / 相关历史是否失效 |

## 7. 资源与生命周期

SceneData 借出几何、材质、实例、纹理和 TLAS 引用；FrameData 保存相机、尺寸、jitter 与历史状态；PathTracingOutput 借出原始 HDR 和主命中引导。Pass 录制常量时读取资源 Handle，GPU 常量以文件尾显式布局 file struct 与 Slang 对齐，布尔使用 uint。

| 资源 | 所有者与内容 |
| --- | --- |
| 场景资源 | SceneResources；BLAS/TLAS、几何、材质和纹理 |
| 原始 HDR 与引导 | PathTracingPass；RGBA32Float 辐亮度、R32Float device Z 与正线性深度、RGBA16Float 世界空间着色法线 Ns、RG32Float UV motion、实例/材质身份；保留历史验证需要的前帧引导，背景使用明确身份标志 |
| SVGF 内部资源 | DenoisePass；单套颜色历史、时间矩、历史计数、局部变化量及滤波 ping-pong；辐亮度与矩使用 32 位浮点，颜色/方差可共用 RGBA，计数可随矩打包；背景不写入有效表面历史 |
| 输出资源 | OutputPass；所需 R/LDR、D/HDR 中间图、RG16UNorm 编码 motion 与共享超分实例；超分内部纹理由扩展拥有 |
| Color | Renderer；D / RGBA8UNorm，存 sRGB 编码值，仅 D 改变时替换；模式与 RenderScale 改变复用 Color |

背景的 device Z 为 1，线性深度为相机 far，依靠身份标志跳过表面计算。前帧预期深度通过当前 device Z、逆矩阵和前帧视图重建，不保存反射命中或世界位置历史。每个缓存都有明确消费者，不保存主着色后已无用途的材质中间量。

- 无参构造返回前建立有效 Color；Settings 在构造后赋值，设置相关内部资源由首次 Update 建立。App 在 Renderer.Update 前绑定 Color，尺寸变化替换旧 Color 时保留其有效期至已绑定它的提交完成。
- 完整录制一帧后交换历史、推进帧号和 jitter。Resize、比例、模式、光照或相机不连续使相关历史失效；最小化不推进历史，恢复时处理失效。
- 逐帧借用 App 的 CommandBuffer，统一由 App 单次 Submit/Wait；加载期完成上传和 AS 构建。借用 CommandBuffer 不保留、不释放。
- 采样输入和写目标分离，按真实 layout 记录计算依赖。SetConstantBuffer 的第二参数为字节偏移，同帧不同参数使用独立常量区域，不覆盖未消费的常量。
- 稳态不创建管线、纹理或场景数组；尺寸以 Texture.Desc 为准，预算包含历史、纹理、AS 与构建峰值。最后一次使用完成后，按视图先于纹理、TLAS 先于 BLAS、AS 先于几何缓冲释放。

## 8. 验收与验证

固定参考版本、光照和验证机位，对照原始 HDR、SVGF 输出与超分输出。调试视图、统计和高样本参考由仓库外验证使用，不改变现有 UI 或工程结构。

- 几何变换、法线、材质、MASK 与资产一致；叶片和链条在主可见性、反射及阴影中的轮廓正确，无漏光、自交、重复计光或错误亮斑。
- BSDF 验证非负、互易、反射率半球积分不超过 1 及白炉子；覆盖纯金属、delta、掠射和法线扰动。PDF 检查包含无效样本及 delta 概率质量。
- 使用 PBRT 对应材质和独立解析用例核对未降噪线性 HDR。相同场景的 BSDF-only、NEE+MIS、不同混合概率与 RR 参数，其高样本均值应在统计误差内一致；同一实现的高样本图不单独证明物理正确。
- SVGF 检查静止、缓慢移动、快速转向、停止与显露区域；移动时噪声受控，纹理与轮廓清晰，无明显持续拖影或过度模糊，记录残余噪声和细节损失。纯金属贡献不得被解调丢失；以固定参考和一致输入验证滤波步骤，不能用静止累积替代移动验收。
- 覆盖 None/Spatial/Temporal、1/.75/.5 比例、6/9/12/18 时刻、奇数尺寸、连续 Resize、最小化恢复、模式切换与退出；超分前后曝光和颜色语义一致。
- 性能目标为 Apple M4、D=2560×1440、Temporal/.5 持续移动时达到 30 FPS，作为实测验收目标。记录整帧时间、各模块耗时、样本数量、散射次数分布、内存及启动开销；区分 CPU/提交等待与可用 GPU 计时，不以 FPS 替代 GPU 时间或扩大后端修改范围。
- 构建和着色器编译无警告、错误；在可用设备上验证 RayQuery、同步与生命周期，无 NaN/Inf、越界、失效句柄或持续资源增长。未运行的后端不宣称通过；确认接口或性能阻挡时报告证据及影响。

## 9. 参考

- [PBRT：路径追踪、MIS、俄罗斯轮盘赌](https://pbr-book.org/4ed/Light_Transport_I_Surface_Reflection/A_Better_Path_Tracer)
- [PBRT：FresnelBlend](https://pbr-book.org/3ed-2018/Reflection_Models/Fresnel_Incidence_Effects)
- [PBRT：微表面与可见法线采样](https://pbr-book.org/4ed/Reflection_Models/Roughness_Using_Microfacet_Theory)
- [SVGF 论文](https://research.nvidia.com/labs/rtr/publication/schied2017spatiotemporal/)与第 5 节固定的 Falcor 源码
- [glTF 2.0 材质规范](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html#materials)
- 仓库接口：Zenith.NET/CommandBuffer.cs、Zenith.NET/ZenithCompiler.cs、Experiments/CornellBox/Renderers/PathTracingRenderer.cs、Extensions/Zenith.NET.Extensions.Upscaling。
