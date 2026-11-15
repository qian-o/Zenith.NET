# DLSS 后端实现指南与代码规范约束

本文档用于在 Windows（NVIDIA RTX GPU）环境下启动新 Agent 会话实现 `Zenith.NET.Extensions.Upscaling` 的 `DLSS` 后端，确保新会话零偏差继承架构契约、代码规范与验收标准。

---

## 1. 任务范围与红线边界

1. **唯一实现范围**：仅在 `sources/Extensions/Zenith.NET.Extensions.Upscaling/DLSS/` 目录下实现完整的 **DLSS 后端**（基于 NuGet 包 `NGX.NET`，需同时完整支持 **DirectX 12** 与 **Vulkan** 两个图形 API），包括：
   - 超分（DLSS Super Resolution）
   - 光线重建（DLSS Ray Reconstruction / DLSSD）
   - 帧生成（DLSS Frame Generation / DLSS-G）
2. **已封板红线（严禁擅自改动）**：
   - 根目录下的全部 16 个公共 API 类型、`UpscalingBridge.cs`、`Vendor.cs`、`FrameGenerationSwapChain.cs`、`upscaling.md` 已完成封板。
   - `MetalFX/` 已完成真机封板，是**唯一标准参考范本**；实现前必须先完整阅读 `upscaling.md`、`FrameGenerationSwapChain.cs` 以及 `MetalFX/` 下的全部 6 个文件。
   - `FSR/FSRVendor.cs` 保持 `throw new NotImplementedException();` 占位符，本次不触碰。
   - 严禁让 `Zenith.NET.Extensions.Upscaling` 引用 `Zenith.NET.DirectX12` 或 `Zenith.NET.Vulkan` 项目；所有底层句柄一律通过 `GraphicsContext` / `CommandBuffer` / `Texture` 的 `GetNativeObject(NativeObjectType)` 获取。
   - 若在真机联调中怀疑核心 RHI 或 `FrameGenerationSwapChain` 存在跨平台同步问题，**严禁直接修改，必须先给出证据并询问用户确认**。

---

## 2. 目录结构与类设计规范（严格镜像 `MetalFX/`）

保持扁平、清晰、正交的文件结构，**严禁引入 `Instance`、`DLSSContext`、`Resources`、`Capturer` 等多余中间包装类**：

- `DLSS/Extensions.cs`
  - `internal static class Extensions`
  - **可见性铁律**：`internal` 类内部的成员与 `extension(...)` 方法一律声明为 **`public`**（严禁在 `internal` 类内部重复写 `internal` 修饰符）。
  - 扩展块按接收类型字母序排列，封装 NGX 返回码检查与 `GraphicsContext` / `CommandBuffer` / `Texture` 原生句柄提取。
- `DLSS/DLSSFormats.cs`
  - `internal static class DLSSFormats`
  - 所有映射/判定方法声明为 `public static`，按参数类型字母序排列；`switch` 分支严格按 RHI 枚举（如 `PixelFormat`）的声明顺序排列，并以 `_ => default` 结尾。
- `DLSS/DLSSVendor.cs`
  - `internal class DLSSVendor : Vendor`
  - 构造时按 `Context.GraphicsApi`（`DirectX12` 或 `Vulkan`）初始化 NGX（搜索路径包含 `Ngx.RuntimeDirectory`）、分配 `NVSDK_NGX_Parameter*` 参数表，并查询当前硬件与驱动对 `SuperResolution`、`RayReconstruction`、`FrameGeneration` 三项能力的实际支持情况以初始化 `Capabilities`。
  - **不抛能力不支持异常**：若当前机器无 NVIDIA GPU、驱动不支持或某项能力不可用，直接将对应 `Capabilities` 布尔值设为 `false`（即便三项全 `false` 也必须正常完成构造，不抛异常，以便 `UpscalingBridge` 自动回退至 `FSR`）。
  - `Destroy()` 中按创建逆序销毁参数表并调用对应 API 的 NGX Shutdown。
- `DLSS/DLSSSuperResolution.cs`、`DLSS/DLSSRayReconstruction.cs`、`DLSS/DLSSFrameGeneration.cs`
  - 统一使用主构造函数，例如：`internal class DLSSSuperResolution(DLSSVendor vendor, SuperResolutionDesc desc) : SuperResolution(vendor.Context, desc)`。
  - **字段布局**：直接在类顶部按逻辑先后顺序声明 `private Key key;` 与原生句柄（如 `private NVSDK_NGX_Handle* handle;`），不使用可空 `Key?`，不套内部包装类。
  - **统一的 `DispatchImpl` 与 `Ensure` 模板**：
    ```csharp
    if (Ensure(args))
    {
        args.Reset = true;
    }
    ```
    `Ensure` 内部结构：
    ```csharp
    private bool Ensure(SuperResolutionArgs args)
    {
        Key newKey = new(args);

        if (key.Equals(newKey))
        {
            return false;
        }

        key = newKey;

        // 释放旧 handle（如有），直接从 args 与 Desc 读取参数创建新 Feature（严禁从 newKey 中取参数创建！）
        return true;
    }
    ```
  - **内嵌缓存键 `Key`**：置于文件底部 `private readonly struct Key(...Args args)`，内部字段一律为 `public readonly`，字段顺序**严格等于对应 `*Args` 结构体的字段声明顺序**，并提供 `public bool Equals(Key other)`。
  - **热路径禁令**：严禁在 `Ensure` 或 `DispatchImpl` 热路径中调用 `Queue.Timeline.Signal().Wait()`。
  - **无创建期防御异常**：对齐核心 RHI，不在构造函数、`Create*`、`Ensure` 或 `DispatchImpl` 中添加 `ArgumentNullException`、`NotSupportedException` 等创建期异常检查。
  - **销毁顺序**：`Destroy()` 中先释放自身原生 Feature Handle，再调用 `base.Destroy()` 释放 `Output`。

