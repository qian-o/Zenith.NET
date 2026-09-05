# Sponza 超分展示实验开发文档

## 1. 目标与边界

做一个画面好看、结构简单的 Sponza 光栅化 PBR 场景，用来展示 Zenith.NET 的空间和时域超分。材质、阳光与程序化天空负责提供清晰的场景细节；开发重点是超分接入、画质对比和性能表现。

本轮采用四个简单 Pass，不做 CSM/PCSS、AO、局部环境探针、体积雾、SSR、Bloom 或独立 TAA，也不将它们列为后续必做项。禁止光追。画质主要靠正确的材质、稳定的阴影、合适的环境光和色调映射完成。

保留现有实验结构和调用方式：

- `RenderSettings` 是普通结构体，Renderer 通过 `public RenderSettings Settings;` 持有，由 App 初始化并直接编辑。
- 控件继续放在 App 的 `ImGuiHelper.Settings(Action)` 回调中；不修改 ImGuiHelper、CameraHandler、窗口、输入、交换链和提交逻辑。
- 保留 Color 的直接绑定，以及现有 `renderer.Update(camera)` / `Render(commandBuffer)` / `Resize(width, height)` 调用位置和签名。
- 保留 App 初值 `RenderScale=1`、`UpscalingMode=None`、`TimeOfDay=12`。不修改原始资产或共享框架，不为本实验复制或重写整套超分算法。

当前设置和模型已经到位，Renderer/Pass/Shader 仍是骨架。以下是待开发规格，不能把资产存在或 C# 编译成功当作渲染已完成。

## 2. 使用当前模型

从 `Path.Combine(AppContext.BaseDirectory, "Assets", "Models", "Sponza.gltf")` 加载，bin 和贴图按 glTF 相对 URI 解析。使用现有 SharpGLTF.Core 和 ImageSharp 依赖；无需下载模型、修改 csproj 或建设通用资产系统。

当前模型有 103 个 primitive、25 个材质，引用 65 张图像。只保留这些会影响实现的约定：

- 根节点 scale 约为 `0.008`，只应用一次；处理后场景约为 `30 × 12 × 18 m`。沿用 App 的现有相机，阴影和光照使用变换后的世界坐标。
- 按 primitive 建立绘制记录并保留材质索引；输入索引是 UInt16，合并缓冲区时正确处理局部索引或 baseVertex。通过 accessor 接口读取，不能假定 buffer 紧密排列。
- 保留原有 NORMAL/TANGENT/UV。`plain_white` 没有法线贴图，其 primitive 缺少切线，可以直接使用几何法线。
- 当前三个 MASK 材质为 `ivy_leaves`、`flowers_and_leaves`、`hanging_chain`，均为双面、cutoff=0.5；其余为 OPAQUE，没有 BLEND。
- baseColorFactor 约为 `(0.588, 0.588, 0.588, 1)`，需要保留。按 glTF 默认值补齐缺失参数；`plain_white` 的 metallicFactor=0。当前没有 AO 或 emissive 贴图，不从其他通道猜测。
- base color 以 sRGB 采样，normal/metallic-roughness 以线性数据采样；roughness 在 G、metallic 在 B。本实验的 TextureHelper 按用途创建纹理和 mip，颜色 mip 在线性空间过滤，法线采样后归一化，不改共享 ImageSharp 扩展。
- 只加载 glTF 实际引用的图像并缓存复用。模型更新后目录中可能保留旧贴图，不能扫描整个目录或靠文件名猜材质映射。

数量和材质名称用于当前资产核对，不写死到加载器。来源记录简要放入最终 README，只填写可核实的信息。

## 3. 四个 Pass

| Pass | 职责 | 复杂度约束 |
| --- | --- | --- |
| `ShadowPass` | 一张太阳深度阴影图 | 单张 2048² 正交 shadow map，固定 3×3 PCF，无级联和 blocker search |
| `ScenePass` | 前向 PBR、程序化天空、深度和运动向量 | 场景一次前向 MRT；天空在同一 Pass 内绘制，无预深度/G-buffer/屏幕空间效果 |
| `ToneMappingPass` | 曝光、色调映射和输出编码 | 单次全屏绘制，无 Bloom 链、自动曝光或额外锐化 |
| `UpscalingPass` | None/Spatial/Temporal 三条输出路径 | None 为双线性基线；Spatial/Temporal 调用现有扩展，自身只做接入 |

**ShadowPass：** 用世界 AABB 拟合太阳正交视锥，保留少量边界。光照固定时投影不跟随相机，避免阴影游动。使用普通深度比较、少量 slope/normal bias 和固定 PCF；MASK 裁剪与 ScenePass 一致。阴影分辨率在超分对比中保持不变。

**ScenePass：** 使用 GGX、Smith visibility、Schlick Fresnel 的 metallic-roughness PBR，一个太阳加可调强度的天空/地面半球环境光。环境镜面项直接采样同一个程序化天空函数，以粗糙度减弱方向性并展宽亮区，再结合 Fresnel；这是为观感服务的近似，不生成环境 cube、预过滤 mip 或 BRDF LUT。让石材、布料和金属有区别，避免阴影死黑或环境光把整个场景抹平。

