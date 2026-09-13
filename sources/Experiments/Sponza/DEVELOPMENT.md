# Sponza 实时光追管线规范

本文档定义 Sponza 的最终项目结构、渲染数据契约、同步规则和实现边界。它是本项目的唯一设计规范。

## 1. 固定配置

| 项目 | 规范 |
| --- | --- |
| 目标平台 | Windows + DirectX 12，目标设备 RTX 4070 Ti SUPER |
| 图形接口 | 仅使用 Zenith.NET 公共 API；使用 inline `RayQuery` |
| 内部尺寸 | 所有 Primary、Lighting、Denoise、Composite 阶段使用 `R`；最终显示尺寸为 `D` |
| 光照 | 主可见性、太阳阴影、一次间接漫反射和一次镜面间接路径 |
| 输出 | `Native`、`Spatial`、`Temporal` |
| 能力失败 | 不支持光追或资源组合验证失败时停止并报告，不切换到隐式降级路径 |

固定采样规则：

- Direct：每像素 4 个固定太阳盘样本，方向只由像素和样本序号决定，不读取 `FrameIndex`；
- Indirect diffuse：每像素每帧 1 条 cosine-weighted secondary ray；
- Specular：每像素每帧 1 条 roughness-dependent GGX VNDF secondary ray；
- secondary path 最多一次间接反弹；
- 随机路径使用 per-pixel scrambled 2D R2 sequence，Direct 与 secondary path 的序列维度分离；
- 接触遮蔽来自实际路径可见性；资源图不包含独立 AO 项和 ReSTIR reservoir。

## 2. 精确目标项目树

以下是 Sponza 的最终源文件树。`bin/`、`obj/` 和构建产物不属于项目设计；`Assets/Models/textures/` 保留 `Sponza.gltf` 引用的全部原始图像文件，不新增按 pass 划分的纹理目录。

