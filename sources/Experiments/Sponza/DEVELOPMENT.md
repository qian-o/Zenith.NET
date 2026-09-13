# Sponza 新版统一实时光追渲染管线设计

## 1. 文档目的

本文档是 Sponza 的唯一目标架构和实现边界。实现阶段必须按照本文档修改，不在代码中重新选择光照模型、降噪模型、历史结构或 ReSTIR 方案。

目标是建立一条完整、稳定、可解释的实时渲染管线：

- 使用 glTF 的真实几何、材质、纹理和 sampler 关系；
- 使用 inline RayQuery 完成主可见性、太阳阴影、一次间接漫反射和镜面反射；
- 将材质、直接光、间接漫反射、镜面反射和 emission 分开处理；
- 只对随机间接 radiance 做分量级时域累积和空间重建；
- 保留地面、帆布、叶片、链条和雕刻的纹理与几何细节；
- 在 Native、Spatial 和 Temporal 输出模式下使用一致的线性 HDR 合成和显示转换；
- 让固定相机的直接光完全稳定，让间接光通过正确的历史统计逐步收敛。

本文档不把降噪当作“把最终颜色变平滑”的后处理，也不把超分当作修复错误光照、材质或 motion 的工具。

## 2. 固定决策

以下决策是本项目的目标实现，不是待选方案：

| 主题 | 固定决策 |
| --- | --- |
| 场景光照 | 统一使用 inline RayQuery，不提供光栅化光照回退 |
| 主可见性 | 独立 Primary/GBuffer 阶段，输出世界位置、深度、法线、材质和 motion |
| 直接光 | 每像素 4 个固定太阳盘样本，样本只由像素坐标和样本序号决定，不由帧序号决定；输出 diffuse irradiance 与 specular 分量 |
| 直接光降噪 | 不降噪；直接光必须在采样和 shadow candidate 阶段稳定 |
| 间接漫反射 | 每像素每帧 1 条 cosine-weighted secondary ray，最多一次间接反弹；输出 albedo-demodulated diffuse irradiance |
| 镜面反射 | 每像素每帧 1 条 roughness-dependent GGX VNDF ray，最多一次间接反弹 |
| AO | 删除独立 AO ray 和全局 AO 乘数；接触遮蔽只能来自实际 GI 可见性 |
| 随机序列 | per-pixel scrambled 2D R2 sequence with fixed irrational increments; Direct excludes frame index, secondary paths include frame index |
| 材质采样 | primary hit 使用有效屏幕 footprint；secondary hit 使用 ray cone 的显式 SampleLevel |
| 光照输出 | `DirectDiffuseIrradiance`、`DirectSpecular`、`IndirectDiffuseIrradiance`、`Specular` 独立输出；`Emission` 属于 GBuffer 稳定信号 |
| 降噪历史 | `IndirectDiffuseIrradiance` 和 `Specular` 各自拥有独立 ping-pong history、YCoCg mean/mean2 和 length |
| 空间重建 | 每个随机分量 3 轮 A-Trous，步长为 1、2、4；空间结果不得写回 temporal history |
| 颜色处理 | 禁止 `ScaleToLuminance`、最终颜色 min/max 模糊和全局锐化补偿 |
| 超分 | Composite HDR 之后进入 Native、SGSR1 或 SGSR2 输出链 |
| ReSTIR | 本版不实现 ReSTIR DI 或 ReSTIR GI；reservoir 不属于目标资源图 |
| 设备能力 | 不支持 ray tracing 时明确失败，不静默切换画质路径 |

ReSTIR 在本版被明确排除不是因为它不能工作，而是因为 Sponza 当前的直接光只有一个太阳，固定的 4-sample direct estimator 已经有确定的采样边界；引入 reservoir 会增加时空状态、visibility reuse 和偏差问题，却不能修复当前的纹理、路径估计器或降噪契约。完整管线先采用可验证的固定估计器，不再保留“以后再决定是否引入 ReSTIR”的分支。

## 3. 总体架构

### 3.1 每帧流程

```text
SceneResources + BLAS/TLAS
              ↓
PrimaryPass: primary ray + GBuffer
              ↓
LightingPass: DirectDiffuseIrradiance + DirectSpecular
              + IndirectDiffuseIrradiance + Specular
              ↓
DenoisePass: per-component temporal accumulation + 3-pass A-Trous
              ↓
CompositePass: DiffuseAlbedo * (DirectDiffuseIrradiance
              + DenoisedIndirectDiffuseIrradiance)
              + DirectSpecular + DenoisedSpecular + Emission
              ↓
UpscalePass: Native / SGSR1 / SGSR2
              ↓
OutputPass: exposure + tone mapping + sRGB
              ↓
Color + UI
```

所有阶段使用当前内部尺寸 `R`，只有最终输出和 UI 使用显示尺寸 `D`。当 `RenderScale=1` 时 `R=D`，但阶段边界不改变。

### 3.2 阶段职责

