# 简体中文翻译约定

## 资源与结构

英文是唯一原文。翻译时只将 `locales/en-US/strings.yml` 的键值复制到本语言的 `strings.yml`。开始翻译前只保留本文件；`translation.md` 永久保留且不发布。保留首行 `### YamlMime:Resources`。

导航、章节顺序、锚点、链接、格式、代码和图片都在 `locales/` 外统一维护。不要在语言目录内新增文章、TOC、模板、脚本或配置。

键名表达稳定的用途与概念。修改措辞时不改键；新增键不要绑定具体句子、显示位置、编号或语言。只翻译英文值，使用自然、简洁的技术中文，保留原意、前提、否定、单位和生命周期约束。

资源值是纯文本，不解析 HTML 或 Markdown。`{context}`、`{link}` 等命名占位符引用公共页中的代码、链接或强调内容。可以按中文语序调整占位符顺序，但必须保留全部名称，不新增或删除占位符。必须包含英文的全部键；缺键、额外旧键或占位符不匹配都会导致构建失败。

沿用原有 DocFX 命令。补齐资源文件后自动启用语言，无需修改配置。各语言共用页面路径，通过 `?lang=zh-CN` 选择语言；语言列表始终按目录代码排序。

```sh
docfx documents/docfx.json --warningsAsErrors
docfx serve documents/_site --hostname 127.0.0.1 --port 8080
```

## 术语

| 英文 | 约定译法或处理 |
| --- | --- |
| rendering hardware interface / RHI | 渲染硬件接口（RHI）；之后可直接使用 RHI |
| graphics context | 图形上下文 |
| command queue | 命令队列 |
| command buffer | 命令缓冲区 |
| command recording | 命令录制 |
| submit / submission | 提交／提交操作；与 GPU 执行区分 |
| resource | 资源 |
| resource usage | 资源用途 |
| resource ownership / lifetime | 资源所有权／生命周期 |
| buffer | 缓冲区；标识符 `Buffer` 保持原样 |
| texture | 纹理 |
| heap | 堆 |
| view | 视图；区分资源视图与 UI 控件 |
| render pass | 渲染通道（render pass） |
| render target | 渲染目标 |
| color attachment | 颜色附件 |
| depth/stencil | 深度／模板 |
| pipeline | 管线；按上下文使用图形管线或计算管线 |
| shader / entry point | 着色器／入口点 |
| vertex / fragment | 顶点／片元 |
| rasterization | 光栅化 |
| viewport / scissor | 视口／裁剪区域 |
| swap chain / presentation | 交换链／呈现 |
| surface（呈现语境） | 原生呈现目标；`Surface` 标识符不译 |
| drawable | 当前可绘制图像；`Drawable` 标识符不译，按原文说明其呈现关系 |
| framebuffer | 帧缓冲区；尺寸以原文规定的像素单位表述 |
| barrier / timeline | 屏障／时间线 |
| bindless | 首次写“无绑定（bindless）”，保留 API 标识符 |
| upload | 上传 |
| download / readback | GPU 数据语境使用“回读”；下载文件时使用“下载” |
| undefined | 未定义；不能改写为某个具体默认结果 |
