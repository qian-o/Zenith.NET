# 后续开发任务指南：FSR（3 / 4）后端实现与 CornellBox 重构规划

本文档用于衔接新会话的开发工作，完整记录当前仓库的封板状态、项目代码规范铁律、**阶段一：FSR（3 / 4）后端实现**的技术细节，以及**阶段二：CornellBox 示例项目重构**的设计规划与已知问题根因。

---

## 1. 当前进度与任务拆分

### 1.1 已封板模块（严禁擅自改动）

以下模块均已完成代码规范审查与真机验证并正式封板：

- [upscaling.md](../upscaling.md)：超分扩展公共协议、数学约定与三端参数映射规范。
- 根目录全部公共类型、[UpscalingBridge.cs](../UpscalingBridge.cs)、[UpscalingHelper.cs](../UpscalingHelper.cs)、[FrameGenerationSwapChain.cs](../FrameGenerationSwapChain.cs)。
- [MetalFX/](../MetalFX/)：macOS Metal 4 后端（超分、光线重建、帧生成）。
- [DLSS/](../DLSS/)：Windows / Linux NVIDIA NGX 后端（DirectX 12 与 Vulkan 超分、光线重建、帧生成）。

### 1.2 待完成的两阶段任务（建议分独立会话推进）

1. **阶段一：实现 `FSR/`（FSR 3 / FSR 4）后端**
   - 基于已引入的 NuGet 包 `FSR.NET`（`2.4.0`，对应 AMD FidelityFX SDK 2.4.0），在当前 `FSR/` 目录下完整实现 DirectX 12 与 Vulkan 的 FSR 后端，替换 [FSRVendor.cs](./FSRVendor.cs) 中的 `NotImplementedException` 占位符。
2. **阶段二：系统性重构 `CornellBox` 示例项目**
   - 待 `FSR` 后端封板后，对 [CornellBox](../../../Experiments/CornellBox/) 进行完整架构与交互重构，彻底解决开启帧生成时的主循环输入采样滞后问题，将其打磨为展示路径追踪、超分、光线重建与帧生成的标杆示例（**注意：阶段一开发 FSR 期间不要混入对 `CornellBox` 的重构改动**）。

---

## 2. 全局执行红线与代码风格铁律

新会话动手前**必须**先完整阅读 [upscaling.md](../upscaling.md)、[UpscalingBridge.cs](../UpscalingBridge.cs)、[FrameGenerationSwapChain.cs](../FrameGenerationSwapChain.cs) 以及 [DLSS/](../DLSS/) 与 [MetalFX/](../MetalFX/) 下的全部文件，并严格遵守以下约束：

### 2.1 执行红线

1. **禁止启用任何 Sub-Agent（严禁调用 `task` 工具）**：所有代码阅读、方案设计、编码实现与真机调试必须由主模型在同一上下文中独立完成，确保全局设计连贯一致。
2. **严格以核心 RHI 为唯一边界**：
   - `Zenith.NET.Extensions.Upscaling` 严禁引用 `Zenith.NET.DirectX12` 或 `Zenith.NET.Vulkan` 项目，也严禁调用任何操作系统/桌面窗口外部库。
   - 所有原生句柄一律通过 `GraphicsContext`、`CommandBuffer`、`Texture`、`Buffer` 的 `GetNativeObject(NativeObjectType)` 获取。
   - 阶段一仅允许在 `sources/Extensions/Zenith.NET.Extensions.Upscaling/FSR/` 目录下新增和修改文件；若真机联调中怀疑外部已封板文件存在问题，**严禁直接修改，必须先给出实测证据并询问用户确认**。
3. **零臃肿封装、零防御性编程**：
   - 不做 `Instance`、`FSRContextWrapper` 等多余中间包装层。
   - 对齐核心 RHI，不在构造函数、`Create*`、`Ensure` 或 `DispatchImpl` 中添加 `ArgumentNullException`、`NotSupportedException` 等创建期异常校验（能力可用性由调用方通过 `UpscalingCapabilities` 判断）。
   - 严禁在 `Ensure` 或 `DispatchImpl` 热路径中插入防御性的 `Queue.Timeline.Signal().Wait()`（创建原生上下文若确需独立命令缓冲区同步提交除外，参考 [DLSSVendor.cs](../DLSS/DLSSVendor.cs)）。