| 阶段 | 输入 | 输出 | 禁止事项 |
| --- | --- | --- | --- |
| `PrimaryPass` | 场景、TLAS、当前 jittered camera | GBuffer、motion、材质属性 | 不生成 GI、反射或最终颜色 |
| `LightingPass` | GBuffer、场景、TLAS、当前 frame index | 四个光照分量、样本 metadata | 不读取上一帧 radiance history |
| `DenoisePass` | 两个 noisy 随机分量、GBuffer、上一帧分量 history | 两个 temporal result、两个空间 result、YCoCg moments、length | 不处理材质、Direct 或 Emission |
| `CompositePass` | BaseColor、DirectDiffuseIrradiance、DirectSpecular、两个空间 result、Emission | 线性 HDR | 不做空间降噪、锐化或额外 AO |
| `UpscalePass` | Composite HDR、device depth、motion、jitter | Native/SGSR1/SGSR2 输出 | 不修正错误的 RT history |
| `OutputPass` | 线性 HDR或显示域 LDR | `Color` | 不重复 tone mapping |

`PrimaryPass` 和 `LightingPass` 可以由一个 C# 管线对象编排，但 shader、资源和数据语义必须保持上述两个阶段的边界。不得为了少一个 dispatch 把 GBuffer、随机光照和历史写入重新混合成一个 `Color`。

## 4. GBuffer 与光照资源契约

### 4.1 GBuffer

每个内部像素必须产生以下资源：

| 资源 | 格式 | 内容 |
| --- | --- | --- |
| `WorldPosition` | `R32G32B32A32Float` | 世界位置；背景 alpha 为 0 |
| `DeviceDepth` | `R32Float` | 主 ray 的普通 device Z；背景为 1，供 SGSR2 使用 |
| `LinearDepth` | `R32Float` | 线性 view-space depth；供 history rejection 使用 |
| `ShadingNormal` | `R16G16B16A16Float` | 归一化 shading normal，编码为 `normal * 0.5 + 0.5` |
| `GeometricNormal` | `R16G16B16A16Float` | 归一化 geometric normal，编码为 `normal * 0.5 + 0.5` |
| `BaseColorMetallic` | `R16G16B16A16Float` | 线性 base color RGB，metallic A |
| `Roughness` | `R16Float` | 线性 roughness |
| `MaterialClass` | `R32UInt` | 精确材质分类/材质索引，供 history rejection 和 A-Trous 使用 |
| `Validity` | `R32UInt` | 有效表面、背景和低 albedo 标记；使用 Zenith.NET 已被 SGSR 扩展验证过的 uint storage-image 路径 |
| `Motion` | `R16G16UNorm` | 未抖动 NDC motion，遵循 SGSR2 编码 |
| `Emission` | `R16G16B16A16Float` | 材质 emission；稳定信号，不进入 denoiser |

背景必须写入明确的 invalid/valid 标记。不能使用“depth 接近 1”同时承担背景识别、历史有效性和材质有效性的全部语义。

### 4.2 光照分量

`LightingPass` 必须输出：

| 资源 | 格式 | 内容 | 是否进入 denoiser |
| --- | --- | --- | --- |
| `DirectDiffuseIrradiance` | `R16G16B16A16Float` | 太阳 diffuse irradiance 和 shadow visibility，不含 primary diffuse albedo | 否 |
| `DirectSpecular` | `R16G16B16A16Float` | 太阳 specular BRDF 和 shadow visibility | 否 |
| `IndirectDiffuseIrradiance` | `R16G16B16A16Float` | 一次漫反射 secondary path 的 diffuse transport，不含 primary diffuse albedo 和 Lambert `/pi` | 是 |
| `Specular` | `R16G16B16A16Float` | 一次 GGX secondary path 的 radiance | 是 |
| `Emission` | `R16G16B16A16Float` | 主命中材质 emission，属于 GBuffer 稳定信号 | 否 |

最终合成严格为：

```text
CompositeHDR = DiffuseAlbedo / PI * (DirectDiffuseIrradiance
             + DenoisedIndirectDiffuseIrradiance)
             + DirectSpecular
             + DenoisedSpecular
             + Emission
```

`DiffuseAlbedo = BaseColor * (1 - Metallic)`。`DirectDiffuseIrradiance` 和 `IndirectDiffuseIrradiance` 都定义为 $E = \int L_i(n \cdot \omega_i)V_i\,d\omega_i$ 的估计值，不包含 primary diffuse albedo，也不包含 Lambert `/pi`。Lighting 阶段直接输出 transport 量，不预先乘 albedo，也不执行除以 albedo 的解调；Composite 阶段只执行一次 `DiffuseAlbedo / PI` 复乘。albedo 接近 0 时只标记低置信度，不能执行除法，也不能产生 Inf/NaN。这样 A-Trous 不会把相邻红、蓝、绿色帆布或地面纹理当成同一种带材质颜色的 radiance 来平均。`BaseColor` 参与 BRDF、secondary throughput 和最终复乘，但不会作为带 primary albedo 的 noisy radiance 被滤波。