```text
Sponza/
├── App.cs
├── DEVELOPMENT.md
├── Program.cs
├── Renderer.cs
├── Sponza.csproj
├── Handlers/
│   ├── CameraHandler.cs
│   └── ImGuiHandler.cs
├── Helpers/
│   ├── CocoaHelper.cs
│   ├── GraphicsHelper.cs
│   ├── ImGuiHelper.cs
│   └── SceneResources.cs
├── Models/
│   ├── DenoiseResources.cs
│   ├── FrameData.cs
│   ├── GBufferResources.cs
│   ├── LightingResources.cs
│   ├── OutputResources.cs
│   ├── RenderSettings.cs
│   └── UpscalingMode.cs
├── Passes/
│   ├── CompositePass.cs
│   ├── DenoisePass.cs
│   ├── LightingPass.cs
│   ├── OutputPass.cs
│   ├── PrimaryPass.cs
│   └── UpscalePass.cs
└── Assets/
    ├── Fonts/
    │   └── msyh.ttf
    ├── Models/
    │   ├── Sponza.bin
    │   ├── Sponza.gltf
    │   └── textures/
        │       ├── aged_plaster_trim_base_color.jpg
        │       ├── aged_plaster_trim_metallic_roughness.jpg
        │       ├── aged_plaster_trim_normal.jpg
        │       ├── architectural_stone_atlas_base_color.jpg
        │       ├── architectural_stone_atlas_metallic_roughness.jpg
        │       ├── architectural_stone_atlas_normal.jpg
        │       ├── carved_marble_base_color.jpg
        │       ├── carved_marble_metallic_roughness.jpg
        │       ├── carved_marble_normal.jpg
        │       ├── carved_stone_ornament_base_color.jpg
        │       ├── carved_stone_ornament_metallic_roughness.jpg
        │       ├── carved_stone_ornament_normal.jpg
        │       ├── carved_stone_trim_base_color.jpg
        │       ├── carved_stone_trim_metallic_roughness.jpg
        │       ├── carved_stone_trim_normal.jpg
        │       ├── decorative_relief_base_color.jpg
        │       ├── decorative_relief_metallic_roughness.jpg
        │       ├── decorative_relief_normal.jpg
        │       ├── door_window_atlas_base_color.jpg
        │       ├── door_window_atlas_metallic_roughness.jpg
        │       ├── door_window_atlas_normal.jpg
        │       ├── flowers_and_leaves_base_color.png
        │       ├── flowers_and_leaves_metallic_roughness.jpg
        │       ├── flowers_and_leaves_normal.jpg
        │       ├── gray_stone_blocks_base_color.jpg
        │       ├── gray_stone_blocks_metallic_roughness.jpg
        │       ├── gray_stone_blocks_normal.jpg
        │       ├── hanging_chain_base_color.png
        │       ├── hanging_chain_metallic_roughness.jpg
        │       ├── hanging_chain_normal.jpg
        │       ├── ivy_leaves_base_color.png
        │       ├── ivy_leaves_metallic_roughness.jpg
        │       ├── ivy_leaves_normal.jpg
        │       ├── lion_head_relief_base_color.jpg
        │       ├── lion_head_relief_metallic_roughness.jpg
        │       ├── lion_head_relief_normal.jpg
        │       ├── masonry_wall_base_color.jpg
        │       ├── masonry_wall_metallic_roughness.jpg
        │       ├── masonry_wall_normal.jpg
        │       ├── ornamental_stone_base_color.jpg
        │       ├── ornamental_stone_metallic_roughness.jpg
        │       ├── ornamental_stone_normal.jpg
        │       ├── ornate_drape_blue_base_color.jpg
        │       ├── ornate_drape_green_base_color.jpg
        │       ├── ornate_drape_metallic_roughness.jpg
        │       ├── ornate_drape_normal.jpg
        │       ├── ornate_drape_red_base_color.jpg
        │       ├── plain_white_base_color.png
        │       ├── rough_stone_base_color.jpg
        │       ├── rough_stone_metallic_roughness.jpg
        │       ├── rough_stone_normal.jpg
        │       ├── stone_shield_ornament_base_color.jpg
        │       ├── stone_shield_ornament_metallic_roughness.jpg
        │       ├── stone_shield_ornament_normal.jpg
        │       ├── terracotta_roof_base_color.jpg
        │       ├── terracotta_roof_metallic_roughness.jpg
        │       ├── terracotta_roof_normal.jpg
        │       ├── woven_fabric_blue_base_color.jpg
        │       ├── woven_fabric_green_base_color.jpg
        │       ├── woven_fabric_metallic_roughness.jpg
        │       ├── woven_fabric_normal.jpg
        │       ├── woven_fabric_red_base_color.jpg
        │       ├── wrought_iron_base_color.jpg
        │       ├── wrought_iron_metallic_roughness.jpg
        │       └── wrought_iron_normal.jpg
    └── Shaders/
        ├── Composite.slang
        ├── Denoise.slang
        ├── HdrOutput.slang
        ├── Lighting.slang
        └── Primary.slang
```

文件职责：

| 文件 | 职责 |
| --- | --- |
| `Renderer.cs` | frame state、相机、jitter、reset、pass 顺序、history 交换和输出尺寸 |
| `PrimaryPass.cs` | 主 ray、GBuffer、primary material sampling 和 motion |
| `LightingPass.cs` | Direct、Indirect diffuse、Specular、shadow、environment 和 secondary material sampling |
| `DenoisePass.cs` | 两个随机分量的 temporal accumulation、moments 和 A-Trous |
| `CompositePass.cs` | 线性 HDR 分量合成 |
| `UpscalePass.cs` | Native、SGSR1、SGSR2 编排及 SGSR2 参数 |
| `OutputPass.cs` | exposure、tone mapping、sRGB 和最终 `Color`；按输出模式在 SGSR 前或后执行 tone mapping |
| `FrameData.cs` | 每帧相机、时间、尺寸、jitter、reset 和资源句柄 |
| `GBufferResources.cs` | GBuffer 资源、格式和句柄 |
| `LightingResources.cs` | noisy lighting component 资源和句柄 |
| `DenoiseResources.cs` | component history、moments、length 和 A-Trous 资源 |
| `OutputResources.cs` | Composite、upscale 和最终输出资源 |
| `SceneResources.cs` | glTF 几何、材质、纹理、sampler、primitive metadata、BLAS 和 TLAS |
| `GraphicsHelper.cs` | Sponza 私有资源创建、shader 加载、pipeline 创建和 buffer 上传 |
| `Primary.slang` | 主 ray 和 GBuffer 写入 |
| `Lighting.slang` | Direct、secondary path 和光照分量写入 |
| `Denoise.slang` | temporal、YCoCg moments 和 A-Trous |
| `Composite.slang` | HDR 分量合成 |
| `HdrOutput.slang` | exposure、tone mapping 和 sRGB |

