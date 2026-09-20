# Documentation authoring and maintenance

This is the handoff reference for filling in the documentation. It is excluded from the published site and its navigation.

## Architecture and theme baseline

The DocFX framework, content paths, navigation and single dark theme are the accepted baseline. Maintain content within the existing pages. The tutorial, samples index and concept articles are authored content; the API reference is generated from source.

Keep the existing filenames, routes and `toc.yml` hierarchy. Retain the original brand assets and the shared templates. Use the existing Markdown components instead of introducing page-specific CSS, scripts, controls or a second theme. A reproducible rendering or accessibility defect can receive a focused fix; new visual directions and navigation changes are separate work.

The brand footer belongs only to the landing page. Learn and API pages end with their content, keeping the reading layout and sidebars continuous to the bottom.

Keep scratch pages, validation scripts and preview fixtures outside the repository. Do not recreate `documents/tests`, publish test content, or reintroduce the temporary `Buffer` XML comments. Do not edit generated `documents/api/*.yml` or `documents/_site` files.

## Page responsibilities

Shared page paths below are relative to `documents/`. English text lives exclusively in `locales/en-US/strings.yml`. A Markdown filename determines its corresponding `.html` route.

| Page | Responsibility |
| --- | --- |
| `index.md` | The shared homepage composition, bound to `home.*` text resources. |
| `learn/index.md` | Orient readers and link to the tutorial, samples and concepts. Keep the existing card structure. |
| `learn/first-triangle.md` | One complete, runnable triangle tutorial, from an empty project to the first rendered frame. |
| `learn/samples.md` | Curated links to actual sample source, with a short explanation of what each demonstrates and any prerequisites. |
| `learn/concepts/execution-model.md` | Contexts, queues, command recording, submission and ownership. |
| `learn/concepts/resource-management.md` | Resource creation, usages, memory placement, heaps, views and lifetime. |
| `learn/concepts/shader-data-and-binding.md` | Slang shaders, data layout and resource handles/binding. |
| `learn/concepts/synchronization.md` | Execution order, resource visibility, barriers, timelines and CPU/GPU coordination. |
| `learn/concepts/platform-integration.md` | Surfaces, presentation, resize/lifecycle and integration with supported UI frameworks. |
| `api/index.md` and generated reference pages | Look up namespaces, declarations and members. API behavior comes from source and any intentional source XML comments. |

Do not turn the samples index into a sequence of advanced tutorials. The triangle can have ordered sections within its single page; other examples remain independent. Concepts explain decisions and constraints, linking to the tutorial, samples or API instead of duplicating them.

## Content rules

- Maintain the canonical prose in American English in `documents/locales/en-US/strings.yml`. Other `documents/locales/<language>/` directories hold translated dictionaries and follow that directory's permanent `translation.md`. Keep C# identifiers and namespace names exact.
- Describe the current API directly. Avoid release histories, migration narratives, product-version badges, "new in" labels and temporary preview-package instructions. A new package release alone does not require a documentation edit when the documented behavior remains accurate.
- Omit package versions and `--prerelease` from installation commands. Necessary platform/SDK prerequisites and target frameworks may be stated when needed to run the example; these are environment requirements, not a product-version narrative.
- Request validation by default with `useValidationLayer: true` in documented context creation. Runnable tutorials should connect `ValidationMessage` to console output so reported diagnostics are visible.
- Verify behavior against `sources/` and runnable examples before writing. Read the implementation when ownership, synchronization or platform support is unclear; do not infer it from a type's name. If package behavior and repository source differ, resolve the mismatch before calling an example runnable.
- Introduce unfamiliar graphics terms when they first matter. Distinguish descriptions from recorded commands, and recording from GPU execution. Explain prerequisites and insertion points before asking the reader to use them.
- Start each article with its practical purpose. Explain the reason for a non-obvious choice close to the relevant code. Avoid restating signatures, generic graphics introductions, decorative statistics and repeated navigation instructions.
- Preserve source declaration order when listing enum members or writing combinations of flags.
- Introduce enum types through the meaning and scope of their members. Use concise explanations or tables; do not turn an enum introduction into a resource-construction walkthrough.
- Use one H1 and a clear H2/H3 hierarchy. Give explicit anchors to sections linked from other pages when their headings may change. Use the existing shared Markdown/HTML components with resource-bound text; use tables for comparisons, notices for necessary constraints and diagrams only when they clarify a relationship.
- Keep diagrams small enough to read on a phone. Prefer a vertical sequence to a wide horizontal graph when scaling would make its labels too small.
- Replace `<!-- Content pending. -->` when a page is filled. Check the existing homepage, Learn overview and README for placeholder wording that has become inaccurate; update that wording without redesigning those pages.