---

## 3. 仓库级代码风格铁律（零容忍）

1. **文件编码与排版**：
   - 所有 `.cs` 文件必须使用 **UTF-8 with BOM** 编码，无尾随空格，禁止连续两个空行，文件末尾仅保留一个换行符。
   - 文件级命名空间：`namespace Zenith.NET.Extensions.Upscaling.DLSS;`。
   - `using` 排序：`System.*` 按字母序排在最前，第三方命名空间（如 `NGX.NET`）按字母序排在后；**严禁保留任何未使用的 `using`**。
2. **结构体赋值顺序 = 声明顺序（硬性规则）**：
   - 所有 `new() { ... }` 对象初始化器以及对原生结构体的连续成员赋值，**必须严格按照目标类型成员的声明顺序（`MetadataToken` 顺序）书写**。完成后必须用反射脚本逐一核验。
3. **语法与书写细节**：
   - 只要没有多语句构造体，`internal` / `private` 类和结构体一律使用**主构造函数**。
   - 集合初始化一律使用集合表达式 `[]` 或 `[with(...)]`。
   - 参数不长的方法调用或构造保持单行，不强行拆行对齐。
   - 单行 `using Foo x = new();` 与紧接着使用 `x` 的语句之间**不加空行**；多行初始化器 `using Foo x = new() { ... };` 的 `};` 后空一行。
   - 所有 `if` / `for` / `foreach` / `while` 即使只有一行语句也必须带 Allman 风格大括号 `{ ... }`。
   - 仅在单个方法内部使用的 `const` 声明为该方法内的局部常量，不放到类顶层。
   - 目标类型已明确为 `uint` 时不写多余的 `u` 后缀（写 `0`、`1`、`8` 而非 `0u`、`1u`、`8u`）。
   - 除极少数反直觉的底层契约说明外，**不写多余注释**。
   - 事件订阅与注销（如订阅 `Texture.Disposing`）及资源销毁必须严格遵循后进先出（LIFO）逆序。

---

## 4. NGX.NET 与双图形 API 实现要点

1. **NGX.NET 调用约定**：
   - 使用 `Ngx.D3D12` 与 `Ngx.Vulkan` 入口，检查返回码使用 `Ngx.NVSDK_NGX_SUCCEED(...)` / `Ngx.NVSDK_NGX_FAILED(...)`。
   - 原生结构体务必使用 `new()` 初始化（确保 SDK `StructVersion` 等默认字段正确填充，切勿使用零初始化的 `default`）。
   - 参数名优先使用 `NGXStrings` 提供的稳定原生字符串常量，避免每帧产生临时字符串分配。
   - 若 NGX 在多次调用间持有某个数组或回调指针地址，必须确保其生命周期内内存固定有效。
2. **外部命令同步与描述符堆恢复（极其关键）**：
   - 调用 NGX `EvaluateFeature` 前后**必须**成对调用：
     ```csharp
     commandBuffer.BeginExternalCommands();
     // NGX EvaluateFeature ...
     commandBuffer.EndExternalCommands();
     ```
     在 DirectX 12 与 Vulkan 下，NGX 内部会改绑自己的描述符堆/管线状态；`EndExternalCommands()` 会发出全局屏障并重新绑定 Zenith 的 `CbvSrvUavHeap` / `ResourceHeap` 与 `SamplerHeap`，漏掉会导致后续渲染直接崩溃或黑屏。
   - 资源布局契约：所有输入纹理进出 `Dispatch` 均保持 `TextureLayout.Sampled`；`Output` 在 `BeginExternalCommands()` 前 `Transition(Output, default, TextureLayout.Undefined, TextureLayout.Storage)`，在 `EndExternalCommands()` 后 `Transition(Output, default, TextureLayout.Storage, TextureLayout.Sampled)`。