## 3. 每帧数据流

```text
SceneResources + BLAS/TLAS
        |
        v
PrimaryPass: primary ray -> GBuffer
        |
        v
LightingPass: DirectDiffuseIrradiance + DirectSpecular
              + IndirectDiffuseIrradiance + Specular
        |
        v
DenoisePass: independent temporal accumulation -> 3-pass A-Trous
        |
        v
CompositePass: DiffuseAlbedo / PI * diffuse transport
              + DirectSpecular + denoised Specular + Emission
        |
        v
UpscalePass: Native / SGSR1 / SGSR2
        |
        v
OutputPass: exposure + tone mapping + sRGB -> Color + UI
```

`PrimaryPass` 和 `LightingPass` 可以由同一个 C# 编排对象连续 dispatch，但 shader、资源和数据语义保持独立。所有阶段使用当前内部尺寸 `R`，只有显示输出和 UI 使用 `D`。

| 阶段 | 输入 | 输出 |
| --- | --- | --- |
| `PrimaryPass` | 场景、TLAS、jittered camera | GBuffer、motion、材质属性 |
| `LightingPass` | GBuffer、场景、TLAS、当前 frame index | 四个 lighting component |
| `DenoisePass` | 两个 noisy component、GBuffer、上一帧 component history | temporal result、spatial result、moments、length |
| `CompositePass` | GBuffer、两个 Direct component、两个 spatial component、Emission | 线性 HDR |
| `UpscalePass` | Composite HDR、device depth、motion、jitter | `Native`、SGSR1 或 SGSR2 输出 |
| `OutputPass` | 线性 HDR或显示域输入 | 最终 `Color` |

## 4. 资源契约

所有资源在对应 owner 中创建和释放。表中的 layout 表示写入和读取之间的状态转换；shader 绑定同时标明写入类型和读取类型。

### 4.1 资源所有权

| Owner | 资源 | 用途 | 生命周期 |
| --- | --- | --- | --- |
| `SceneResources` | vertex/index/material/primitive buffers、纹理、sampler、BLAS/TLAS | 场景输入 | 场景生命周期 |
| `PrimaryPass` | GBuffer、motion | 主 ray 输出 | 当前内部尺寸；Resize 重建 |
| `LightingPass` | Direct 和 noisy secondary outputs | 光照输出 | 当前内部尺寸；Resize 重建 |
| `DenoisePass` | component history、moments、length、属性 history、A-Trous ping-pong | 时域和空间重建 | 当前内部尺寸；Resize 重建 |
| `CompositePass` | Composite HDR | 合成输出 | 当前内部尺寸；Resize 重建 |
| `UpscalePass` | SGSR1/SGSR2 所需公开资源和参数 | 超分输出 | 由 upscaler 生命周期管理；模式/尺寸变化时 reset |
| `OutputPass` | 最终 `Color` 和输出常量 | 显示输出 | 显示尺寸；Resize 重建 |

### 4.2 GBuffer

