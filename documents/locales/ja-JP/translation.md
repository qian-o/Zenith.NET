# 日本語翻訳ガイド

## リソースと構成

英語を唯一の原文とします。`locales/en-US/strings.yml` のキーと値だけを各言語の `strings.yml` にコピーします。翻訳を始めるまでは、この永続的な `translation.md` だけを残し、サイトには公開しません。先頭の `### YamlMime:Resources` は維持してください。

ナビゲーション、章の順序、アンカー、リンク、書式、コード、画像は `locales/` の外で共有します。このディレクトリに記事、TOC、テンプレート、スクリプト、設定を追加しません。

キーは安定した役割や概念を表します。文言を調整してもキーを改名せず、文言そのもの、表示位置、番号、言語に依存する名前は避けます。英語の値だけを簡潔で自然な「です・ます」調の日本語に翻訳します。

値はプレーンテキストであり、HTML や Markdown ではありません。`{context}` や `{link}` のような名前付きプレースホルダーには、共通のコードやリンクが挿入されます。順序は変更できますが、すべての名前を保持し、追加・削除はしません。英語の全キーが必要です。キーの欠落やプレースホルダーの不一致はビルドエラーになります。

既存の DocFX コマンドを使います。完全な辞書ファイルを追加すると言語が自動的に有効になります。全言語でページのパスを共有し、`?lang=ja-JP` で言語を選択します。言語一覧はディレクトリコード順です。

```sh
docfx documents/docfx.json --warningsAsErrors
docfx serve documents/_site --hostname 127.0.0.1 --port 8080
```

## 用語

| 英語 | 使用する表現 |
| --- | --- |
| API / GPU / RHI | 略語を維持し、原文に従って説明する |
| barrier | バリア |
| buffer | バッファー。`Buffer` は変更しない |
| command buffer | コマンドバッファー |
| command queue | コマンドキュー |
| drawable | 現在の表示対象画像。`Drawable` は変更しない |
| graphics context | グラフィックスコンテキスト |
| pipeline | パイプライン |
| readback | リードバック |
| render pass | レンダーパス |
| resource | リソース |
| shader | シェーダー |
| swap chain | スワップチェーン |
| texture | テクスチャ |
| timeline | タイムライン |
| upload | アップロード |