### 2.2 仓库代码风格清单

1. **多图形 API 文件组织（1:1 对齐 [DLSS/](../DLSS/)）**：
   - `FSR/Extensions.cs`：`internal static class Extensions`，内部 `extension(...)` 方法一律声明为 `public`（严禁在 `internal` 类内重复写 `internal`），按接收类型字母序排列。
   - `FSR/FSRFormats.cs`：`internal static class FSRFormats`，映射方法声明为 `public static` 并按参数类型字母序排列；`switch` 分支严格按 RHI 枚举（如 `PixelFormat`）声明顺序排列，以 `_ => default` 结尾。
   - `FSR/FSRVendor.cs`、`FSR/FSRVendor.DirectX12.cs`、`FSR/FSRVendor.Vulkan.cs`：使用 `partial` 拆分 DirectX 12 与 Vulkan 原生调用，`FSRVendor.cs` 负责聚合初始化、能力查询与 `switch (Context.GraphicsApi)` 路由（不支持分支安全落空，不抛异常）；纯静态参数拼装逻辑可置于 `FSRVendor.cs` 底部的 `file static unsafe class FSRHelper`。
   - 生命周期辅助方法采用对称命名（如 `CreateParameters` / `DestroyParameters`、`CreateFeature` / `DestroyFeature` / `EvaluateFeature`），不使用裸 `Create` 或混用 SDK 原始动词。
   - 功能实例类（`FSRSuperResolution`、`FSRRayReconstruction`、`FSRFrameGeneration`）：统一使用主构造函数，所有状态保持 `private`（不向 `Vendor` 传递 `this`，而是解包传递参数与句柄），采用 `if (Ensure(args)) { args.Reset = true; }` 模板，底部放置 `private readonly struct Key(...Args args)`（字段存储 RHI `PixelFormat`，顺序严格等于对应 `*Args` 声明顺序）。
2. **字段与成员排布**：
   - 字段顺序严格对齐 `VKSwapChain`：`readonly` 字段置顶紧凑排列 → 空行 → `public` 可变字段（空行分隔）→ 空行 → `private` 可变字段置底紧凑排列。
   - 仅在单个方法内使用的 `const` 声明在该方法内部顶部，不放到类顶层；仅用一两次的底层常量数值可直接写字面量。
   - 避免 `this.x = x`（优先主构造函数或只读属性）；公开可变描述使用 `{ get; private set; }`。
3. **初始化顺序与语法细节**：
   - **硬性规则**：所有 `new() { ... }` 对象初始化器与 `with { ... }` 表达式的成员赋值顺序，**必须严格等于目标类型成员的声明顺序（`MetadataToken` 顺序）**，主构造函数参数顺序严格等于字段声明顺序，完成后用反射脚本逐项核验。
   - 资源释放遵循严格后进先出（LIFO）逆序；资源 `Disposing` 订阅统一采用 `+= (sender, _) =>` 配合字典 `Remove(...)` 守卫（参考 [DLSSVendor.Vulkan.cs](../DLSS/DLSSVendor.Vulkan.cs)），不声明临时 `EventHandler` 变量或额外列表。
   - 优先使用正向卫语句提前返回减少嵌套；所有控制流语句均带 Allman 风格大括号；集合初始化统一使用 `[]`；短语句保持单行，不强行折行对齐；目标类型明确为 `uint` 时不加 `u` 后缀；不写多余注释。
   - 所有 `.cs` 文件必须为 **UTF-8 with BOM** 编码，无尾随空格，无连续空行，文件末尾仅保留一个换行符，`using` 按 `System.*` 在前、第三方在后字母序排列且无冗余引用。

---

## 3. 阶段一：`FSR`（FSR 3 / FSR 4）后端实现要点