| 资源 | 格式 | shader 绑定 | 内容 |
| --- | --- | --- | --- |
| `WorldPosition` | `R32G32B32A32Float` | `RWTexture2D<float4>` -> `Texture2D<float4>` | 世界位置；背景 alpha 为 0 |
| `DeviceDepth` | `R32Float` | `RWTexture2D<float>` -> `Texture2D<float>` | 主 ray 的 device Z；背景为 1，供 SGSR2 使用 |
| `LinearDepth` | `R32Float` | `RWTexture2D<float>` -> `Texture2D<float>` | 线性 view-space depth |
| `ShadingNormal` | `R16G16B16A16Float` | `RWTexture2D<float4>` -> `Texture2D<float4>` | `normal * 0.5 + 0.5` |
| `GeometricNormal` | `R16G16B16A16Float` | `RWTexture2D<float4>` -> `Texture2D<float4>` | `normal * 0.5 + 0.5` |
| `BaseColorMetallic` | `R16G16B16A16Float` | `RWTexture2D<float4>` -> `Texture2D<float4>` | 线性 base color RGB，metallic A |
| `Roughness` | `R16Float` | `RWTexture2D<float>` -> `Texture2D<float>` | 线性 roughness |
| `MaterialClass` | `R32UInt` | `RWTexture2D<uint>` -> `Texture2D<uint>` | 精确材质分类或材质索引 |
| `Validity` | `R32UInt` | `RWTexture2D<uint>` -> `Texture2D<uint>` | 表面、背景和低 albedo 标记 |
| `Motion` | `R16G16UNorm` | `RWTexture2D<float2>` -> `Texture2D<float2>` | 未抖动 NDC motion，遵循 SGSR2 编码 |
| `Emission` | `R16G16B16A16Float` | `RWTexture2D<float4>` -> `Texture2D<float4>` | 主命中材质 emission |

背景必须写入明确的 `Validity` 状态。`DeviceDepth` 不承担背景识别、历史有效性和材质有效性的全部语义。

### 4.3 Lighting component

| 资源 | 格式 | shader 绑定 | 进入 Denoise |
| --- | --- | --- | --- |
| `DirectDiffuseIrradiance` | `R16G16B16A16Float` | `RWTexture2D<float4>` -> `Texture2D<float4>` | 否 |
| `DirectSpecular` | `R16G16B16A16Float` | `RWTexture2D<float4>` -> `Texture2D<float4>` | 否 |
| `IndirectDiffuseIrradiance` | `R16G16B16A16Float` | `RWTexture2D<float4>` -> `Texture2D<float4>` | 是 |
| `Specular` | `R16G16B16A16Float` | `RWTexture2D<float4>` -> `Texture2D<float4>` | 是 |

`DirectDiffuseIrradiance` 和 `IndirectDiffuseIrradiance` 都是不含 primary diffuse albedo 和 Lambert `/pi` 的 irradiance transport。`Specular` 是独立的 secondary specular radiance。

最终合成固定为：

```text
DiffuseAlbedo = BaseColor * (1 - Metallic)

CompositeHDR = max(
    DiffuseAlbedo / PI *
        (DirectDiffuseIrradiance + DenoisedIndirectDiffuseIrradiance)
    + DirectSpecular
    + DenoisedSpecular
    + Emission,
    0)
```

低 albedo 只产生低置信度标记，不执行除法。Composite 不重复 tone mapping，不加入额外 AO、经验亮度、反射增益或锐化。

### 4.4 Denoise resources

`IndirectDiffuseIrradiance` 与 `Specular` 各自拥有完整的 A/B ping-pong 资源：