3. **Vulkan 下的 `VkImageView` 缓存管理**：
   - `Zenith.NET.Vulkan` 已在设备层启用 `VK_NVX_binary_import` 与 `VK_NVX_image_view_handle`，但 `VKTextureView` 使用 `VK_EXT_descriptor_heap` 无视图描述符，`GetNativeObject` 不直接提供 `VkImageView`。
   - 在 Vulkan 路径构建 `NVSDK_NGX_Resource_VK` 时，需通过 `context.GetNativeObject(NativeObjectType.VulkanGetDeviceProcAddr)` 获取 `vkCreateImageView` / `vkDestroyImageView` 函数指针，在 `DLSSVendor` 内按 `(Texture, IsStorage)` 缓存创建的 `VkImageView`，并订阅 `texture.Disposing`（及 `DLSSVendor.Destroy`）及时销毁，严禁每帧重复创建泄漏 `VkImageView`。
4. **图形学与数学语义对齐（详见 `upscaling.md` 第 4 节）**：
   - **运动向量**：输入为 UV 空间 `PreviousUV - CurrentUV`（尺寸 `RenderWidth × RenderHeight`，不含抖动），创建时开启 `MVLowRes`，评估时传入 `MVScaleX = Desc.RenderWidth`、`MVScaleY = Desc.RenderHeight`。
   - **亚像素抖动**：`args.JitterOffset` 为像素偏移（`[-0.5, 0.5]`，+X 向右、+Y 向下），必须用真机超分 MSE 测试核实 NGX `JitterOffsetX / Y` 的正负号（与 `CornellBox` 的 `PathTracing.slang` 及 `UpscalingHelper.JitterProjection` 对齐）。
   - **相机投影分解**：复用 `UpscalingHelper.DecomposePerspective(args.Projection, out float nearPlane, out float farPlane, out float verticalFieldOfViewInRadians, out float aspectRatio, out bool depthReversed, out bool depthInfinite)`，自动设置 `DepthInverted`、近远平面、垂直视场角、宽高比与无限远平面标志。
   - **曝光与 HDR**：`args.Exposure is null` 时开启 `AutoExposure`，非空时关闭并绑定 `Exposure`（1×1 `R16Float`）；`PreExposure ?? 1.0f`；按颜色格式是否为浮点格式设置 `IsHDR`。
   - **光线重建（DLSSD）**：绑定单路带噪线性 `Color`、`Depth`、`MotionVectors`、`DiffuseAlbedo`、`SpecularAlbedo`、`Normals`（世界空间单位向量）、`Roughness`（线性粗糙度）及可选 `SpecularHitDistance`、`ReactiveMask`（`BiasCurrentColorMask`）、`TransparencyOverlay`、`Exposure`；传入 `WorldToViewMatrix`（`args.View`）与 `ViewToClipMatrix`（`args.Projection`）并在真机核验矩阵内存行列布局。
   - **帧生成（DLSS-G）**：
     - `Backbuffer` 绑定 `args.Color`（含 UI 的呈现帧），`Hudless` 绑定 `args.Hudless`（为 `null` 时传空资源），输出写入 `Output`（由 `FrameGenerationSwapChain` 在 `GraphicsQueue` 上原位调用并调度双帧等距上屏）。
     - 根据 `args.Color.Desc.Format` 自动推导颜色空间/传输函数（`R16G16B16A16Float` 线性 scRGB，`R10G10B10A2UNorm` PQ，其余 8 位格式 sRGB）。

---

## 5. 真机验收清单（完成前必须逐项实测通过）

不能仅凭编译通过结束任务，必须在 Windows NVIDIA RTX 硬件上同时覆盖 **DirectX 12** 与 **Vulkan**（均开启 `useValidationLayer: true`）完成以下验收：

1. **数值与画质精度测试**：
   - **超分抖动符号测试**：对高频斜线图案做 2x 超分，对比正反 `JitterOffset` 符号与 2x 原生真值的 MSE，确认符号方向绝对正确。
   - **光线重建降噪测试**：输入带噪光线追踪颜色与 G-Buffer 引导图，验证多帧时域方差显著收敛、移动镜头下无法线/矩阵转置导致的重投影撕裂。
   - **帧生成中点与 UI 分离测试**：测试水平移动方块在插值帧中的质心精确落在上一帧与当前帧正中间，且静态 UI 像素固定无鬼影。
2. **`CornellBox` 实机验证**：
   - 在 DirectX 12 与 Vulkan 下分别验证 `SuperResolution`、`RayReconstruction`、质量档动态切换、`FrameGenerationSwapChain` 开关、窗口 `Resize` 与正常退出 `Dispose`，确认零验证层错误、零崩溃、视觉帧率丝滑翻倍。
3. **工程规范终检**：
   - 运行反射脚本确认所有结构体初始化器与连续属性赋值严格符合 `MetadataToken` 声明顺序。
   - 检查全部新增/修改文件均为 UTF-8 BOM、无多余空行/尾随空格、`using` 排序合规。
   - `Debug`、`Release` 无增量编译及 `PublishAot=true`（`win-x64`）均 **0 警告、0 错误**通过。
   - 验收完成后可按需清理本临时指南文件。