### 3.1 `FSR.NET`（2.4.0）核心机制与 FSR 3 / 4 自动选型

1. **统一入口与 Provider 自动升级**：
   - `FSR.NET` 绑定 AMD FidelityFX SDK 2.4.0，通过 `Fsr.D3D12` 与 `Fsr.Vulkan` 提供统一的 5 个 C ABI 入口：`CreateContext`、`DestroyContext`、`Configure`、`Query`、`Dispatch`（返回码检查 `FSRApiReturnCodes.Ok`）。
   - **FSR 3 / FSR 4 兼容机制**：创建上下文时**保持 `FSROverrideVersion` 未设置默认状态**，SDK 会在支持硬件与驱动（如 AMD RDNA 4）上自动启用 **FSR 4（ML Provider）**，并在其余 DirectX 12 / Vulkan 设备（AMD / NVIDIA / Intel）上自动运行 **FSR 3**。
   - **能力探测**：通过 `FSRQueryDescGetVersions`（注意无上下文查询同样必须将 `FSRCreateBackendDX12Desc` 或 `FSRCreateBackendVKDesc` 挂在 `Header.Next` 链上）查询当前设备与图形 API 下各效果可用版本列表；不可用时对应 `UpscalingCapabilities` 属性返回 `false`，严禁在不支持设备上抛异常。
2. **描述符链与内存生命周期契约**：
   - 所有 `FSR*Desc` 结构体**必须使用 `new()` 初始化**（构造函数会自动写入正确的 `Header.Type`，切勿使用 `default` 零初始化）。
   - 后端创建描述（`FSRCreateBackendDX12Desc` / `FSRCreateBackendVKDesc`）与扩展描述通过 `Header.Next` 指针串联。
   - 若 FSR 在多次调用之间保留回调或配置内存地址，必须确保托管生命周期内指针固定有效。
   - Vulkan 后端需通过 `VulkanGetInstanceProcAddr` / `VulkanGetDeviceProcAddr` 填充 `FSRVkDeviceContext` 与函数表（可复用 `Fsr.LoadFunctions` 及 `Fsr.Vulkan.GetResource` / `GetImageResourceDescription`）；DirectX 12 资源包装可结合纹理 `TextureDesc` 构造 `FSRD3D12ResourceDesc` 或调用 `Fsr.D3D12.GetResource`。
3. **外部命令同步与布局约定**：
   - 每次调用原生 `Fsr.*.Dispatch` 前后**必须**成对包裹 `commandBuffer.BeginExternalCommands()` 与 `commandBuffer.EndExternalCommands()`，确保 Zenith 的描述符堆/资源堆状态在外部 SDK 改绑后正确恢复。
   - 所有输入纹理进出 `Dispatch` 保持 `TextureLayout.Sampled`；`Output` 按 `args.Reset ? TextureLayout.Undefined : TextureLayout.Sampled` 转入工作布局，并在 `Dispatch` 末尾统一转回 `TextureLayout.Sampled`。

### 3.2 三项能力实现细则（对齐 [upscaling.md](../upscaling.md) 第 4 节）

1. **超分（`FSRSuperResolution`，支持 DirectX 12 与 Vulkan）**
   - 使用 `FSRCreateContextDescUpscale` 与 `FSRDispatchDescUpscale`。
   - 按输入颜色格式是否为浮点（`UpscalingHelper.IsFloat`）、`args.Exposure is null` 以及 `UpscalingHelper.DecomposePerspective(args.Projection, ...)` 自动配置 `HighDynamicRange`、`AutoExposure`、`DepthInverted`、`DepthInfinite` 与运动向量低分辨率标志。
   - `JitterOffset` 正负号、相机 `NearPlane` / `FarPlane` / `CameraFovAngleVertical` 以及 `FrameTimeDelta`（`args.DeltaTimeInSeconds * 1000.0f`）严格按协议映射，并用真机斜线超分 MSE 测试核验抖动符号。