| 资源组 | 格式 | shader 绑定 | 语义 |
| --- | --- | --- | --- |
| `IndirectHistoryA/B`、`SpecularHistoryA/B` | `R16G16B16A16Float` | `Texture2D<float4>` + `RWTexture2D<float4>` | temporal radiance |
| `IndirectMomentsA/B`、`SpecularMomentsA/B` | `R32G32B32A32Float` | `Texture2D<float4>` + `RWTexture2D<float4>` | Y mean、Y mean2、Co mean、Co mean2 |
| `IndirectMomentsCgA/B`、`SpecularMomentsCgA/B` | `R32G32Float` | `Texture2D<float2>` + `RWTexture2D<float2>` | Cg mean、Cg mean2 |
| `IndirectLengthA/B`、`SpecularLengthA/B` | `R32UInt` | `Texture2D<uint>` + `RWTexture2D<uint>` | 有效 history length |
| `PreviousLinearDepth` | `R32Float` | `Texture2D<float>` + `RWTexture2D<float>` | 上一帧线性深度 |
| `PreviousShadingNormal`、`PreviousGeometricNormal` | `R16G16B16A16Float` | `Texture2D<float4>` + `RWTexture2D<float4>` | 上一帧法线 |
| `PreviousRoughness` | `R16Float` | `Texture2D<float>` + `RWTexture2D<float>` | 上一帧 roughness |
| `PreviousMaterialClass`、`PreviousMotionValidity` | `R32UInt` | `Texture2D<uint>` + `RWTexture2D<uint>` | 上一帧材质和有效性 |
| `IndirectAtrousA/B`、`SpecularAtrousA/B` | `R16G16B16A16Float` | `Texture2D<float4>` + `RWTexture2D<float4>` | 当前帧空间滤波 ping-pong |

Indirect history length 上限为 32，Specular history length 上限为 8。A-Trous 只读取当前帧 temporal result 和 GBuffer；spatial result 不写回 temporal history。

## 5. Primary、材质与纹理 LOD

Primary hit 必须恢复 instance、primitive、barycentric、local/world position、几何法线、插值法线、切线空间、UV、材质索引、alpha mode、material class、device depth、linear depth 和未抖动 motion。

材质遵循 glTF metallic-roughness：

- base color 只做一次 sRGB 解码，alpha 保持线性；
- metallic-roughness 使用线性纹理，B 为 metallic，G 为 roughness；
- `roughness = clamp(factor * texture, 0.045, 1.0)`；
- `F0 = lerp(0.04, baseColor, metallic)`；
- 使用 GGX、Smith visibility、Schlick Fresnel、Lambert diffuse 和正确 TBN normal map；
- emission 只写入 `Emission`，不进入随机 component history。

纹理关系固定为：

```text
primitive -> material -> texture slot -> textures[index].source -> images[source]
```

缓存键包含 image 引用和颜色语义。base color、normal、metallic-roughness、emissive 即使引用同一 image，也分别保留格式和颜色空间。

采样规则：

1. Primary hit 根据三角形的屏幕投影计算 `du/dx`、`dv/dx`、`du/dy`、`dv/dy`，按纹理真实宽高计算 footprint；
2. Primary 可以使用 `SampleGrad`，梯度只来自当前主 ray 的有效屏幕 footprint；
3. GI、reflection 和 shadow candidate 使用 ray cone 的显式 LOD 与 `SampleLevel`；
4. ray cone 随 primary ray、secondary scatter direction、roughness 和 hit distance 传播；
5. alpha mask 使用保守、稳定的显式 LOD；
6. 不使用负 LOD bias，不用 `max(width,height)` 代替两个 UV 方向的导数。

主 ray 的有效屏幕 footprint 与 secondary ray 的 ray cone 是两套独立采样输入，不能互换。

## 6. LightingPass

### 6.1 Direct

太阳方向只由 `TimeOfDay` 决定。每像素固定生成 4 个太阳盘方向，执行 shadow RayQuery、MASK candidate、双面规则、布料透射、太阳 BRDF 和样本平均。

Direct 输出不依赖 frame sequence、不读取上一帧 history，也不进入 A-Trous。shadow candidate 必须使用对应 RayQuery 的真实 `CandidateTriangleFrontFace()`。

### 6.2 Indirect diffuse

每像素每帧生成一条 cosine-weighted hemisphere ray。设 `s = frameIndex + 0.5`，`q0/q1` 为 `PCGHash(pixel, dimension)` 产生的 `[0,1)` scramble：

