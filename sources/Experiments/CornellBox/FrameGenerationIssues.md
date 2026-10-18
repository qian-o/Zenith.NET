# 帧生成（DLSS FG / MFG）待解决问题

基于提交 `cdd1c1b` 整理，测试环境为 RTX 4070 Ti SUPER（`MaxGeneratedFrameCount = 1`）和 240 Hz 显示器。

参考资料：

- [NGX DLSS-G 头文件](https://github.com/NVIDIA/DLSS/blob/main/include/nvsdk_ngx_defs_dlssg.h)
- dxvk-remix（下文简称 Remix）直接调用 NGX DLSS-G 的集成代码：[rtx_ngx_wrapper.cpp](https://github.com/NVIDIAGameWorks/dxvk-remix/blob/main/src/dxvk/rtx_render/rtx_ngx_wrapper.cpp)、[rtx_dlfg.cpp](https://github.com/NVIDIAGameWorks/dxvk-remix/blob/main/src/dxvk/rtx_render/rtx_dlfg.cpp)

## 画质 / 参数

1. **JitterOffset 的单位可能不对（待确认）。** [Renderer.cs](Renderer.cs) 的 `Generate` 按 NGX 头文件的注释（"clip space jitter offset"）把 FG 的 jitter 转成了裁剪空间（`2 * jitter / size`）。
   - Remix 传的是像素空间 jitter，和 SR 用的是同一个值。
   - MvecScale 也有同样的分歧：头文件说"归一化到 [-1,1]"，Remix 却按像素空间传 `(1, 1)`。
   - 我们的 MvecScale 复用了 RR 的像素缩放，和 Remix 一致；jitter 却跟着头文件走，两个参数参照了不同的来源。
   - RR 关闭时 jitter 为 0，不受影响；开启时可能让生成帧边缘出现亚像素抖动。倾向于改成像素空间，需要目测对比确认。

2. **深度范围可能和 FG 的深度启发式不匹配（影响较小）。** 相机 near = 0.1，物体在 800~1400 单位外。按头文件的线性化公式 `1 / (1 - depth)`，整个场景都在默认的远近分界（600）之外。头文件建议深度范围被压缩时调整 `LinearizedDepth_Scale` / `NearFarPartition`，扩展没有暴露这两个参数。可能影响遮挡边缘。

## 延迟与节奏

3. **没有 Reflex。** NVIDIA 把 Reflex 作为 FG 的必需组件，Remix 也用 Reflex 标记包住插帧和呈现。我们没有任何延迟控制。

4. **VSync 受限时交换链会排队。** [DXSwapChain.cs](../../Zenith.NET.DirectX12/DXSwapChain.cs) 是 3 个后台缓冲，没有用 frame latency waitable object；[VKSwapChain.cs](../../Zenith.NET.Vulkan/VKSwapChain.cs) 是 FIFO + `MinImageCount + 1`。输出被 VSync 卡住时可能排队约 2 帧，多出约 2 个刷新周期的延迟。要解决需要 RHI 支持（waitable object / present wait）。

5. **节奏只靠 CPU 时间的启发式。** [FramePresenter.cs](Helpers/FramePresenter.cs) 的 `MeasureInterval` / `PresentSlot` 用 CPU 到达时间和交换链阻塞来推断显示间隔（1ms 阈值、EMA、超过 100ms 重置）。
   - Remix 用 FG 完成时的 GPU 时间戳排期；硬件支持时优先用 present metering（`VkSetPresentConfigNV`），CPU 节奏只是回退方案。MFG 3x/4x 时差距会更明显。
   - 开启 FG 或卡顿超过 100ms 后，头 1~2 组没有节奏（测到过 0.82ms 的间隔）。
   - GPU 繁忙时，呈现拷贝要和下一帧的路径追踪抢 GPU，启发式可能误判；这种情况没有测过。

6. **自旋等待占满一个核。** [FramePresenter.cs](Helpers/FramePresenter.cs) 的 `WaitUntil` 每帧大部分时间在 `Thread.Yield` 自旋。可以先粗粒度 sleep，最后约 1ms 再自旋。

## 吞吐与开销

7. **CPU 和 GPU 完全串行。** [FramePresenter.cs](Helpers/FramePresenter.cs) 的 `Slot.Capture` 每帧等 GPU 完成才返回，因为 CornellBox 的常量/顶点缓冲是单缓冲，DisableInterpolation 也要 CPU 回读。结果基础帧率是 1/(CPU+GPU)，不是 1/max(CPU, GPU)，抵消了一部分 FG 的收益。要解决需要多帧在途（环形缓冲）加异步回读，改动较大。

8. **额外拷贝和显存占用。** 每个输出帧要做 UI 合成、拷进 slot、再拷进交换链，共 3 次全屏读写；Remix 是把 FG 直接输出到交换链图像。4K 开 4x 时，生成纹理、合成输出和两组 slot 加起来约 0.5GB。

## 卡顿

9. **切换 FG 会重置 RR 历史。** [Renderer.cs](Renderer.cs) 的 `SetGeneratedFrameCount` 调用了 `ResetHistory()`，RR 的累积也被清空，切换时会闪一下噪点。

10. **FG feature 重建太多，而且是同步创建。**
    - 切倍数（2x→3x→4x）也会重建，但 `MultiFrameCount` 是 eval 参数，不需要重建（只有 50 系会遇到）。
    - [Renderer.cs](Renderer.cs) 的 `RecreateRenderResources` 在 RR 档位变化时也会重建 FG。
    - [DLSSFrameGeneration.cs](../../Extensions/Zenith.NET.Extensions.DLSS/DLSSFrameGeneration.cs) 构造里调用 `Submit().Wait()`，每次重建卡顿超过 100ms。
    - NGX 有 `DLSSG.AsyncCreateEnabled`（避免创建卡顿）和 `DLSSG.DynamicResolution`（MV/深度分辨率可变，RR 换档不用重建），扩展都没有暴露。

## 平台 / 覆盖

11. **MFG（3x/4x）从没在真机上跑过。** 4070 Ti SUPER 上 `MaxGeneratedFrameCount = 1`。索引 +1 和在同一个命令缓冲里连续 dispatch 的结构和 Remix 一致，但 `GeneratedFrameIndex > 0`、[CompositePass.cs](Passes/CompositePass.cs) 的多个常量槽、N ≥ 3 时的呈现节奏都没验证过。

12. **Vulkan 目前没法验证 FG。** 受已知驱动问题 [VK_EXT_descriptor_heap + ray query 导致 VK_ERROR_DEVICE_LOST](https://forums.developer.nvidia.com/t/vk-ext-descriptor-heap-ray-query-causes-vk-error-device-lost/376527/2) 影响。另外关掉验证层后不再 device lost，但 VSync（FIFO）下每秒呈现几千次，没有被刷新率限制，可能是同一个问题，等驱动修好再确认。

13. **Vulkan 队列别名竞争（AMD/Intel）。** 没有富余图形队列时，[VKGraphicsContext.cs](../../Zenith.NET.Vulkan/VKGraphicsContext.cs) 里的 PresentQueue 和 GraphicsQueue 是包装同一个 `VkQueue` 的两个对象，各有一把锁，`vkQueuePresentKHR` 也没加锁。渲染线程和呈现线程会并发访问同一个队列，违反 Vulkan 的外部同步要求。
    - NVIDIA 显卡有 16 个图形队列，不会触发；Linux 上的 AMD/Intel 会。
    - 旧呈现器就有这个问题，根因在 RHI。

## 小问题

14. 界面只显示呈现帧率（[App.cs](App.cs) 的 `DrawOverlay`），开 FG 后看不到基础帧率。

15. NGX 禁用插帧时，那一组生成帧的合成和拷贝已经做完了，等于白做；而且这组只呈现真实帧，瞬时帧率减半。

16. [DLSSFrameGenerationArgs.cs](../../Extensions/Zenith.NET.Extensions.DLSS/Structs/DLSSFrameGenerationArgs.cs) 的 `GeneratedFrameCount` 默认为 0，会原样传给 NGX；[DLSSCapabilities.cs](../../Extensions/Zenith.NET.Extensions.DLSS/DLSSCapabilities.cs) 的 `MaxGeneratedFrameCount` 为 0 表示 NGX 没设置，调用方要按 1 处理。这是最小封装的取舍，调用方需要知道这两点。

## 优先级建议

- 第 1 条先目测确认。
- 第 9、10 条改动小，可以先做。
- 第 3、4、5、7 条收益最大，但需要 RHI 支持或较大重构。