2. **光线重建（`FSRRayReconstruction`，仅支持 DirectX 12）**
   - `FSR.NET` 原生降噪器（`FSRCreateContextDescDenoiser` / `FSRDispatchDescDenoiser`）仅支持 DirectX 12，因此 Vulkan 下 `Capabilities.RayReconstructionSupported` 应为 `false`（与 [upscaling.md](../upscaling.md) 4.1 表一致）。
   - 严格按 [upscaling.md](../upscaling.md) 4.4 在同一 `CommandBuffer` 中内部串联四阶段，不向公共层暴露分路信号：
     1. **Prepare 计算阶段（Slang Compute Shader）**：由硬件 `Depth` 与 `Projection` 还原线性深度，结合内部缓存的上一帧线性深度与 `MotionVectors` 计算帧间线性深度差（`Reset` 帧置零）；将世界空间 `Normals` 编码为八面体法线并与 `Roughness` 打包；按 `DiffuseAlbedo` 与 `SpecularAlbedo` 权重将单路带噪 `Color` 解调为漫反射与镜面两路辐射度（打包命中距离）并保留自发光/残差项。
     2. **Denoise 阶段**：调用原生 `FSRDenoiser` 对漫反射与镜面辐射度执行时域+空域降噪。
     3. **Compose 计算阶段（Slang Compute Shader）**：将降噪结果重新调制 `DiffuseAlbedo`、`SpecularAlbedo` 并加回残差，若有 `TransparencyOverlay` 则按 Alpha 合成叠加。
     4. **Upscale 阶段**：送入内部超分阶段输出至 `Output`（`TextureLayout.Sampled`）。
   - **着色器预编译规范**：`Zenith.NET.Extensions.Upscaling` 运行时严禁引用 `Zenith.NET.Compiler`；请参考 [Compile.cs](../../Zenith.NET.Extensions.ImGui/Shaders/Compile.cs) 与 `ImGuiBridge.g.cs` 的模式，在 `FSR/Shaders/` 下放置 `.slang` 源码与离线生成脚本，将 DXIL字节码预编译为 `.g.cs` 静态数组。

3. **帧生成（`FSRFrameGeneration`，支持 DirectX 12 与 Vulkan）**
   - **严禁使用 `FSRCreateContextDescFrameGenerationSwapChain*` 原生交换链包装**！窗口交换链管理与双帧平滑上屏节奏已由封板的 [FrameGenerationSwapChain.cs](../FrameGenerationSwapChain.cs) 在 RHI 层统一实现。
   - `FSRFrameGeneration` 只负责在调用方传入的 `CommandBuffer` 上原位生成单张插值帧写入 `Output`：
     - 创建时使用 `FSRCreateContextDescFrameGeneration`（当传入 `Hudless` 时按需配置 `FSRCreateContextDescFrameGenerationHudless` 及对应传输函数：`R16G16B16A16Float` 线性、`R10G10B10A2UNorm` PQ、其余 sRGB）。
     - 评估时先通过 `FSRConfigureDescFrameGeneration` 配置本帧生成矩形与传输函数，再依次调用 `FSRDispatchDescFrameGenerationPrepareV2`（传入 `Depth`、`MotionVectors`、`JitterOffset` 与由 `View` / `Projection` 推导的相机矩阵及向量）和 `FSRDispatchDescFrameGeneration`（`PresentColor` 绑定 `args.Color`，`HUDLessColor` 绑定 `args.Hudless`，`Outputs[0]` 绑定 `Output`）。

### 3.3 阶段一真机验收要求

- **注意后端优先级**：在 NVIDIA RTX 机器上，`UpscalingBridge` 默认优先选中 `DLSSVendor`。验收 `FSRVendor` 时，请在会话临时目录编写独立验收程序直接测试 `FSR` 路径（严禁将临时屏蔽 `DLSSVendor` 的代码提交或遗留在工作区）。
- 同时覆盖 **DirectX 12** 与 **Vulkan**（均开启 `useValidationLayer: true`）：
  1. 验证超分抖动符号 MSE 收敛、光线重建（DX12）多帧降噪收敛且移动镜头无重投影撕裂、帧生成插值中点位置准确且 UI 无鬼影；
  2. 结合 `FrameGenerationSwapChain` 验证开启/关闭垂直同步、切换超分档位、连续窗口 `Resize` 与资源 `Dispose` 全流程 **0 验证层错误、0 泄漏、帧间隔平稳**；
  3. 反射核验所有初始化器 `MetadataToken` 顺序，并通过 `Debug` 与 `Release --no-incremental` 零警告编译。

