---
title: Zenith.NET - Cross-Platform Graphics for .NET
description: A modern rendering hardware interface for DirectX 12, Metal 4, and Vulkan 1.4.
_layout: landing
---

<div class="landing-page">
    <section class="render-hero">
        <img class="render-hero-media" src="images/ray-tracing.png" alt="Ray-traced scene rendered with Zenith.NET">
        <div class="render-hero-scrim"></div>
        <div class="render-grid"></div>
        <div class="landing-shell render-hero-inner">
            <div class="hero-copy">
                <h1>One rendering model.<span>Every modern GPU.</span></h1>
                <p class="hero-lede">A precise, low-level .NET interface for graphics, compute, ray tracing, and mesh shading across DirectX 12, Metal 4, and Vulkan 1.4.</p>
                <div class="hero-actions">
                    <a class="landing-button landing-button-primary" href="https://www.nuget.org/packages/Zenith.NET">
                        Install Zenith.NET
                    </a>
                    <a class="landing-button landing-button-secondary" href="docs/index.md">
                        Read the docs
                    </a>
                </div>
                <div class="backend-support" aria-label="Supported graphics backends">
                    <span class="backend-label">BACKENDS</span>
                    <span class="backend-pill backend-directx">DirectX 12</span>
                    <span class="backend-pill backend-metal">Metal 4</span>
                    <span class="backend-pill backend-vulkan">Vulkan 1.4</span>
                </div>
            </div>
        </div>
    </section>
    <section class="features-section">
        <div class="landing-shell">
            <header class="section-heading">
                <span class="section-kicker">CORE FEATURES</span>
                <h2>Everything required for<span>modern GPU rendering</span></h2>
                <p>Keep explicit control of the hardware while sharing one precise, strongly typed programming model across every backend.</p>
            </header>
            <div class="feature-grid">
                <article class="feature-card">
                    <div class="feature-icon feature-icon-violet"><i class="bi bi-globe2" aria-hidden="true"></i></div>
                    <h3>Cross-platform rendering</h3>
                    <p>Target DirectX 12, Metal 4, and Vulkan 1.4 through one consistent, strongly typed .NET API.</p>
                </article>
                <article class="feature-card">
                    <div class="feature-icon feature-icon-pink"><i class="bi bi-cpu" aria-hidden="true"></i></div>
                    <h3>Explicit graphics API</h3>
                    <p>Record command buffers, manage queues, synchronize timelines, and control resource transitions directly.</p>
                </article>
                <article class="feature-card">
                    <div class="feature-icon feature-icon-blue"><i class="bi bi-lightning-charge" aria-hidden="true"></i></div>
                    <h3>Bindless by design</h3>
                    <p>Pass compact resource handles to Slang shaders without maintaining per-draw binding tables.</p>
                </article>
                <article class="feature-card">
                    <div class="feature-icon feature-icon-cyan"><i class="bi bi-bounding-box-circles" aria-hidden="true"></i></div>
                    <h3>Advanced workloads</h3>
                    <p>Build ray queries, mesh shaders, compute pipelines, and indirect workloads on the same command model.</p>
                </article>
                <article class="feature-card">
                    <div class="feature-icon feature-icon-orange"><i class="bi bi-boxes" aria-hidden="true"></i></div>
                    <h3>Native .NET performance</h3>
                    <p>Use spans, native memory, SIMD-friendly data, and deterministic disposal without a managed rendering layer.</p>
                </article>
                <article class="feature-card">
                    <div class="feature-icon feature-icon-green"><i class="bi bi-window-stack" aria-hidden="true"></i></div>
                    <h3>Extensible ecosystem</h3>
                    <p>Integrate rendering with Avalonia, MAUI, WinForms, WinUI, WPF, ImageSharp, ImGui, and Skia.</p>
                </article>
            </div>
        </div>
    </section>
    <section class="architecture-section">
        <div class="landing-shell architecture-grid">
            <div class="architecture-code" aria-label="Zenith.NET architecture example">
                <div class="architecture-code-bar">
                    <span class="architecture-code-title">RenderFrame.cs</span>
                    <button type="button" class="architecture-code-copy" aria-label="Copy code example" title="Copy code example">
                        <i class="bi bi-copy" aria-hidden="true"></i>
                    </button>
                </div>
                <pre><code class="language-csharp nohighlight" data-highlighted="yes"><span class="code-keyword">using</span> <span class="code-namespace">Zenith.NET</span>;