## 5. 主 ray、材质和纹理采样

### 5.1 主 ray 命中

主 ray 使用当前 jittered projection 生成，命中后还原：

- instance id、primitive index 和 barycentric；
- local/world position；
- geometric normal、interpolated normal、tangent 和 bitangent；
- UV、材质索引、alpha mode 和 material class；
- device depth、linear view-space depth 和未抖动 motion。

MASK 材质必须在 RayQuery candidate 阶段读取 barycentric UV 和 base color alpha，再决定是否 commit。所有调用点都传入对应 RayQuery 的真实 `CandidateTriangleFrontFace()`。背面、双面、镜像和 alpha mask 的规则在 primary、shadow、GI 和反射路径中一致。

### 5.2 PBR 规则

使用 glTF metallic-roughness 模型：

- base color 只进行一次 sRGB 解码，alpha 保持线性；
- metallic-roughness 使用线性纹理，B 通道为 metallic，G 通道为 roughness；
- `roughness = clamp(factor * texture, 0.045, 1.0)`；
- `F0 = lerp(0.04, baseColor, metallic)`；
- 使用 GGX、Smith visibility、Schlick Fresnel 和 Lambert diffuse；
- normal map 使用正确的 TBN 和 `NormalScale`；
- emission 只在材质命中时加入 `Emission`，不进入随机 radiance history。

### 5.3 纹理 LOD

纹理解析严格遵循：

```text
primitive -> material -> texture slot -> textures[index].source -> images[source]
```

纹理缓存键必须包含 image 引用和颜色语义。base color、normal、metallic-roughness 和 emissive 即使引用同一 image，也必须保留各自格式和颜色空间。

采样规则固定如下：

1. 主相机命中根据三角形的屏幕投影计算 `du/dx`、`dv/dx`、`du/dy`、`dv/dy`，按每个纹理的真实宽高计算 footprint；
2. 主相机命中可以使用 `SampleGrad`，但梯度必须来自当前主 ray 的有效屏幕 footprint，不能将三角形平均值用于所有后续路径；
3. GI、反射和 shadow candidate 没有光栅化 `ddx/ddy`，一律使用 ray cone 的显式 LOD 和 `SampleLevel`；
4. ray cone 必须随 primary ray、secondary scatter direction、roughness 和 hit distance 传播；
5. alpha mask 使用保守且稳定的显式 LOD，不能因高频 alpha 采样在相邻帧改变 candidate commit 结果；
6. 不使用负 LOD bias，不使用最终锐化补偿错误 mip，不把 `max(width,height)` 当作两个 UV 方向的导数。

资源有 mip、sampler 允许 mip、shader 实际选择正确 footprint 是三个独立检查项，必须分别验证。

## 6. LightingPass 估计器

### 6.1 直接光

太阳方向只由 `TimeOfDay` 决定，不读取 `FrameIndex`。每个像素固定生成 4 个太阳盘方向，方向由 pixel hash 和固定 sample index 决定；同一像素在同一时间的太阳样本跨帧完全一致。

每个样本执行：

1. 从 geometric normal 沿光源方向偏移 `rayEpsilon=0.01`；
2. 发射 shadow ray；
3. 在 candidate 阶段处理 MASK、双面和布料透射；
4. 计算太阳 BRDF、cosine、radiance、transmission 和样本平均；
5. 将不含 albedo 和 Lambert `/pi` 的 diffuse irradiance 写入 `DirectDiffuseIrradiance`，将太阳高光项写入 `DirectSpecular`。

直接光不使用随机 frame sequence，不写入 `IndirectDiffuseIrradiance` 或 `Specular`，不经过 A-Trous。若直接光发生帧间变化，修复 shadow candidate、alpha mask、ray epsilon 或太阳样本，而不是修改 history weight。

### 6.2 一次间接漫反射

每像素每帧生成 1 条 cosine-weighted hemisphere ray：

```text
camera ray -> first hit -> diffuse secondary ray -> second hit/environment
```

随机方向使用 per-pixel scrambled 2D R2。令 `s = frameIndex + 0.5`，令 `q0/q1` 为由 `PCGHash(pixel, dimension)` 生成的 `[0,1)` scramble，固定增量为 `a0=0.754877666`、`a1=0.569840296`：

```text
u = frac(0.5 + s * a0 + q0)
v = frac(0.5 + s * a1 + q1)
```

Direct 使用 `s = sampleIndex + 0.5` 且不包含 frame index；secondary path 使用上式的 frame index。secondary hit 处理规则固定为：

- 命中 emission：返回命中材质的 emission；
- 命中普通表面：评估该点的直接太阳可见性和环境项，不继续发射第三条 ray；
- 未命中：返回统一环境 radiance；
- 乘以完整 BSDF throughput 和 PDF；
- 按 `Li * cosTheta / pdf` 形成不含 primary albedo 和 Lambert `/pi` 的 irradiance 估计，直接写入 `IndirectDiffuseIrradiance`。

