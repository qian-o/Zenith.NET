# FSR（3 / 4）后端实现指南与后续 CornellBox 重构规划

本文档用于衔接新会话的开发工作，完整沉淀当前超分扩展的封板边界、全套仓库代码规范铁律、**阶段一：`FSR`（3 / 4）后端实现规范**，以及**阶段二：`CornellBox` 示例重构蓝图**。

---

## 1. 任务边界与两阶段路线

### 1.1 已封板红线（严禁擅自修改）

以下模块均已完成严格代码规范终审与真机验证并正式封板：

- [upscaling.md](../upscaling.md)：超分扩展公共协议、数学约定与三端参数映射规范。
- 根目录全部 16 个公共 API 类型（其中 `SuperResolution`、`RayReconstruction`、`FrameGeneration`、`FrameGenerationSwapChain` 均统一继承 `GraphicsResource`）、[UpscalingBridge.cs](../UpscalingBridge.cs)、[UpscalingHelper.cs](../UpscalingHelper.cs)、[FrameGenerationSwapChain.cs](../FrameGenerationSwapChain.cs)。
- [MetalFX/](../MetalFX/)：macOS Metal 4 后端（单 API 标准范本）。
- [DLSS/](../DLSS/)：Windows / Linux NVIDIA NGX 后端（DirectX 12 + Vulkan 双 API 标准范本）。

### 1.2 分阶段推进原则

1. **阶段一（当前首要任务）**：仅在 `sources/Extensions/Zenith.NET.Extensions.Upscaling/FSR/` 目录下，基于 NuGet 包 `FSR.NET`（`2.4.0`，对应 AMD FidelityFX SDK 2.4.0）完整实现支持 **DirectX 12** 与 **Vulkan** 的 FSR 后端（自动覆盖 FSR 3 与 FSR 4），替换 [FSRVendor.cs](./FSRVendor.cs) 的占位实现。三项能力均按双 API 对等设计，是否支持一律由接口查询决定，严禁按图形 API 写死。
2. **阶段二（后续独立会话任务）**：待 `FSR/` 封板后，单独对 [CornellBox](../../../Experiments/CornellBox/) 进行系统级重构（阶段一开发期间**严禁混入对 `CornellBox` 的重构改动**）。
3. **全局执行禁令**：
   - **严禁启动任何 Sub-Agent（禁止调用 `task` 工具）**：所有阅读、设计、编码与真机调试必须由主模型在同一上下文内独立完成，确保设计思路绝对统一。
   - **严禁突破 RHI 边界**：严禁引用 `Zenith.NET.DirectX12`、`Zenith.NET.Vulkan` 或运行时引用 `Zenith.NET.Compiler`；严禁调用任何操作系统/桌面窗口外部库；所有原生句柄一律通过 `GetNativeObject(NativeObjectType)` 获取。
   - 若真机联调中怀疑已封板文件存在问题，**严禁直接修改，必须先给出实测证据并向用户确认**。

---

## 2. `FSR/` 目录结构与类设计规范（1:1 对齐 [DLSS/](../DLSS/)）

动手前必须先完整阅读 [upscaling.md](../upscaling.md)、[UpscalingBridge.cs](../UpscalingBridge.cs)、[FrameGenerationSwapChain.cs](../FrameGenerationSwapChain.cs) 以及 [DLSS/](../DLSS/) 与 [MetalFX/](../MetalFX/) 下的全部文件，保持扁平正交结构，**严禁引入 `Instance`、`FSRContextWrapper` 等多余中间包装类**：

- `FSR/Extensions.cs`
  - `internal static class Extensions`
  - 内部 `extension(...)` 块按接收类型字母序排列，所有方法一律声明为 `public`（严禁在 `internal` 类内重复写 `internal`），封装 `FSRApiReturnCodes` 检查（失败时 `Debug.WriteLine`）与 `GraphicsContext` / `CommandBuffer` / `Texture` / `Buffer` 原生句柄提取。