```text
u = frac(0.5 + s * 0.754877666 + q0)
v = frac(0.5 + s * 0.569840296 + q1)
```

路径为 `camera ray -> first hit -> diffuse secondary ray -> second hit/environment`。secondary hit 命中 emission 时返回 emission，命中普通表面时评估该点的直接太阳可见性和环境项，miss 时返回统一环境 radiance；不继续发射第三条 ray。结果乘以完整 BSDF throughput 和 PDF，写入不含 primary albedo 与 Lambert `/pi` 的 `IndirectDiffuseIrradiance`。

### 6.3 Specular

每像素每帧生成一条 roughness-dependent GGX VNDF ray。方向使用 shading normal 和 roughness；低粗糙度路径执行有效性检查和能量 clamp。secondary hit 的 emission、普通表面光照或环境 radiance 写入 `Specular`，不并入 `DirectSpecular`，并且不共享 Indirect diffuse 的 metadata、moments 或 history。

背景、secondary miss 和 reflection miss 使用同一世界空间环境函数、颜色空间和曝光方向。体积散射只在背景路径执行固定 2 段积分，结果保持线性 HDR。

## 7. DenoisePass

### 7.1 Reprojection 与 rejection

使用未抖动 NDC motion 重投影到上一帧。历史有效条件为：

- previous UV 在 `[0,1]` 内；
- 当前和上一帧的 validity 状态相容；
- 线性深度差小于 `max(0.02, currentDepth * 0.01)`；
- shading normal dot 大于 `0.90`；
- geometric normal dot 大于 `0.95`；
- roughness 差小于 `0.10`；
- material class 完全相同；
- Specular 的反射方向 dot 大于 `0.95`；
- 当前没有 reset，上一帧 length 大于 0。

属性和 radiance history 都使用点采样。历史失效时 `alpha=1`、length 写为 1；reset 时完全忽略旧 history。

### 7.2 Temporal accumulation

两个 component 分别执行：

1. 将当前 noisy component 转换到 YCoCg；
2. 从当前 3x3 同表面邻域计算 mean、mean2 和 variance；
3. 读取重投影的 radiance、moments 和 length；
4. 以每通道 `mean ± 2.5 * sigma` clamp 历史 YCoCg；
5. 使用 `alpha = max(1 / (length + 1), minimumCurrentWeight)` 混合当前样本和历史；
6. 更新 moments、length 和 temporal result。

固定参数：Indirect `minimumCurrentWeight=0.03`、length 上限 32；Specular `minimumCurrentWeight=0.12`、length 上限 8。Moments 必须与 clamp 使用同一 YCoCg 定义；不能只保存 luminance。

### 7.3 A-Trous

每个 component 独立执行：

```text
temporal result -> step 1 -> step 2 -> step 4 -> spatial result
```

每轮使用固定 5x5 kernel。一维核为 `[1, 4, 6, 4, 1] / 16`，二维核为外积并归一化。权重包含 kernel、线性深度、shading normal、geometric normal、roughness、material class 和当前 variance confidence；Specular 额外使用反射方向相容性，并采用更窄的深度和法线权重。

A-Trous 不能跨越几何边缘或不同 material class。BaseColor、Direct 和 Emission 不进入 A-Trous。

## 8. Composite、Upscale 与 Output

### 8.1 输出链

| 模式 | 固定流程 |
| --- | --- |
| `Native` | `CompositeHDR(D) -> OutputPass(ToneMap + sRGB) -> Color(D)` |
| `Spatial` | `CompositeHDR(R) -> OutputPass(ToneMap) -> SGSR1 -> OutputPass(sRGB) -> Color(D)` |
| `Temporal` | `CompositeHDR(R) + DeviceDepth + Motion + jitter -> SGSR2 Quality -> OutputPass(ToneMap + sRGB) -> Color(D)` |

SGSR2 接收合成后的去噪线性 HDR。SGSR2 history 与 RT denoiser history 的资源和生命周期分离；reset 信号可以同时触发，但 history 不共享。