## Triangle and sample workflow

First locate and inspect the existing tutorial/sample source. This checkout has examples under `sources/Experiments/`; it does not contain a dedicated triangle tutorial project. Locate any separately maintained tutorial repository before depending on it, and verify each linked file and API. Do not invent repository paths or treat an experiment as the canonical triangle tutorial without inspecting it.

The first triangle is a general .NET console application using NuGet packages. Show installation commands without package versions. Do not replace package installation with source project references or cloning the main repository to accommodate the validation machine. Keep any explicitly authorized test-only package selection in a temporary project outside the repository.

Introduce the windowing library, backend selection and native-surface differences before graphics setup. Use one shared application flow for Windows, macOS and Linux, identify the windowing requirements, and keep platform interop in a small, verified support file where possible. Do not turn this into a platform-specific SDK setup tutorial.

Teach the triangle in cumulative steps: an empty window, a presented clear color, vertex data and upload, shaders, input layout, pipeline state, drawing, presentation and cleanup. Explain what each step contributes, why its settings are chosen and how its data connects to the next stage. Mark the exact file and insertion or replacement point for every partial snippet. Provide observable checkpoints, and ensure the assembled result is complete. Link to concepts for deeper decisions; do not substitute a finished-project code dump or a validation report for the reader's learning sequence.

Follow the established tutorial source's code style: explicit control flow, target-typed initialization, and its blank lines between logical operations and fields. Do not compress branches into nested conditional expressions, manually wrap method calls to fit the page, or introduce constants and helper functions solely to reorganize the article. Long code lines scroll within the existing code blocks.

Introduce file settings when the file is created. For a shader, describe its Copy to Output Directory setting after writing the shader; do not front-load project XML for files the reader has not created. Prefer a directly linked support filename with a short purpose statement over raw-file download instructions.

Keep snippets consistent with the complete application. The assembled source must not depend on omitted setup or unexplained helper methods. Keep platform-specific alternatives only where they are necessary to run it.

For each sample, provide a verified source link, its purpose and relevant requirements. Avoid copying its implementation into the page. Use the repository's normal branch links rather than transient local paths or release-specific URLs unless the example requires otherwise.

Compile the complete tutorial using its documented setup and run it to verify the visible triangle on the selected platform. A DocFX build validates the documentation, not the C# or shader examples. Report any platform that could not be exercised; do not claim cross-platform execution from a single-platform check.

## Inline references

Use DocFX UIDs so links follow the generated API reference:

```markdown
Create a [`GraphicsContext`](xref:Zenith.NET.GraphicsContext).
```

Plain backticks mark code without adding a link. Specify a UID when a reference is intended; identical names alone do not establish which symbol they refer to.

## Linked code blocks

Ordinary fenced code blocks retain their usual syntax highlighting and copy behavior. When a block needs clickable symbols, use a native HTML code block with DocFX `xref` elements:

```html
<pre><code class="lang-csharp"><xref uid="Zenith.NET.GraphicsContext" text="GraphicsContext"/> context = CreateContext();
<xref uid="Zenith.NET.Buffer" text="Zenith.NET.Buffer"/> buffer = context.<xref uid="Zenith.NET.GraphicsContext.CreateBuffer(Zenith.NET.BufferDesc)" text="CreateBuffer"/>(description);</code></pre>
```

The `text` attribute is the exact code to display and copy. Escape HTML characters such as `<`, `>` and `&` in the code text and attribute values. A reference can cover a qualified name or a complete generic type expression.

DocFX resolves the UID during the build. The shared code-link renderer preserves the reference through highlighting; copying returns only the displayed code. The same renderer handles generated API declarations. Comments, strings, parameter names and tuple element names do not acquire links by matching another symbol's spelling.

Use `csharp` for C#, `slang` for shaders and `sh` for shell commands. The theme supplements C# highlighting for conventional type identifiers, calls, member access and operators, and provides Slang-specific types and shader semantics. This is lexical coloring, not compiler symbol resolution. A colored token is only a link when it has an explicit reference or falls within the generated API link scope below; code text and copy output stay unchanged.

## Automatic link scope

Generated API declarations link **types with a reference page on this site** in these positions:

- Field, property and event types, and method/operator return types.
- Method, constructor and indexer parameter types.
- Base classes and implemented interfaces in a type declaration.

This includes local types inside generic, nullable, array, pointer and tuple expressions. The declared member's own name, parameter names, generic parameter names, tuple element names, attributes, generic constraints, modifiers and literal values remain text. Framework and third-party types without a local API page also remain text; automatic references do not lead to external documentation.

Tutorial code and XML code examples do not acquire guessed links. Authored Markdown/HTML `xref` and XML prose `see` references are explicit and may point to local or external documentation. Only rendered links receive the code-reference color, pointer cursor and focus treatment.

## XML comments

Write a short `<summary>` describing behavior. Add `<param>`, `<typeparam>`, `<returns>` and `<value>` when they explain something beyond the declaration. These sections are displayed only when their descriptions contain content. Use `<remarks>` for constraints, `<exception>` for failure conditions and `<example>` for usage.

Use `<see cref="..."/>` for references in prose, `<paramref>` and `<typeparamref>` for names, and `<c>` for literal inline code. The compiler resolves `cref` in the source's context.

```csharp
/// <summary>Creates a buffer using <see cref="BufferDesc"/>.</summary>
/// <param name="description">The size, usage and memory requirements.</param>
/// <returns>The newly created buffer.</returns>
/// <remarks>The caller owns the returned resource.</remarks>
/// <example>
/// <para>See <see cref="GraphicsContext.CreateBuffer(BufferDesc)"/>.</para>
/// <code language="csharp"><![CDATA[
/// BufferDesc description = default;
/// // Supply the application's resource requirements here.
/// var buffer = context.CreateBuffer(description);
/// ]]></code>
/// </example>
```

Keep XML `<code>` contents literal using `CDATA`. Put `<see>` references in the surrounding prose: DocFX extracts the text of XML code examples and discards nested reference elements, which can remove the referenced identifier. The HTML `xref` form above applies to authored Markdown pages.

## Resource dictionaries and shared pages

This is a WPF-style resource model. `index.md`, `learn/` and `api/index.md` contain the single shared page structure, anchors, links, illustrations and code. `toc.yml` is the only hand-maintained navigation tree. The API TOC is generated from C# metadata; it is not another authored navigation or a language-specific copy.

`locales/en-US/strings.yml` is the sole English source dictionary. Other language folders contain only their permanent `translation.md` until translation begins. A translator adds just `strings.yml`, keeping the `### YamlMime:Resources` header and the complete source key set. No localized pages, TOCs, images, templates, scripts or configuration are needed. All resource values are strings.

Language folders, registrations and menus use code order: `de-DE`, `en-US`, `es-ES`, `fr-FR`, `ja-JP`, `ko-KR`, `pt-BR`, `ru-RU`, `zh-CN`, `zh-TW`. English is explicitly the source and fallback. Complete resource dictionaries become available automatically after the normal DocFX build. Missing keys, stale extra keys, non-string values and changed placeholder names fail that build.

Pages share the same URL paths. `?lang=<code>` selects a dictionary, retaining the page and fragment. Without an explicit language, the site uses a saved manual selection, browser preferences, then English. An explicit unsupported or unpublished language falls back to English, without substituting a different saved language. A missing, stale or malformed downloaded dictionary is rejected before any translated text is applied, keeping the complete English page usable. The English HTML is fully rendered during the build; other static dictionaries bind to that same markup in the browser. No translation API is used. Search uses translated resources with the shared generated API index.

All images remain in `images/`; all generated API YAML remains in `api/`. This is a complete refactor, with no legacy route or compatibility layer. Do not create `pages/`, `snippets/`, a parallel content graph or an additional build command for localization.

## Resource keys and placeholders

Keys name stable roles or concepts, such as `tutorial.context.description`, `concepts.resources.memory.description` or `ui.search.title`. Do not encode current wording, article numbers, screen positions, language codes or release versions. A copy edit changes the value, not the key. Give different meanings separate keys even if their current English values happen to match.

Values are plain text, not HTML or Markdown. Keep a complete sentence or paragraph together. Inline code, links and emphasis live in named slots in the shared page, so translations can move those slots without copying their markup or changing their destinations.

For example, in the dictionary:

```yaml
tutorial.context.description: A {context} provides the graphics connection.
```

And in the shared page:

```html
<p><resource key="tutorial.context.description"><slot name="context"><xref uid="Zenith.NET.GraphicsContext" text="GraphicsContext"/></slot></resource></p>
```