<span class="code-keyword">using</span> <span class="code-namespace">Zenith.NET.Vulkan</span>;
&#10;<span class="code-keyword">using</span> <span class="code-type">GraphicsContext</span> context = <span class="code-type">GraphicsContext</span>.<span class="code-method">CreateVulkan</span>(<span class="code-keyword">bool</span>: useValidationLayer);
&#10;<span class="code-comment">// Build portable shader state once.</span>
<span class="code-type">Shader</span> shader = <span class="code-method">LoadShader</span>(<span class="code-value">"RenderFrame.slang"</span>);
<span class="code-type">ComputePipeline</span> pipeline = context.<span class="code-method">CreateComputePipeline</span>(shader);
&#10;<span class="code-comment">// Rent transient commands for this frame.</span>
<span class="code-type">CommandBuffer</span> commands = context.GraphicsQueue.<span class="code-method">CommandBuffer</span>();
commands.<span class="code-method">SetPipeline</span>(pipeline);
&#10;<span class="code-comment">// Push bindless handles and frame constants directly.</span>
commands.<span class="code-method">PushConstants</span>(<span class="code-method">CreateFrameData</span>(output.Handle, frameIndex));
&#10;<span class="code-comment">// Record backend-neutral GPU work.</span>
commands.<span class="code-method">Dispatch</span>(threadCountX, threadCountY, <span class="code-number">1</span>);
&#10;<span class="code-comment">// ... pass bindless resources as compact handles ...</span>
<span class="code-comment">// ... mix graphics, compute, mesh, and ray-query work ...</span>
&#10;<span class="code-comment">// Submit explicitly and synchronize through the queue timeline.</span>
commands.<span class="code-method">Submit</span>().<span class="code-method">Wait</span>();</code></pre>
            </div>
            <div class="architecture-copy">
                <span class="section-kicker">ARCHITECTURE</span>
                <h2>A small API.<br><span>Complete GPU control.</span></h2>
                <p>Zenith.NET keeps the public surface deliberate and close to modern graphics hardware. Build portable renderers without hiding queues, synchronization, or resource state.</p>
                <div class="architecture-points">
                    <a href="docs/concepts/command-model.md"><i class="bi bi-check-circle" aria-hidden="true"></i><span><strong>Low-overhead command submission</strong><small>Record work explicitly and synchronize with timeline values.</small></span></a>
                    <a href="docs/concepts/resource-binding.md"><i class="bi bi-check-circle" aria-hidden="true"></i><span><strong>Bindless resource management</strong><small>Use compact handles across graphics and compute workloads.</small></span></a>
                    <a href="docs/platform/backend-selection.md"><i class="bi bi-check-circle" aria-hidden="true"></i><span><strong>Backend control without divergence</strong><small>Select the native API while keeping one renderer architecture.</small></span></a>
                </div>
            </div>
        </div>
    </section>
    <section class="install-section">
        <div class="landing-shell install-inner">
            <header class="install-heading">
                <span class="section-kicker">QUICK START</span>
                <h2>One command.<span>Start rendering.</span></h2>
                <p>Add a backend package from NuGet, then follow the first-renderer tutorial.</p>
            </header>
            <div class="install-command">
                <code><span>$</span> dotnet add package <strong>Zenith.NET.Vulkan</strong></code>
                <button type="button" class="install-copy" aria-label="Copy install command" title="Copy install command" data-copy-command="dotnet add package Zenith.NET.Vulkan"><i class="bi bi-copy" aria-hidden="true"></i></button>
            </div>
            <p class="install-note">Continue with <a href="tutorials/getting-started/prerequisites.md">Getting started</a> or browse the <a href="api/index.md">API reference</a>.</p>
        </div>
    </section>
    <section class="resources-section">
        <div class="landing-shell">
            <header class="resources-heading">
                <span class="section-kicker">RESOURCES</span>
                <h2>Everything you need to build.</h2>
            </header>
            <div class="resource-grid">
                <a class="resource-card resource-card-docs" href="docs/index.md">
                    <span class="resource-icon"><i class="bi bi-book" aria-hidden="true"></i></span>
                    <strong class="resource-title">Documentation</strong>
                    <p>Concepts, backend guidance, workload details, and practical integration notes.</p>
                    <strong>Browse docs</strong>
                </a>
                <a class="resource-card resource-card-nuget" href="https://www.nuget.org/packages/Zenith.NET">
                    <span class="resource-icon"><i class="bi bi-download" aria-hidden="true"></i></span>
                    <strong class="resource-title">NuGet packages</strong>
                    <p>Install the core API and native backend packages directly into your .NET project.</p>
                    <strong>View packages</strong>
                </a>
                <a class="resource-card resource-card-community" href="https://github.com/qian-o/Zenith.NET">
                    <span class="resource-icon"><i class="bi bi-chat-square" aria-hidden="true"></i></span>
                    <strong class="resource-title">Open source</strong>
                    <p>Report issues, inspect backend implementations, and contribute on GitHub.</p>
                    <strong>Visit GitHub</strong>
                </a>
            </div>
        </div>
    </section>
    <footer class="landing-footer">
        <div class="landing-shell landing-footer-grid">
            <div class="landing-footer-brand">
                <img class="landing-footer-logo" src="images/Zenith.NET-Logo.svg" alt="Zenith.NET">
                <p>A high-performance rendering hardware interface for modern .NET applications.</p>
                <a class="landing-footer-github" href="https://github.com/qian-o/Zenith.NET" aria-label="Zenith.NET on GitHub"><i class="bi bi-github" aria-hidden="true"></i></a>
            </div>
            <div class="landing-footer-column">
                <strong class="landing-footer-heading">Explore</strong>
                <a href="docs/index.md">Core concepts</a>
                <a href="docs/features/graphics.md">Graphics workloads</a>
                <a href="api/index.md">API reference</a>
                <a href="tutorials/index.md">Tutorials</a>
            </div>
            <div class="landing-footer-column">
                <strong class="landing-footer-heading">Project</strong>
                <a href="https://www.nuget.org/packages/Zenith.NET">NuGet</a>
                <a href="https://github.com/qian-o/Zenith.NET">Source code</a>
                <a href="https://github.com/qian-o/Zenith.NET/issues">Issues</a>
                <a href="https://github.com/qian-o/Zenith.NET/pulls">Pull requests</a>
            </div>
        </div>
        <div class="landing-shell landing-footer-bottom"><span>© 2026 Zenith.NET. MIT License.</span><span><a href="https://github.com/qian-o/Zenith.NET">GitHub</a><a href="docs/index.md">Docs</a></span></div>
    </footer>
</div>
