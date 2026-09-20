# Writing linked documentation

This is a maintainer reference. It is not included in the site's navigation.

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

## Validation

After changing XML comments, run the full extraction and build from the repository root:

```sh
docfx documents/docfx.json --warningsAsErrors
```

For Markdown or theme-only changes, run `docfx build documents/docfx.json --warningsAsErrors`.

Check the resulting type page and a tutorial page in the browser. Verify links after highlighting, keyboard focus, copied code, long generic signatures and narrow layouts. A documented member should keep its summary, parameter descriptions, return/value explanation, remarks, examples and exceptions. An undocumented member should remain compact.

## Template organization

The template keeps DocFX's existing entry points and the `layout`, `partials` and `public` directories.

- `layout/_master.tmpl` assembles the page shell from the `site.*` partials.
- `api.*` partials render generated reference data. `ManagedReference.extension.js` prepares names, descriptions and unique member anchors.
- `public/main.css` is the stylesheet entry. Theme, layout, search, content, home and motion styles have separate files; `api.css` is loaded only on reference pages.
- `public/main.js` is DocFX's extension entry. `site.js` owns navigation and content setup; `dialog.js` shares keyboard and backdrop behavior between the search dialogs.
- `code-links.js` preserves authored references through highlighting. `signature-links.js` supplies the bounded, metadata-driven references for API declarations.

Keep the generated `_site` and API metadata out of source edits. Rebuild after template changes, and check existing page URLs, fragment links and copy behavior before publishing.