不再执行独立 AO ray，也不把 AO 乘到 ambient、GI 或 reflection 上。接触遮蔽来自 secondary path 的实际可见性，避免同一遮蔽被重复计算。

### 6.3 一次镜面反射

每像素每帧生成 1 条 roughness-dependent GGX VNDF ray：

```text
camera ray -> first hit -> GGX secondary ray -> second hit/environment
```

镜面路径必须：

- 使用 shading normal 和 roughness 选择方向；
- 低粗糙度表面使用更严格的有效性和能量 clamp；
- 命中 emission、普通表面或环境时写入 `Specular`；
- 不把 secondary hit 的 direct specular 作为主表面的 `DirectSpecular`；
- 不与 `IndirectDiffuseIrradiance` 共用 sample metadata、moments 或 history。

### 6.4 环境与体积

背景、secondary miss 和反射 miss 使用同一世界空间环境函数、颜色空间和曝光方向。环境方向不受相机平移影响。

体积散射只在背景路径执行固定的 2 段积分；表面路径不叠加体积阴影。所有环境和体积结果在进入对应光照分量前保持线性 HDR。

## 7. 分量级 DenoisePass

### 7.1 History 资源

`DenoisePass` 为 `IndirectDiffuseIrradiance` 和 `Specular` 分别维护两套 ping-pong 资源：

| 资源 | 作用 |
| --- | --- |
| `IndirectHistoryA/B` | 漫反射 irradiance temporal result |
| `SpecularHistoryA/B` | 镜面 temporal result |
| `IndirectMomentsA/B` | 漫反射 YCoCg mean/mean2，`R32G32B32A32Float` 保存 Y mean、Y mean2、Co mean、Co mean2 |
| `IndirectMomentsCgA/B` | `R32G32Float` 保存漫反射 Cg mean、Cg mean2 |
| `SpecularMomentsA/B` | 镜面 YCoCg mean/mean2，`R32G32B32A32Float` 保存 Y mean、Y mean2、Co mean、Co mean2 |
| `SpecularMomentsCgA/B` | `R32G32Float` 保存镜面 Cg mean、Cg mean2 |
| `IndirectLengthA/B` | 漫反射有效历史长度，范围 0..32 |
| `SpecularLengthA/B` | 镜面有效历史长度，范围 0..8 |
| `PreviousLinearDepth` | 上一帧线性深度 |
| `PreviousShadingNormal` | 上一帧 shading normal |
| `PreviousGeometricNormal` | 上一帧 geometric normal |
| `PreviousRoughness` | 上一帧 roughness |
| `PreviousMaterialClass` | 上一帧精确 material class |
| `PreviousMotionValidity` | 上一帧有效命中和 motion 状态 |
| `IndirectAtrousA/B` | 漫反射空间滤波 ping-pong |
| `SpecularAtrousA/B` | 镜面空间滤波 ping-pong |

空间滤波临时资源只用于当前帧，不作为下一帧 temporal history。任何 history 失效都通过 reset/length 分支处理；Resize 时重建所有相关资源。

### 7.2 Reprojection 与 rejection

当前像素按照未抖动 NDC motion 重投影到上一帧。历史有效必须同时满足：

- previous UV 在 `[0,1]` 内；
- 当前和上一帧均为有效表面，或均为背景；
- 线性 view-space depth 的绝对差异小于 `max(0.02, currentDepth * 0.01)`；
- 表面法线 dot 大于 `0.90`；
- geometric normal dot 大于 `0.95`；
- roughness 差异小于 `0.10`；
- material class 完全相同；
- specular 额外要求反射方向 dot 大于 `0.95`；
- 当前帧没有 reset，上一帧 history length 大于 0。

属性 history 使用点采样，不使用线性采样跨越几何边界。radiance history 也使用点采样，之后由当前邻域统计完成有限的历史裁剪。

### 7.3 Temporal accumulation

对每个分量分别执行：

1. 从当前 noisy 分量计算 current YCoCg；
2. 从当前 3x3 同表面邻域计算 YCoCg mean、mean2 和 variance；
3. 读取重投影的上一帧 radiance、moments 和 history length；
4. 在 YCoCg 空间以每通道 `mean ± 2.5 * sigma` 对历史颜色做逐通道 clamp；
5. 使用 `alpha = max(1 / (historyLength + 1), minimumCurrentWeight)` 累积当前样本和历史；
6. 更新 moments 和 history length；
7. 输出 temporal result，供当前帧 A-Trous 使用。

固定参数为：

- `IndirectDiffuseIrradiance`: `minimumCurrentWeight=0.03`，length 上限 32；
- `Specular`: `minimumCurrentWeight=0.12`，length 上限 8；
- rejection 后 `alpha=1`、length 写为 1；
- reset 后完全忽略旧 history；
- 不允许用 RGB 统一缩放到目标亮度；
- 不允许使用当前邻域 RGB min/max 直接裁剪 history。

