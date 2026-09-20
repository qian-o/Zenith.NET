# 繁體中文翻譯約定

## 資源與結構

英文是唯一原文。翻譯時只將 `locales/en-US/strings.yml` 的鍵值複製到本語言的 `strings.yml`。開始翻譯前只保留本檔案；`translation.md` 永久保留且不發布。保留首行 `### YamlMime:Resources`。

導覽、章節順序、錨點、連結、格式、程式碼和圖片都在 `locales/` 外統一維護。不要在語言目錄內新增文章、TOC、範本、指令碼或設定。

鍵名表達穩定的用途與概念。修改措辭時不改鍵；新增鍵不要綁定特定句子、顯示位置、編號或語言。只翻譯英文值，使用自然、簡潔的臺灣繁體中文，保留原意、前提、否定、單位與生命週期限制。

資源值是純文字，不解析 HTML 或 Markdown。`{context}`、`{link}` 等具名預留位置引用共用頁面中的程式碼、連結或強調內容。可以依中文語序調整順序，但必須保留全部名稱，不新增或刪除。必須包含英文的全部鍵；缺鍵、多餘舊鍵或預留位置不符都會使建置失敗。

沿用既有 DocFX 命令。補齊資源檔案後自動啟用語言，無須修改設定。各語言共用頁面路徑，以 `?lang=zh-TW` 選擇語言；語言清單一律依目錄代碼排序。

```sh
docfx documents/docfx.json --warningsAsErrors
docfx serve documents/_site --hostname 127.0.0.1 --port 8080
```

## 術語

| 英文 | 約定用語 |
| --- | --- |
| API / GPU / RHI | 保留縮寫，首次依原文說明 |
| barrier | 屏障 |
| bindless | 無綁定（bindless） |
| buffer | 緩衝區；`Buffer` 不變 |
| color attachment | 色彩附件 |
| command buffer | 命令緩衝區 |
| command queue | 命令佇列 |
| command recording | 命令記錄 |
| depth/stencil | 深度／樣板 |
| drawable | 目前可呈現的影像；`Drawable` 不變 |
| framebuffer | 畫面緩衝區，尺寸依原文的像素單位表述 |
| graphics context | 圖形上下文 |
| heap | 堆積 |
| pipeline | 管線；依情境區分圖形或計算管線 |
| rasterization | 光柵化 |
| readback | 從 GPU 回讀資料；下載檔案另用「下載」 |
| render pass | 渲染通道（render pass） |
| render target | 渲染目標 |
| resource | 資源 |
| resource ownership / lifetime | 資源所有權／生命週期 |
| resource usage | 資源用途 |
| shader / entry point | 著色器／進入點 |
| submit / submission | 提交／提交操作，與 GPU 執行區分 |
| surface | 呈現情境使用「原生呈現目標」；`Surface` 不變 |
| swap chain / presentation | 交換鏈／呈現 |
| texture | 紋理 |
| timeline | 時間線 |
| undefined | 未定義，不能改寫為某個預設結果 |
| upload | 上傳資料 |
| vertex / fragment | 頂點／片元 |
| view | 資源檢視；與 UI 檢視區分 |
| viewport / scissor | 視口／裁剪區域 |