- `FSR/FSRFormats.cs`
  - `internal static class FSRFormats`
  - 映射方法一律为 `public static`，按 RHI 参数类型字母序排列；`switch` 分支严格按 RHI 枚举（如 `PixelFormat`）声明顺序排列，以 `_ => default` 结尾。
- `FSR/FSRVendor.cs`、`FSR/FSRVendor.DirectX12.cs`、`FSR/FSRVendor.Vulkan.cs`
  - `internal unsafe partial class FSRVendor : Vendor`
  - `FSRVendor.cs` 聚合构造初始化、能力探测与 `switch (Context.GraphicsApi)` 路由（不支持的图形 API 分支安全落空，不抛异常）；`switch` 只负责原生细节路由，不参与决定能力布尔值；DirectX 12 与 Vulkan 原生细节拆分至对应 `partial` 文件；纯静态参数构造辅助置于 `FSRVendor.cs` 底部的 `file static unsafe class FSRHelper`。
  - 生命周期辅助方法采用对称带名词命名（如 `CreateFeature` / `DestroyFeature` / `EvaluateFeature`），不使用裸 `Create` 或混用 SDK 原始动词。
- `FSR/FSRSuperResolution.cs`、`FSR/FSRRayReconstruction.cs`、`FSR/FSRFrameGeneration.cs`
  - 统一使用主构造函数（如 `internal class FSRSuperResolution(FSRVendor vendor, SuperResolutionDesc desc) : SuperResolution(vendor.Context, desc)`）。
  - 基类 `SuperResolution` / `RayReconstruction` / `FrameGeneration` 已继承 `GraphicsResource` 并统一实现了 `GetNativeObject` 与 `SetResourceName`，**子类严禁重复重写这两个方法**。
  - 所有内部状态保持 `private`（不向 `Vendor` 传 `this`，解包传递所需句柄与参数），统一采用 `if (Ensure(args)) { args.Reset = true; }` 模板，文件底部放置 `private readonly struct Key(...Args args)`（字段存储 RHI `PixelFormat`，顺序严格等于对应 `*Args` 声明顺序）。
- `FSR/Shaders/` 与预编译字节码（供 `FSRRayReconstruction` 内部阶段使用）
  - 参考 [Compile.cs](../../Zenith.NET.Extensions.ImGui/Shaders/Compile.cs) 与 `ImGuiBridge.g.cs` 的离线生成模式，在 `FSR/Shaders/` 下维护 `.slang` 源码与生成脚本，将编译后的着色器字节码固化为 `.g.cs` 静态数组。

---

## 3. 仓库级代码风格铁律（零容忍）

1. **零防御、零冗余包装**：
   - 不在构造函数、`Create*`、`Ensure` 或 `DispatchImpl` 中添加创建期参数/能力异常检查（调用方通过 `UpscalingCapabilities` 自行判断）；不在 `Ensure` / `DispatchImpl` 热路径插入 `Timeline.Signal().Wait()`。
   - 不保留仅做单行转发的无意义私有包装方法（如直接调用 `UpscalingBridge.CreateOutput(...)`）；内部与私有方法**严禁使用默认可选参数**（调用处显式传参）。
2. **字段、属性与嵌套类型规范**：
   - **字段排布顺序**（对齐 `VKSwapChain` 与 [FrameGenerationSwapChain.cs](../FrameGenerationSwapChain.cs)）：`readonly` 字段置顶紧凑排列（**带内联初始化器的 `readonly` 字段排在构造函数赋值的 `readonly` 字段之前**，使声明顺序与运行时初始化顺序一致）→ 空行 → 值类型 `public` 可变字段（如有，空行分隔）→ 空行 → `private` 可变字段置底紧凑排列。
   - **引用类型必须属性化**：任何 `class`（含嵌套 `private class`）上对外暴露的托管引用类型成员（如 `Texture`、`Buffer`、`ManualResetEventSlim`）**必须声明为属性**（`{ get; }` / `{ get; set; }`），严禁声明为公共字段；`readonly struct` 数据载体则使用 `public readonly` 字段。
   - **私有类型隔离与直接依赖**：仅供单类使用的辅助类型一律声明为该类底部的同级 `private` 嵌套类型（不泄漏到命名空间 `internal` 层，且嵌套类型名不重复外层类前缀）；直接持有需要的 `private readonly` 依赖字段，严禁为回避字段而暴露外部不访问的属性或通过子对象绕弯取值。
   - **局部常量下沉**：仅在单个方法内使用的 `const` 声明在该方法顶部，不挂在类顶层；仅用一两次的底层常量数值直接写字面量。