YCoCg clamp 的目的只是去除重投影异常值，不能改变材质色相、纹理对比或法线贴图表达。Moments 必须以与 clamp 相同的解调空间统计；不能只保存 luminance 却声称完成 YCoCg clamp。

### 7.4 A-Trous spatial reconstruction

`IndirectDiffuseIrradiance` 和 `Specular` 分开执行 3 次 A-Trous：

```text
temporal result -> step 1 -> step 2 -> step 4 -> spatial result
```

每一轮使用固定 5x5 A-Trous kernel。基础一维核为 `[1, 4, 6, 4, 1] / 16`，二维 kernel 为其外积，25 个 tap 的系数固定并归一化；step 为 1、2、4。每一轮的权重由以下因素相乘：

- 固定 5x5 A-Trous kernel，基础一维系数为 `[1, 4, 6, 4, 1] / 16`，二维系数取外积；
- 线性深度相容性；
- shading normal 相容性；
- geometric normal 相容性；
- roughness 相容性；
- material class 相等性；
- 当前分量 variance confidence。

specular 还必须加入反射方向相容性，并使用比漫反射更窄的深度和法线权重。空间滤波不得跨越墙体边缘、链条、叶片、帆布边缘或不同 material class。

A-Trous 只读取 temporal result 和当前 GBuffer；它不读取下一帧 history，也不把 spatial result 拷回 temporal history。Direct、BaseColor 和 Emission 完全绕过 A-Trous。

## 8. Composite、Upscale 与 Output

### 8.1 CompositePass

`CompositePass` 只执行线性 HDR 相加和必要的非负 clamp：

```text
CompositeHDR = max(DiffuseAlbedo * (DirectDiffuseIrradiance
                 + DenoisedIndirectDiffuseIrradiance)
                 + DirectSpecular
                 + DenoisedSpecular
                 + Emission, 0)
```

禁止在此阶段加入全局 AO、经验亮度、颜色锐化、反射增益或第二次 tone mapping。

### 8.2 三种输出模式

| 模式 | 固定流程 |
| --- | --- |
| `Native` | `CompositeHDR(D) -> OutputPass -> Color(D)` |
| `Spatial` | `CompositeHDR(R) -> ToneMap(R) -> SGSR1 -> Color(D)` |
| `Temporal` | `CompositeHDR(R,jitter) + DeviceDepth + Motion -> SGSR2 Quality -> ToneMap(D) -> Color(D)` |

SGSR2 只能接收合成后的去噪线性 HDR。SGSR2 history 与 RT denoiser history 完全分离，reset 事件可以相同，但资源和生命周期不能共享。

### 8.3 Motion、jitter 与 reset

motion 是当前和上一帧同一世界表面的未抖动 NDC 位移：

```text
m = currentNdc - previousNdc
previousUv.x = currentUv.x - 0.5 * m.x
previousUv.y = currentUv.y + 0.5 * m.y
encoded = m * 0.2495 + 32767 / 65535
```

Temporal 模式使用 8 相 Halton(2,3) jitter；Native 和 Spatial 使用零 jitter。jitter 不写入 object motion，但会影响当前主 ray 的覆盖位置。

以下事件同时使 RT component history 和 SGSR2 history reset：

- 首帧；
- Resize、内部尺寸变化或 RenderScale 变化；
- 模式变化；
- TimeOfDay 或环境参数变化；
- 相机投影变化或大幅跳变；
- TLAS、BLAS、材质、纹理或 sampler 变化；
- 采样序列、ray count、denoise 参数或 shader 版本变化。

连续相机运动不自动 reset，但每个像素仍必须经过 depth、normal、roughness、material 和 specular direction rejection。

## 9. C# 项目架构与修改边界

### 9.1 允许修改的文件范围

实现只允许修改或新增 Sponza 项目中的以下区域：

| 区域 | 责任 |
| --- | --- |
| `Renderer.cs` | 编排阶段、frame state、jitter、reset、历史交换和输出尺寸 |
| `Passes/PrimaryPass.cs` | 主 ray、GBuffer、motion 和材质命中 |
| `Passes/LightingPass.cs` | DirectDiffuseIrradiance、DirectSpecular、IndirectDiffuseIrradiance、Specular |
| `Passes/DenoisePass.cs` | 两个分量的 history、temporal、moments 和 A-Trous |
| `Passes/CompositePass.cs` | 线性 HDR 分量合成 |
| `Passes/UpscalePass.cs` | Native、SGSR1、SGSR2 编排和独立超分 history |
| `Passes/OutputPass.cs` | exposure、tone mapping、sRGB 和最终 Color |
| `Models/` | 每个 pass 的输入、输出、FrameData 和显式常量布局 |
| `Helpers/SceneResources.cs` | glTF 材质、纹理、sampler、primitive metadata 和 BLAS/TLAS |
| `Helpers/GraphicsHelper.cs` | Sponza 私有资源创建、shader 加载和 buffer 上传 |
| `Assets/Shaders/Primary.slang` | 主 ray、GBuffer、primary material sampling |
| `Assets/Shaders/Lighting.slang` | shadow、GI、reflection、environment 和 secondary material sampling |
| `Assets/Shaders/Denoise.slang` | component temporal、moments 和 A-Trous |
| `Assets/Shaders/Composite.slang` | HDR component composition |
| `Assets/Shaders/HdrOutput.slang` | 最终显示转换 |