### 8.2 Motion、jitter 与 reset

motion 是同一世界表面当前和上一帧的未抖动 NDC 位移：

```text
m = currentNdc - previousNdc
previousUv.x = currentUv.x - 0.5 * m.x
previousUv.y = currentUv.y + 0.5 * m.y
encoded = m * 0.2495 + 32767 / 65535
```

Temporal 使用 8 相 Halton(2,3) jitter；Native 和 Spatial 使用零 jitter。jitter 不写入 object motion，但影响当前 primary ray 覆盖位置。

以下事件同时 reset RT component history 和 SGSR2 history：首帧、Resize、内部尺寸或 RenderScale 变化、模式变化、TimeOfDay 或环境变化、投影变化或相机大幅跳变、TLAS/BLAS/材质/纹理/sampler 变化，以及采样序列、ray count、denoise 参数或 shader 版本变化。连续相机运动使用 rejection，不自动清空 history。

## 9. 同步与布局

所有 texture 的写入阶段绑定为 storage，读取阶段绑定为 sampled；同一 dispatch 不同时绑定同一资源的 sampled 和 storage handle。典型顺序如下：

```text
GBuffer: Undefined -> Storage
PrimaryPass dispatch
barrier; GBuffer: Storage -> Sampled

Lighting outputs: Undefined -> Storage
LightingPass dispatch
barrier; Lighting outputs: Storage -> Sampled

Denoise temporal: noisy components + previous histories -> current histories
barrier
Denoise A-Trous: temporal result -> A/B ping-pong, barrier after each dispatch

Composite: component inputs -> Composite storage
barrier; Composite -> Upscale/Output sampled input
```

history 读取上一帧资源，写入当前帧 ping-pong 目标；不执行 in-place history 更新。每帧结束保存 component temporal result、moments、length、linear depth、normal、roughness、material class 和 validity。Spatial result 不属于 history。Resize 等待 GPU 使用完成后重建内部资源，并将 length 置 0。

## 10. Zenith.NET 边界与能力审查

### 10.1 公共能力

| 所需能力 | Zenith.NET 公共入口 | 结论 |
| --- | --- | --- |
| 纹理和 storage image | `GraphicsContext.CreateTexture(TextureDesc)`、`SampledHandle`、`StorageHandle` | 可表达 GBuffer、component、history 和 A-Trous |
| 格式 | `R16Float`、`R16G16UNorm`、`R16G16B16A16Float`、`R32Float`、`R32UInt`、`R32G32Float`、`R32G32B32A32Float` | 设计格式可表达，能力需运行时验证 |
| compute | `CreateShader`、`CreateComputePipeline`、`SetPipeline`、`SetConstantBuffer`、`Dispatch` | 可表达全部 pass |
| ray tracing | BLAS/TLAS descriptors、Slang `RayQuery`、`RaytracingAccelerationStructure` | 可表达主 ray、shadow 和 secondary path |
| layout 与同步 | `Transition`、`TextureLayout`、`Barrier` | 可表达阶段转换和 dispatch 间依赖 |
| copy 与初始化 | `CopyTexture`、`CopySrc`、`CopyDst` | 可表达 history 初始化和交换 |
| upscaling | `CreateSpatialUpscaler`、`CreateTemporalUpscaler`、公开 upscaler args | 可表达 Native、SGSR1、SGSR2 |

ray cone、R2 sequence、YCoCg、A-Trous 和 component composition 都是 shader 逻辑，不需要扩展 RHI。

### 10.2 边界

Sponza 只使用 Zenith.NET 公共 API。以下内容不属于本项目实现边界：native texture overload、`GetNativeObject`、后端对象强转、P/Invoke、反射、共享 RHI 修改、DirectX12/Vulkan/Metal 后端修改、`Zenith.NET.Extensions.Upscaling` 修改、厂商 SDK、光栅化光照回退和 ReSTIR reservoir。