3. **命名、分组、初始化顺序与生命周期**：
   - **命名去冗余**：字段与局部变量不带 `*Stopwatch`、`*List` 等类型后缀，索引与同步参数对齐核心 RHI 惯用词（`index`、`wait`、`layout`）。
   - **语义空行分组**：方法内部按单一语义阶段用单个空行分段，同一状态的成对更新（如 `handle.Dispose(); handle = default;`）紧贴成组。
   - **初始化顺序 = 声明顺序（硬性规则）**：所有 `new() { ... }` 与 `with { ... }` 成员赋值顺序必须严格等于目标类型成员的 `MetadataToken` 声明顺序，主构造函数参数顺序严格等于字段声明顺序，完成后必须用反射脚本核验。
   - **严格 LIFO 销毁与事件订阅**：`Destroy()` 严格按实际初始化逆序释放资源；订阅资源 `Disposing` 统一采用 `+= (sender, _) =>` 配合缓存字典 `Remove(...)` 守卫（参考 [DLSSVendor.Vulkan.cs](../DLSS/DLSSVendor.Vulkan.cs)）。
   - **文件排版**：全部 `.cs` 文件使用 **UTF-8 with BOM** 编码，Allman 大括号，集合统一用 `[]`，短语句单行不折行对齐，目标类型为 `uint` 时不写 `u` 后缀，不写多余注释，无尾随空格、无连续空行、文件末尾单换行，`using` 按 `System.*` 在前、第三方在后字母序排列。

---

## 4. 阶段一：`FSR.NET`（2.4.0）与三项能力实现规范

### 4.1 `FSR.NET` 统一 C ABI 与 FSR 3 / 4 自动升级机制

1. **五大入口与默认 Provider**：
   - 使用 `Fsr.D3D12` 与 `Fsr.Vulkan` 下的 `CreateContext`、`DestroyContext`、`Configure`、`Query`、`Dispatch`，返回码统一检查 `FSRApiReturnCodes.Ok`。
   - **自动支持 FSR 3 与 FSR 4**：创建上下文时**不设置 `FSROverrideVersion`**，由 FidelityFX SDK 2.4.0 默认 Provider 自动选型——在支持的硬件与驱动（如 AMD RDNA 4）上自动启用 **FSR 4（ML Provider）**，其余 DirectX 12 / Vulkan 设备自动运行 **FSR 3**。
   - **能力探测（唯一判定来源）**：构造 `FSRVendor` 时对 **DirectX 12 与 Vulkan 一视同仁**，三项能力（超分 / 光线重建 / 帧生成）均使用 `FSRQueryDescGetVersions`（`CreateDescType` 分别填入 `new FSRCreateContextDescUpscale()`、`new FSRCreateContextDescDenoiser()`、`new FSRCreateContextDescFrameGeneration()` 的 `Header.Type`；DirectX 12 传入 `Device`，Vulkan 还需在 `Header.Next` 串联 `FSRCreateBackendVKDesc`）查询可用版本数 `OutputCount > 0`，用三项查询结果直接构造 `UpscalingCapabilities`。**严禁按 `GraphicsApi` 硬编码任何能力布尔值**（例如不得因当前 SDK 仅在 DX12 提供原生降噪器就把 Vulkan 的光线重建写死为 `false`）；设备不支持某项能力（或全部不支持）时由查询结果自然得出 `false` 并正常完成构造，严禁抛异常。这样未来 SDK / 驱动在任一 API 下补齐能力时无需改动代码即可自动启用。