`RayTracingPass.cs` 可以在迁移期间作为编排兼容层，但最终不得继续拥有“单一 Color 包含所有光照”的公开输出契约。

### 9.2 明确禁止修改的区域

本次重构不得修改：

- `sources/Zenith.NET/` 共享 RHI；
- DirectX12、Vulkan、Metal 后端；
- `Zenith.NET.Extensions.Upscaling` 共享 SGSR 实现；
- `.slnx`、工程文件、NuGet 包版本和公共 API；
- glTF 模型文件和外部资源语义；
- `App.cs` 的窗口/输入协议，除非增加已有 Settings 所需的最小绑定；
- `RenderValidation`、`--validate`、自动截图、性能 JSON 或新的调试输出设施；
- ReSTIR reservoir、light list、path reservoir 和额外厂商 SDK。

Pass 不读取 App、Camera 或全局 Settings。Renderer 将解析后的 frame 数据传给 pass。稳定帧不得创建 pipeline、BLAS/TLAS、纹理、history 或场景数组。

### 9.3 C# 与 Slang 布局

所有 constant buffer 使用显式 C# layout，并与 Slang 字段顺序、对齐和大小逐项对应。每个 descriptor handle 单独占用 8 字节；矩阵采用仓库既有行向量约定。新增 GBuffer、component output、history 和 A-Trous 资源必须在同一处定义：

```text
resource owner -> texture format/usage -> layout state -> shader descriptor -> history lifetime
```

不得通过隐式字段追加、未对齐 struct 或猜测的 descriptor 顺序传递新资源。

## 10. 每帧同步与资源生命周期

每帧 command buffer 的固定顺序为：

```text
GBuffer textures: Undefined/Sampled -> Storage
PrimaryPass dispatch
barrier
GBuffer textures: Storage -> Sampled

Lighting outputs: Undefined/Sampled -> Storage
LightingPass dispatch
barrier
Lighting outputs: Storage -> Sampled

Current noisy components + previous histories -> Denoise temporal
barrier
Temporal outputs -> A-Trous ping-pong
barrier after each spatial dispatch

Component outputs -> Composite storage
barrier
Composite -> Upscale or Output sampled input
```

同一 dispatch 中，资源不能同时以 sampled 和 storage 角色绑定。history 读取的是上一帧资源，history 写入的是当前帧 ping-pong 目标；禁止 in-place history 更新。

每帧结束前保存：

- component temporal result；
- moments；
- history length；
- 当前线性 depth、normal、roughness、material class 和 validity。

保存 spatial result 不属于 history。Resize 必须等待最后一次 GPU 使用完成，再重建内部尺寸资源，并将 history length 置为 0。

## 11. 实现顺序

这是固定实施顺序，不是质量方案选择：

1. 重建 `FrameData`、GBuffer output、component output 和显式 constant layout；
2. 实现 PrimaryPass，先验证 geometry、depth、normal、material class、motion 和 primary texture LOD；
3. 实现固定 4-sample Direct 和稳定 MASK/shadow candidate；
4. 实现 1-spp IndirectDiffuseIrradiance 和 1-spp Specular，完成 ray cone secondary texture LOD；
5. 实现 CompositePass，关闭所有 denoiser 时仍保持材质纹理清晰；
6. 实现两个独立 temporal history、reprojection rejection、YCoCg variance clamp 和 moments；
7. 实现两个独立 3-pass A-Trous，并确认 spatial result 不反馈 history；
8. 接入 Native、Spatial、Temporal 输出链和 SGSR2 独立 history；
9. 完成固定验收矩阵、同步检查、性能记录和设计审查。

不得跳过分量输出直接恢复单一最终 Color，也不得在 temporal、spatial 或 upscaling 阶段用锐化掩盖前一阶段错误。

## 12. 验收标准

### 12.1 稳定性

- 固定相机、固定 TimeOfDay、Native、RenderScale=1 时，同一 primary surface 的 DirectDiffuseIrradiance 和 DirectSpecular 重投影结果一致；
- Temporal jitter 下允许相邻内部像素的 primary sample 改变，但无 jitter 的 Native/Spatial 直接光必须逐像素一致；
- 固定相机下 IndirectDiffuseIrradiance 和 Specular 的方差随 history length 增加而下降；
- 太阳、阴影、alpha mask 和布料透射不因 frame index 改变而闪烁；
- reset 后不会残留上一时间或上一相机的 radiance。

### 12.2 细节保持