程序化天空只需要天顶/地平线渐变、太阳盘与柔和光晕。TimeOfDay 同时控制天空和太阳方向/颜色，6..18 点平滑变化；本轮不做云动画或天气系统。环境照明采样时排除锐利太阳盘，避免与太阳直射重复叠加。

**ToneMappingPass：** 线性 HDR 乘固定或手动曝光，使用一种固定 filmic 曲线（默认 ACES fitted 近似），最后做一次 sRGB 编码。三种模式保持相同参数，保留石材和织物细节。

文件沿用现有分类：

```text
Sponza/
├── App.cs / Renderer.cs             # 宿主 / 顺序编排、资源尺寸和每帧数据
├── Models/                         # RenderSettings、UpscalingMode、场景数据
├── Helpers/                        # 现有 helper；新增必要的模型/纹理加载辅助
├── Handlers/                       # 保持现有实现
├── Passes/
│   ├── ShadowPass.cs
│   ├── ScenePass.cs
│   ├── ToneMappingPass.cs
│   └── UpscalingPass.cs
└── Assets/                         # 已有 Models/Fonts，以及对应 Slang
```

每个 Pass 只管理本职责所需的管线、常量和资源；借用资源不重复释放。PBR、天空、材质裁剪可写成共享 Slang 函数，不需要新增 Pass、基类、RenderGraph 或配置注册系统。Renderer 的固定调用顺序应能直接读懂。

## 4. 超分接入是主线

| 模式 | 顺序 | 定位 |
| --- | --- | --- |
| None | Scene HDR → ToneMapping → 必要时双线性放大 → Color | scale=1 为原生对照，低 scale 为双线性基线 |
| Spatial | Scene HDR → ToneMapping → SGSR 1 → Color | 与相同 scale 的 None 比较空间重建效果 |
| Temporal | Scene HDR + depth/motion → SGSR 2 Quality → ToneMapping → Color | 同时提供时域抗锯齿与放大；scale=1 也可对照 |

SGSR 1 的现有 shader 有 0..1 clamp，因此放在 LDR 阶段；SGSR 2 接收 HDR，在色调映射之前执行。None/Spatial 不加 jitter 或 FXAA，Temporal 不叠加另一套 TAA。这样模式差异来自相应重建路径，而不是额外后期。

使用 `CreateSpatialUpscaler` / `CreateTemporalUpscaler`，Temporal 选择 `TemporalUpscalerMode.Quality`。以本地 `sources/Extensions/Zenith.NET.Extensions.Upscaling/` 下的 Args、Desc 和 Slang 为准；Desc 尺寸改变时重建实例。

时域输入必须正确：

| 输入 | 约定 |
| --- | --- |
| Input / OpaqueInput | 当前无 BLEND，两者使用同一张线性 HDR 纹理，无需额外复制 |
| Depth | 普通 device depth：near=0、far=1；不传线性米深度或 reversed-Z |
| MotionVectors | 当前到上一渲染帧的运动，按现有 SGSR shader 编码，不能直接填原始 UV velocity |
| JitterOffsetX/Y | 当前帧输入像素单位偏移；与实际投影一致 |
| PreExposure | 本轮固定为 1，曝光在 ToneMapping 中处理 |
| SameCamera / Reset | 根据未 jitter 的相机判断是否静止；首帧与历史不兼容时 Reset |

ScenePass 直接写编码后的 motion，不额外增加打包 Pass。定义 `motion = currentUnjitteredNdc.xy - previousUnjitteredNdc.xy`，按当前解码函数的逆编码：`encoded = motion * 0.2495 + 32767.0 / 65535.0`。有效静止表面编码约为 0.5，不是 0；背景天空输出只含相机旋转的 motion。

重投影检查应满足 `previousUv = currentUv + (-0.5 * motion.x, 0.5 * motion.y)`。使用 Halton(2,3) 的 8 帧序列减 0.5，投影 NDC 偏移为 `(2*jx/renderWidth, -2*jy/renderHeight)`。每个实际渲染帧只推进一次序列和上一帧矩阵，不能在未渲染的 Update 中推进。

优先显式提供几何和天空的 motion；零编码会触发扩展的矩阵回退。若需要回退，令 C/P 为当前/上一帧未 jitter 的 ViewProjection，J 为当前 clip jitter，`ClipToPrevClip = inverse(C * J) * (P * J)`，使静止相机的回退运动也为零。水平 FOV 按当前相机 aspect 计算并核对扩展所需单位；其余参数不要留成无意义的零值。

首次使用、尺寸/模式改变、相机 cut、FOV 变化、时间大幅调整时重置历史；普通连续移动不重置。切回 None/Spatial 时恢复无 jitter 投影。检查奇数尺寸下的线程/采样边界及首次 history 初始化；若确认是共享扩展问题，记录具体复现单独处理，不把重写 SGSR 或擅改框架列入本实验任务。

## 5. 必须保持的数据与生命周期约定