2. **结构体初始化与指针链**：
   - 所有 `FSR*Desc` 结构体**必须使用 `new()` 初始化**（构造函数负责填充 `Header.Type`，切勿使用 `default`）。
   - 后端创建描述（`FSRCreateBackendDX12Desc` / `FSRCreateBackendVKDesc`）与扩展描述通过 `Header.Next` 串联；若 SDK 跨调用持有托管数据地址，生命周期内必须保持固定有效。
   - DirectX 12 资源可通过 `Fsr.D3D12.GetResource` 包装；Vulkan 需通过 `VulkanGetInstanceProcAddr` / `VulkanGetDeviceProcAddr` 初始化 `FSRVkDeviceContext` 与 `Fsr.LoadFunctions`，并通过 `Fsr.Vulkan.GetResource` / `GetImageResourceDescription` 构造 `FSRApiResource`。
3. **外部命令同步与布局契约**：
   - 原生 `Dispatch` 前后**必须**成对调用 `commandBuffer.BeginExternalCommands()` 与 `commandBuffer.EndExternalCommands()`，并在开启验证层下确认描述符堆/资源堆状态恢复无误。
   - 所有输入纹理进出 `Dispatch` 保持 `TextureLayout.Sampled`；`Output` 按 `args.Reset ? TextureLayout.Undefined : TextureLayout.Sampled` 转入工作布局，并在末尾转回 `TextureLayout.Sampled`。

### 4.2 三项能力技术细则（严格对齐 [upscaling.md](../upscaling.md) 第 4 节）

1. **超分（`FSRSuperResolution`，DirectX 12 + Vulkan）**
   - 使用 `FSRCreateContextDescUpscale`（`MaxRenderSize`、`MaxUpscaleSize`、按 `UpscalingHelper.IsFloat(args.Color.Desc.Format)` / `args.Exposure is null` / `UpscalingHelper.IsDepthReversed(args.Projection)` / 无限远平面组合 `FSRApiCreateContextUpscaleFlags`）与 `FSRDispatchDescUpscale`。
   - 评估时传入 `Color`、`Depth`、`MotionVectors`、可选 `Exposure` 与 `Reactive`、输出 `Output`、`JitterOffset`（真机斜线超分 MSE 核验正负号）、`MotionVectorScale`（渲染宽高）、`FrameTimeDelta`（`args.DeltaTimeInSeconds * 1000.0f`）、`PreExposure`（`args.PreExposure ?? 1.0f`）及由 `UpscalingHelper.DecomposePerspective` 分解的 `CameraNear`、`CameraFar`、`CameraFovAngleVertical`、`ViewSpaceToMetersFactor = 1.0f`。