- 关闭 denoiser 时地面、帆布、叶片和雕刻保持清晰；
- 开启 denoiser 后只减少随机光照噪声，不改变 BaseColor 纹理对比；
- A-Trous 不跨越墙体、帆布、链条、叶片或 material class 边界；
- 低粗糙度反射不会被漫反射半径抹平；
- 不出现由 `ScaleToLuminance`、RGB min/max 或重复 tone mapping 引起的色相和亮度失真。

### 12.3 运动

- 连续旋转时历史按照 motion、depth、normal 和 material 正确重投影；
- 连续平移时新显露区域从当前样本重新收敛；
- 反射方向变化会拒绝旧 specular history；
- SGSR2 不会把 RT history 错误当成自己的历史，也不放大未去噪分量。

### 12.4 工程与性能

- DirectX12/RTX 设备上通过 runtime shader compilation 和 validation layer；
- 无布局、同步、越界、失效 descriptor、RayQuery candidate 或资源读写冲突；
- 稳态帧不创建资源；
- 分别记录 Primary、Lighting、Denoise temporal、A-Trous、Composite、SGSR 和 Output 成本；
- 覆盖 `RenderScale=1/0.75/0.5`、Native/Spatial/Temporal、6/9/12/18 时刻、固定机位、旋转、平移和 reset；
- 不在没有实测数据时宣称 FPS 或画质达标。

## 13. ReSTIR 对照与排除理由

本版完整管线不包含 ReSTIR，但必须理解它与本设计的边界：

| 项目 | 本版固定设计 | ReSTIR DI | ReSTIR GI |
| --- | --- | --- | --- |
| 复用对象 | component radiance history | 直接光候选样本 | 间接路径候选样本 |
| 状态 | radiance、moments、length、GBuffer history | selected light、weight sum、M、age、light PDF | path、throughput、PDF、target、weight sum、M、age |
| 主要解决 | 稳定重建已生成的随机 radiance | 多光源/小面积光的 direct sampling variance | 间接路径候选的时空复用和路径采样 variance |
| 是否替代 denoiser | 否 | 否 | 否 |
| 本版是否使用 | 是 | 否 | 否 |

当前 Sponza 只有一个太阳时，4 个固定太阳盘样本和一个稳定 shadow candidate 比 DI reservoir 更容易验证。GI 只有一跳且主要目标是先解决 history、纹理 LOD 和分量污染，因此 GI reservoir 会扩大实现边界，却不能替代 component denoiser。任何未来改变都必须先修改本文档并重新审查；实现阶段不得自行添加 ReSTIR。

## 14. Zenith.NET 技术审查

### 14.1 公共能力覆盖

本设计逐项对照 Zenith.NET 公共 API，结论如下：

| 设计能力 | 公共 API/能力 | 审查结论 |
| --- | --- | --- |
| 2D GBuffer、component output、history、A-Trous ping-pong | `GraphicsContext.CreateTexture(TextureDesc)`、`Texture.SampledHandle`、`Texture.StorageHandle` | 可直接实现 |
| 浮点和 uint 资源 | `PixelFormat.R16G16B16A16Float`、`R32Float`、`R32UInt`、`R32G32Float`、`R32G32B32A32Float` | 可直接表达；格式能力仍须按后端运行时验证 |
| compute 阶段 | `CreateShader`、`CreateComputePipeline`、`CommandBuffer.SetPipeline`、`SetConstantBuffer`、`Dispatch` | 可直接实现 |
| RayQuery、BLAS、TLAS | Zenith.NET ray-tracing descriptors、Slang `RayQuery` 和 `RaytracingAccelerationStructure` | 可直接实现 |
| sampled/storage 状态转换 | `CommandBuffer.Transition`、`TextureLayout.Sampled`、`TextureLayout.Storage` | 可直接实现 |
| dispatch 间同步 | `CommandBuffer.Barrier` | 可直接实现 |
| history copy 和资源初始化 | `CommandBuffer.CopyTexture`、`TextureLayout.CopySrc/CopyDst` | 可直接实现 |
| glTF texture mip 和 sampler | Sponza 私有资源加载、`CreateSampler`、材质 texture sampled handles | 可直接实现，不需要原生纹理句柄 |
| ray-cone LOD | shader 内部的 float 状态、`SampleLevel` | 可直接实现，不需要 RHI 扩展 |
| Native、SGSR1、SGSR2 输出 | `CreateSpatialUpscaler`、`CreateTemporalUpscaler`、公开 upscaler args | 可直接实现，不修改共享扩展 |
| SGSR2 history invalidation | 公开 `TemporalUpscalerArgs.Reset`、`SameCamera` 和 `MotionVectors` | 可直接实现；Reset 是逻辑失效信号，不访问扩展内部 history |

本设计禁止使用 `GraphicsContext.CreateTexture` 的 native overload、`GetNativeObject`、后端对象强转、P/Invoke、反射、共享 RHI 修改或厂商 SDK。shader 中的 ray cone、R2 序列、YCoCg、A-Trous 和 component composition 都属于普通 compute/shader 逻辑，不构成绕开 Zenith.NET。