- 输出尺寸 D 使用 framebuffer 像素，内部尺寸 R 为 `max(1, floor(D * RenderScale))`，宽高分别计算；Color 始终为 D。相机比例使用输出比例，UI 使用逻辑尺寸。
- C# 与 Slang 沿用 row-major、行向量：`ViewProjection = View * Projection`，shader 使用 `mul(position, matrix)`；普通 Z、clear=1、LessEqual。GPU 常量显式对齐，不额外转置矩阵。
- ScenePass 输出 HDR `RGBA16F`、DeviceDepth `R32Float`、EncodedMotion `RG16F`，另有硬件深度附件。天空同时写背景深度 1 和旋转 motion，所有输入首帧有效。无需输出 normal/roughness 中间纹理。
- `Color` 为 `R8G8B8A8UNorm`、已编码的 LDR。保持现有 `ImGuiColorSpace.Legacy` 和 UNorm 交换链，避免重复 gamma。Temporal 的中间输出为 D 尺寸 RGBA16F。
- Renderer 构造时 App 尚未通过对象初始化器赋值 Settings。构造先创建 D 尺寸 Color；首个 `Update(camera)` 才依据有效 Settings 创建 R 资源/upscaler。不加空 Color 判断，不移动 App 的 Binding/Update。
- Scale 变化只重建内部资源和 upscaler；Color 仅随输出尺寸改变。保持原有 Resize 入口，确保旧资源仍被 UI/GPU 引用时不会提前销毁。最小化期间不推进渲染历史。
- 保留 App 的单队列 `Submit().Wait()`；Pass 不自行提交/等待。显式处理实际 texture layout 和写后读依赖，不采样未初始化资源，不在同一子资源上边读边写。纹理随 Resize/Dispose 正确释放。

## 6. 如何展示超分

先保留现有三个控件：RenderScale、UpscalingMode、TimeOfDay。曝光、环境光强和阴影参数先在实现中调好，不铺设大量高级面板。App 的现有 Settings 回调中增加实际输入/输出尺寸、模式、整帧 GPU 耗时和超分 GPU 耗时即可。

展示固定一组相机和光照，对比：

1. `None + scale=1`：原生分辨率对照。
2. `None + scale=0.5/0.75`：相同低分辨率输入的双线性基线。
3. `Spatial + 相同 scale`：观察纹理与边缘重建。
4. `Temporal + 相同 scale`：观察细节稳定性、运动和新显露区域；另测 scale=1 以区分抗锯齿与放大的收益。

选择栏杆/细柱、叶片镂空、布料纹理三个观察区域。对比时保持曝光、材质采样和阴影质量一致；不针对某个模式额外锐化或更换光照。原生图只是对照，不当作无锯齿的像素级真值。

静态截图等待时域至少稳定 32 帧；再检查缓慢平移、快速转头和停止后的收敛。截图可按需读取 Color 或使用外部截图工具，不必开发分屏双渲染、录制回放或对比编辑器。

计时预热至少 60 帧，记录至少 120 帧平均 GPU 时间。使用同一队列的 timestamp 和 `GetElapsedNanoseconds`，在上一帧完成后读取；不能用受 VSync 限制的 FPS 代替 GPU 收益。记录设备/API、D/R 和模式；渲染画质设置在比较中固定。

## 7. 开发顺序与完成标准

| 阶段 | 工作 | 通过条件 |
| --- | --- | --- |
| 1. 场景基线 | 加载现有资产，完成 Scene/ToneMapping 和 Color 生命周期 | 材质、比例、MASK 正确，原有 UI 可用；只需 None 原生输出 |
| 2. 画面调整 | 加入简单阴影、天空和近似环境光 | 阳光层次清楚，阴影不死黑，金属/石材/布料可区分；完成后冻结对比参数 |
| 3. 空间超分 | None 双线性与 SGSR 1，接入 RenderScale | 0.5/0.75/1 均铺满输出，模式切换真实生效、颜色一致 |
| 4. 时域超分 | motion、jitter、SGSR 2、history reset | 静态稳定，移动/新显露区域无持续拖影，切模式/尺寸不闪黑 |
| 5. 展示验证 | 完成对比截图、GPU 计时与生命周期检查 | 能说明画质变化和实际耗时，提交可运行实验及简短 README |

每阶段先构建，再执行对应运行检查，不增加无关效果。最终至少覆盖三模式、scale=0.5/0.75/1、奇数尺寸窗口、连续 Resize、最小化恢复及时间切换；无越界、NaN、资源泄漏或失效句柄。对矩阵、motion 编码和历史重置做针对性验证即可，不建设庞大的测试或基准框架。

从仓库根目录构建和运行：

```sh
dotnet build sources/Experiments/Sponza/Sponza.csproj -c Release
dotnet run --project sources/Experiments/Sponza/Sponza.csproj -c Release --no-build
```

最终 README 只需说明运行方式、控制项、渲染/超分顺序、代表性截图、实测耗时和未验证平台。D3D12/Metal/Vulkan 按可用设备分别验证；未运行的平台明确注明。代码能编译、模型已入库或某个模式只有界面选项，都不能当作整个实验完成。