Zenith.NET 当前没有公开的 format feature 查询。虽然公共枚举和 DX12/Vulkan/Metal 映射存在，storage image 能力仍须逐后端运行时验证。若 `CreateTexture`、shader 编译、pipeline 创建、validation layer 或首次 dispatch 失败，必须报告具体后端、格式、资源组合和公共 API 调用并停止；不能自行换格式、换后端或修改共享层。

## 11. C# 边界与布局

允许修改或新增的实现文件仅限第 2 节目标树中的 Sponza 文件。`App.cs`、Handlers 和 ImGui helper 只负责窗口、输入、相机和已有设置绑定；Renderer 将解析后的 `FrameData` 传给各 pass，pass 不直接读取 App、Camera 或全局 Settings。

所有 constant buffer 使用显式 C# layout，并与 Slang 字段顺序、对齐和大小逐项对应。矩阵遵循仓库现有行向量约定；每个 descriptor handle 单独占用 8 字节。新增资源按以下单一链路定义：

```text
resource owner -> format/usage -> layout -> shader descriptor -> lifetime
```

稳定帧不创建 pipeline、BLAS/TLAS、纹理、history 或场景数组。

## 12. 固定实现顺序

1. 建立 `FrameData`、GBuffer、component output 和显式 constant layout；
2. 完成 PrimaryPass，并验证 geometry、depth、normal、material、motion 和 primary LOD；
3. 完成 4-sample Direct、MASK 和 shadow candidate；
4. 完成 1-spp Indirect diffuse、1-spp Specular 和 secondary ray cone LOD；
5. 完成 Composite，确认关闭 Denoise 时材质纹理仍清晰；
6. 完成两套 temporal history、rejection、YCoCg clamp 和 moments；
7. 完成两套 3-pass A-Trous，并确认 spatial result 不反馈 history；
8. 接入 Native、Spatial、Temporal 输出链和独立 SGSR2 history；
9. 执行运行时验证、同步检查和验收矩阵。

## 13. 验收矩阵

| 类别 | 必须满足 |
| --- | --- |
| 稳定性 | 固定相机和 TimeOfDay 下 Direct 跨帧稳定；随机 component 方差随 length 增长下降；reset 不残留旧 history |
| 细节 | BaseColor 纹理不被 Denoise 改变；A-Trous 不跨几何或材质边界；低粗糙度反射不被 diffuse 半径抹平 |
| 运动 | 旋转和平移正确使用 motion/depth/normal/material rejection；新显露区域重新收敛；反射方向变化拒绝旧 Specular history |
| 输出 | Native、Spatial、Temporal 使用相同 Composite HDR 语义；无重复 tone mapping；SGSR2 不读取 RT history |
| 工程 | DX12 validation 和 runtime shader compilation 通过；无布局、同步、越界、失效 descriptor 或 RayQuery candidate 错误；稳态帧无资源创建 |
| 覆盖 | `RenderScale=1/0.75/0.5`、三种输出模式、6/9/12/18 时刻、固定机位、旋转、平移和 reset |

性能只记录 Primary、Lighting、Denoise temporal、A-Trous、Composite、SGSR 和 Output 的实测成本，不以未验证数据宣称 FPS 或画质达标。

## 14. 审查结论

- 数据流已将材质、Direct、Indirect diffuse、Specular 和 Emission 分离；
- Composite 只对 diffuse transport 统一复乘 albedo 和 `/pi`，避免材质色彩进入随机 radiance 滤波；
- 两套 component history 具备独立的 moments、variance、length、rejection 和 A-Trous；
- Primary footprint 与 secondary ray cone 分别负责纹理 LOD；
- RT denoiser history 与 SGSR2 history 分离；
- 目标 DX12 路径没有发现必须绕过 Zenith.NET 的功能缺口；跨后端交付仍以格式和首次 dispatch 的运行时验证为准。

因此，本设计可作为实现依据。任何公共 API 能力不足都按第 10 节的停止并报告规则处理。