### 14.2 阻挡项与处理规则

以下是实现前必须明确的真实限制：

1. **跨后端格式能力查询缺失。** Zenith.NET 暴露了 `R32UInt`、`R32G32Float` 和 `R32G32B32A32Float`，并且 DX12、Vulkan、Metal 都有映射；但当前公共 API 没有 `FormatFeature` 或 storage-image capability 查询。当前 Windows + DirectX12 目标可以按公共 API 创建并通过 validation/runtime shader 检查；不能据此宣称 Vulkan/Metal 也已通过。
2. **跨后端验证失败时不得自行降级。** 如果公共 `CreateTexture`、pipeline 创建、shader 编译、validation layer 或首次 dispatch 证明某个格式/资源组合不可用，必须停止并向用户报告具体格式、后端和公共 API 调用；不得改用原生句柄、后端强转、隐藏的格式替换或修改共享 RHI。
3. **格式命名已经按公共 API 修正。** 文档统一使用 `R32UInt`，不使用不存在的 `R32Uint`；`Validity` 不使用没有现成 Sponza/SGSR storage 先例的 `R8UInt`。这不是静默画质降级，而是选择 Zenith.NET 已有的公开 uint storage-image 契约。
4. **没有发现功能级 RHI 阻挡。** 2D storage image、`RWTexture2D<uint/float/float4>`、多次 compute dispatch、ping-pong history、texture copy、RayQuery 和 SGSR 参数都已有公共表达方式。ray cone 不需要新增 RHI API。

因此，本设计的技术审查判定是：**当前 DX12 目标不存在必须绕过 Zenith.NET 的功能阻挡；全后端交付存在格式能力验证阻挡。验证不足时必须停下来报告，不能擅自替换实现。**

## 15. 设计审查结论

### 15.1 已通过的设计检查

| 检查项 | 结论 | 依据 |
| --- | --- | --- |
| 闪烁根因隔离 | 通过 | Direct 不依赖 frame index；随机分量独立 history；rejection 有完整属性契约 |
| 油画感隔离 | 通过 | 材质、Direct、Emission 不降噪；禁止 RGB 亮度缩放；A-Trous 不回写 history |
| 地面纹理清晰度 | 通过 | primary footprint 与 secondary ray cone 分流；禁止负 bias 和错误 SampleGrad |
| 统计契约 | 通过 | 每分量独立 moments、variance、length、alpha 和 YCoCg history clamp |
| 超分边界 | 通过 | Composite HDR、RT history、SGSR2 history 三者职责和生命周期分离 |
| 修改范围 | 通过 | 只进入 Sponza 的 Renderer、Passes、Models、Helpers、Assets/Shaders |
| ReSTIR 决策 | 通过 | 以 Sponza 单太阳、一次 GI 的实际规模为依据，固定排除 reservoir |

### 15.2 实现前的阻断项

当前实现只有在完成以下重构后才符合本文档，不能把现有单一 `Color` 降噪路径称为通过：

- 必须拆分 DirectDiffuseIrradiance、DirectSpecular、IndirectDiffuseIrradiance 和 Specular，并将 Emission 保持为稳定 GBuffer 信号；
- 必须删除独立 AO 乘数和独立 AO sample path；
- 必须建立 PrimaryPass、LightingPass、CompositePass 的资源边界；
- 必须建立两个独立 temporal history 和两个独立 moments/length；
- 必须用 ray cone 取代 secondary path 的伪造屏幕梯度；
- 必须实现 3 轮 A-Trous，且空间结果不能写回 temporal history；
- 必须重新验证 C# constant buffer、descriptor、layout transition 和 runtime shader compilation。

### 14.3 审查判定

设计审查结论为：**本文档的目标架构、Zenith.NET 能力边界和修改边界通过；实现尚未执行，当前代码不视为符合本文档。** 实现必须按第 11 节顺序重构，先完成本节的公共 API/格式验证，再通过第 12 节验收。若验证发现公共能力不足，必须先报告阻挡，不得绕开 Zenith.NET。

## 16. 参考与仓库边界

参考规范：

- [glTF 2.0 metallic-roughness](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html#appendix-b-brdf-implementation)
- [SVGF: Spatiotemporal Variance-Guided Filtering](https://research.nvidia.com/publication/2017-07_Spatio-Temporal-Variance-Guided-Filtering)
- [Ray Tracing Gems II](https://www.realtimerendering.com/raytracinggems/)
- [SGSR1](https://github.com/SnapdragonGameStudios/snapdragon-gsr/tree/main/sgsr/v1)
- [SGSR2](https://github.com/SnapdragonGameStudios/snapdragon-gsr/tree/main/sgsr/v2)

Sponza 的资源、RayQuery、BLAS/TLAS、纹理 mip、shader 编译、command buffer 和 layout transition 必须遵循仓库现有公共 API。本文档不授权修改共享 RHI，不授权引入新的调试设施，也不授权把未验证的高级采样技术加入正式管线。