---

## 4. 阶段二：`CornellBox` 示例项目全面重构规划

当阶段一 `FSR` 封板后，开启独立会话对 [CornellBox](../../../Experiments/CornellBox/) 进行系统性重构，使其从早期的接口适配测试升级为高质量的官方标杆示例。

### 4.1 已定位的核心痛点与根因

1. **开启帧生成时的 +1 帧（~22ms）额外输入采样延迟**
   - **根因**：Silk.NET `window.Run()` 的单帧固定调用链为 `DoEvents()`（派发系统鼠标移动事件更新 `CameraHandler`）→ `OnUpdate` → `OnRender`。
   - 关闭帧生成时，`Presentation.Present` 在 `OnRender` 末尾执行 `rendered.Wait()` 同步阻塞，醒来后立刻进入下一轮 `DoEvents()` 读取最新鼠标输入并紧接着录制渲染，输入延迟接近 `0ms`。
   - 开启帧生成时，`FrameGenerationSwapChain.Present` 异步提交后立即返回（`~0.3ms`），主循环立刻进入下一帧先执行 `DoEvents()` 过早采样鼠标，随后才在 `App.OnUpdate` 的 `presentation.WaitForPendingWork()` 中阻塞等待上一帧约 `22ms` 的 GPU 工作完成——阻塞期间产生的所有鼠标移动均积压在系统消息队列中，导致紧随其后的 `OnRender` 使用的是 **一整帧前（~22ms 前）** 的陈旧相机姿态，造成“相机不跟手”的滞后感。
2. **显示同步与帧率观测不完善**
   - `Presentation` 创建核心 `SwapChain` 与 `FrameGenerationSwapChain` 时硬编码了默认 `IsVerticalSyncEnabled = false`，未在 UI 中暴露垂直同步开关；60 FPS 原生降噪下快速横移镜头容易因非整数倍刷新率拍频产生视觉抖动（Judder）与画面撕裂。
   - 当前 `UserInterface` 仅简单将 `ImGui.GetIO().Framerate` 乘以 `2.0f` 估算帧率，缺乏清晰的真实渲染帧时间与呈现帧率展示。
3. **工程结构与交互细节待打磨**
   - `App.cs` 在 `OnUpdate`、`OnRender`、`OnFramebufferResize` 多处重复串行调用 `WaitForPendingWork()`，帧生命周期与设置变更（超分算法/质量档/光线重建/帧生成/垂直同步/全屏切换）的状态流转不够内聚。

### 4.2 `CornellBox` 重构目标与设计方向

1. **重构帧同步与低延迟输入管线**
   - 重新编排主循环同步点（确保 GPU 帧等待发生在上一帧提交末尾或输入轮询之前，使每次录制命令缓冲区前拿到的都是最新一拍的鼠标与键盘状态），将开启帧生成时的操作跟手度逼近硬件插帧的理论物理下限。
2. **完善渲染与呈现设置面板**
   - 支持实时切换垂直同步（`IsVerticalSyncEnabled`）、窗口/全屏模式、超分质量档位、光线重建开关与帧生成开关，切换过程平滑无闪烁、无验证层告警。
   - 提供准确的帧时间（`ms`）与 FPS 统计展示。
3. **对齐仓库最高代码规范**
   - 全面审查 `CornellBox` 下所有 Pass、Handler、Model 与 Shader 代码，清理历史过渡逻辑，统一字段排序、结构体初始化顺序、资源生命周期（严格 LIFO）与高 DPI 缩放处理，使其代码质量与核心 RHI 及超分扩展保持同一水准。