2. **光线重建（`FSRRayReconstruction`，DirectX 12 + Vulkan）**
   - 按双 API 对等设计：Prepare / Compose 为跨 API 的预编译 Slang Compute 阶段，Denoise 与 Upscale 阶段按 `Context.GraphicsApi` 路由到对应原生入口；`Capabilities.RayReconstructionSupported` 一律由 4.1 的 `FSRQueryDescGetVersions` 查询结果决定，**严禁按图形 API 写死**。当前 `FSR.NET 2.4.0` 仅在 DirectX 12 提供原生降噪器（`amd_fidelityfx_denoiser_dx12.dll`），因此 Vulkan 下查询结果自然为 `false`；未来 SDK / 驱动补齐 Vulkan 降噪器时无需改动代码即可自动启用。
   - 按 [upscaling.md](../upscaling.md) 4.4 在同一 `CommandBuffer` 中串联四阶段，内部消化分路信号差异：
     1. **Prepare（预编译 Slang Compute）**：由硬件 `Depth` 与 `Projection` 还原线性深度，结合内部缓存的上一帧线性深度与 `MotionVectors` 计算帧间线性深度差打包至 3D 运动向量 Z 分量（`Reset` 帧置零）；将世界空间 `Normals` 编码为八面体法线并与 `Roughness` 打包；按 `DiffuseAlbedo` 与 `SpecularAlbedo` 权重将单路 `Color` 解调为漫反射与镜面两路辐射度（打包命中距离）并保留残差项。
     2. **Denoise（原生 `FSRDenoiser`）**：使用 `FSRCreateContextDescDenoiser` 与 `FSRDispatchDescDenoiser`（通过 `Header.Next` 串联 `FSRDispatchDescDenoiserIndirectDiffuse` 与 `FSRDispatchDescDenoiserDirectSpecular` 信号描述，传入 `LinearDepth`、3D `MotionVectors`、打包法线、`SpecularAlbedo`、`DiffuseAlbedo`、`CameraPositionDelta`、`View`、`Projection` 等）。
     3. **Compose（预编译 Slang Compute）**：将降噪后的两路辐射度重新调制 `DiffuseAlbedo`、`SpecularAlbedo` 并加回残差，若提供 `TransparencyOverlay` 则执行 Alpha 合成叠加。
     4. **Upscale**：将合成后的干净线性颜色送入内部超分阶段输出至 `Output`（`TextureLayout.Sampled`）。

3. **帧生成（`FSRFrameGeneration`，DirectX 12 + Vulkan）**
   - **严禁使用 `FSRCreateContextDescFrameGenerationSwapChain*` 原生交换链包装**！呈现队列与双帧等距调度由已封板的 [FrameGenerationSwapChain.cs](../FrameGenerationSwapChain.cs) 统一承担。
   - 创建：使用 `FSRCreateContextDescFrameGeneration`（`DisplaySize` 为输出宽高、`MaxRenderSize` 为渲染宽高、`BackBufferFormat` 为 `Desc.Format`；当存在 `Hudless` 时在 `Header.Next` 串联 `FSRCreateContextDescFrameGenerationHudless`）。
   - 评估：维护单调递增的 `ulong frameId`，同一次 `DispatchImpl` 中三步共享同一 `FrameID`：
     1. 调用 `Configure` 传入 `FSRConfigureDescFrameGeneration`（设置 `FrameGenerationEnabled = true`、`AllowAsyncWorkloads = false`、`HUDLessColor` 绑定 `args.Hudless` 或空资源、`GenerationRect`、`FrameID`）；
     2. 调用 `Dispatch` 传入 `FSRDispatchDescFrameGenerationPrepareV2`（绑定 `Depth`、`MotionVectors`、`RenderSize`、`JitterOffset`、`MotionVectorScale`、`FrameTimeDelta`、`Reset`、投影分解近远平面与视场角，以及由 `Matrix4x4.Invert(args.View, ...)` 提取的 `CameraPosition`、`CameraUp`、`CameraRight`、`CameraForward`）；
     3. 调用 `Dispatch` 传入 `FSRDispatchDescFrameGeneration`（`PresentColor` 绑定 `args.Color`，`Outputs[0]` 绑定 `Output`，`NumGeneratedFrames = 1`，`BackBufferTransferFunction` 按 `R16G16B16A16Float` 线性、`R10G10B10A2UNorm` PQ、其余 sRGB 自动映射）。

---

## 5. 阶段一真机验收清单

> **NVIDIA 开发机验证提示**：由于 `UpscalingBridge` 在 DX12 / Vulkan 下优先选取 `DLSSVendor`，在 NVIDIA GPU 机器上实测 `FSRVendor` 时，请在会话临时目录（`session-state/files`）编写独立测试工程直接覆盖 `FSR` 后端，验收完毕后清理临时文件，严禁在仓库代码中遗留屏蔽 `DLSSVendor` 的改动。

必须在开启 `useValidationLayer: true` 的前提下同时跑通 **DirectX 12** 与 **Vulkan**：