The binding escapes text values and inserts only the shared slot markup. Preserve every placeholder name, including case, when translating. Reordering is allowed; adding or removing names is not. Use descriptive slot names for new content. Nested emphasis or link labels can have their own resources when they contain translatable prose. Executable examples, shader code and diagnostic/illustrative code blocks stay in shared Markdown unchanged.

Page front matter uses `title: '@tutorial.title'` and an optional resource-bound `description`. Navigation names bind keys through `name: '@tutorial.title'` in the root TOC. Keep heading IDs in shared markup so rewording or translating a title cannot change links.

The binding is implemented inside the existing template system: one native resource schema loads flat dictionaries, a small shared formatter handles named placeholders, and the conceptual template resolves English text at build time. The browser uses the same placeholder format. Do not add a general Markdown interpreter, external translation service or content-generation pipeline.

## Validation

After changing XML comments, run the full extraction and build from the repository root:

```sh
docfx documents/docfx.json --warningsAsErrors
```

For Markdown or theme-only changes, run `docfx build documents/docfx.json --warningsAsErrors`.

Preview the generated site from the repository root:

```sh
docfx serve documents/_site --hostname 127.0.0.1 --port 8080
```

Check the resulting type page and a tutorial page in the browser. Verify links after highlighting, keyboard focus, copied code, long generic signatures and narrow layouts. A documented member should keep its summary, parameter descriptions, return/value explanation, remarks, examples and exceptions. An undocumented member should remain compact.

Before handing off a content change, ensure the DocFX build has no warnings or errors, local links and fragments resolve, and the filled pages work at desktop and phone widths. Check headings, code/table overflow, search results and keyboard navigation on the changed pages. Summarize the pages filled, the source used, compilation/rendering checks performed and any unfinished content.

## Template organization

The template keeps DocFX's existing entry points and the `layout`, `partials` and `public` directories.

- `layout/_master.tmpl` assembles the fixed dark page shell from the `site.*` partials. The header uses `primary-navigation` and the API browser uses `site:api-toc`, avoiding DocFX’s automatic navigation and theme controls.
- `ManagedReference.html.primary.tmpl` assembles API pages and their shared remarks, examples and related links. The `api.*` partials render each content section; `api.code` keeps declarations and their reference metadata together. `ManagedReference.extension.js` prepares names, descriptions and unique member anchors.
- `public/main.css` is the stylesheet entry. `theme.css` defines the palette and typography; `layout.css` owns the shell and navigation; `search.css` owns both search dialogs; `content.css` owns code, tables and prose links. Home and motion styles remain separate. `api.css` is loaded only for API pages. Reuse semantic theme colors instead of adding slightly different control and text colors.
- `public/main.js` is DocFX's extension entry. `site.js` owns mobile header interactions and code-block labels; `dialog.js` shares keyboard and backdrop behavior between the search dialogs.
- `languages.js` selects a resource dictionary and owns the language menu. `navigation.js` renders the header and Learn sidebar from the same root TOC. `search.js` owns page-index loading and search recovery. `resources.js` validates complete page bindings before applying translated text, while `resource-format.js` shares placeholder parsing with the build host. `resources.common.js` and the conceptual extension bind and validate the English resource values; `Resources.json.primary.js` publishes the dictionaries through DocFX's native schema processor.
- `outline.js` builds the article's nested heading navigation and tracks the current section. `syntax.js` configures the bundled highlighter for documentation code; it does not add links.
- `code-links.js` preserves authored references through highlighting. `signature-links.js` supplies the bounded, metadata-driven references for API declarations.

Keep the generated `_site` and API metadata out of source edits. Rebuild after template changes, and check existing page URLs, fragment links and copy behavior before publishing.

## Framework maintenance boundary

The resource-dictionary model and current theme are the framework baseline. New translations add only a complete `strings.yml`; ordinary copy changes edit values without renaming keys. Keep the single authored TOC, shared pages, generated API, and common images. Do not recreate compatibility routes, language-specific pages, a second navigation tree, a separate build command, or a repository test directory.

For a framework change, check the homepage, Learn overview, a long article, a namespace, a type/member page and an enum. Check a wide viewport and a 320-pixel viewport, keyboard focus, menu exclusivity, search return/retry behavior, and member navigation. Translation fixtures must remain outside the repository and cover successful switching, explicit-language priority, unchanged fragments, and invalid dictionary fallback. Retain the original English text and code unless the task explicitly includes content edits.