1. **算法精度验证**：
   - 超分：高频斜线图案 2x 超分对比正反 `JitterOffset` 符号的 MSE，确认抖动方向与历史收敛正确。
   - 光线重建（在查询结果报告支持该能力的 API 上验证，当前为 DX12）：输入带噪光线追踪颜色与 G-Buffer 引导图，验证时域方差显著下降且镜头旋转无重投影撕裂或能量丢失；Vulkan 下若查询结果为不支持，则核验 `Capabilities.RayReconstructionSupported` 为 `false` 且构造正常完成、不抛异常。
   - 帧生成：运动物体在插值帧 `Output` 中的质心精确落在相邻两真实帧中点，含 UI 的 `Color` 与不含 UI 的 `Hudless` 分离正确、静态 UI 无伪影。
2. **交换链联调与生命周期验证**：
   - 对接 `FrameGenerationSwapChain` 测试 VSync 开/关、动态切换超分分辨率、连续窗口 `Resize` 冷却恢复以及 `Dispose` 退出，确认 **0 验证层错误/警告、0 资源泄漏、翻转间隔均匀**。
3. **工程终检**：
   - 运行反射脚本确认全部 `new() { ... }`、`with { ... }` 严格符合 `MetadataToken` 声明顺序；
   - 检查全部 `.cs` 文件均为 UTF-8 BOM、零连续空行、零尾随空格、单行尾换行；
   - 全解决方案 `Debug` 与 `Release --no-incremental` 编译 **0 警告、0 错误**。

---

## 6. 阶段二备忘：`CornellBox` 示例项目重构蓝图

待阶段一 `FSR` 后端正式封板后，在独立新会话中对 [CornellBox](../../../Experiments/CornellBox/) 进行全面重构，将其从接口联调 Demo 升级为高质量官方标杆示例：

1. **修复开启帧生成时的 +1 帧（~22ms）主循环输入采样滞后**
   - **现状根因**：Silk.NET `window.Run()` 每轮固定按 `DoEvents()`（派发鼠标移动更新相机）→ `OnUpdate` → `OnRender` 执行。开启帧生成后 `Present` 异步立即返回，下一轮先执行 `DoEvents()` 过早采样鼠标，随后才在 `App.OnUpdate` 的 `presentation.WaitForPendingWork()` 中阻塞等待上一帧 GPU 完成，导致阻塞期间的鼠标移动积压在系统消息队列，紧随其后的 `OnRender` 使用的是一整帧前的旧相机矩阵。
   - **重构要求**：重新编排主循环同步时机，确保每次录制渲染命令前采样到的是最新输入，消除非必要的软件管线延迟。
2. **收敛窗口尺寸变更时序（纯事件驱动 + 交换链优先 `Resize`）**
   - **现状根因**：当前 `App.OnFramebufferResize` 先执行耗时的 `WaitForPendingWork()` 与 `renderer.Resize()`，最后才调用 `presentation.Configure()` 触发交换链 `Resize`；在 Vulkan 开启帧生成切全屏/窗口化时，这几十毫秒空窗期会导致后台呈现线程继续访问已 `OUT_OF_DATE` 的 `VkSwapchainKHR` 触发验证层报错。
   - **重构要求**：所有窗口尺寸变更**仅由 `FramebufferResize` 回调单一入口驱动**（严禁在切换窗口状态处手动主动调用 `Resize`）；且回调内**必须最先执行交换链 `Resize`**（第一时间中断并排空后台呈现线程、更新交换链），随后再重建各渲染 Pass 资源。
3. **完善交互面板与统一代码规范**
   - 增加垂直同步（`IsVerticalSyncEnabled`）开关、窗口/全屏切换以及准确的帧时间（`ms`）与呈现帧率统计展示；
   - 全面清理历史过渡逻辑，使所有 Pass、Handler、Model 与 Shader 的字段排布、属性化规则、结构体初始化顺序、高 DPI 处理及 LIFO 生命周期严格对齐核心 RHI 与超分扩展标准。
