---
title: Zenith.NET - Explicit GPU Programming for .NET
description: A low-level, cross-platform rendering hardware interface with bindless resources and explicit synchronization for DirectX 12, Metal 4, and Vulkan 1.4.
_layout: landing
---

<div class="landing-page">
    <section class="render-hero">
        <img class="render-hero-media" src="images/ray-tracing.png" alt="Ray-traced scene rendered with Zenith.NET">
        <div class="render-hero-scrim"></div>
        <div class="render-grid"></div>
        <div class="landing-shell render-hero-inner">
            <div class="hero-copy">
                <h1>Explicit GPU control.<span>One .NET RHI.</span></h1>
                <p class="hero-lede">Build portable graphics and compute workloads without giving up command queues, resource states, bindless access, or timeline synchronization.</p>
                <div class="hero-actions">
                    <a class="landing-button landing-button-primary" href="https://www.nuget.org/packages/Zenith.NET">
                        Install Zenith.NET
                    </a>
                    <a class="landing-button landing-button-secondary" href="docs/index.md">
                        Read the docs
                    </a>
                </div>
                <div class="backend-support" aria-label="Supported graphics APIs">
                    <span class="backend-label">GRAPHICS APIs</span>
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
                <h2>One explicit model for<span>modern GPU workloads</span></h2>
                <p>Zenith.NET exposes the common low-level model of DirectX 12, Metal 4, and Vulkan 1.4, keeping renderer architecture portable while GPU work remains explicit.</p>
            </header>
            <div class="feature-grid">
                <article class="feature-card">
                    <div class="feature-icon feature-icon-violet"><i class="bi bi-globe2" aria-hidden="true"></i></div>
                    <h3>Cross-platform RHI</h3>
                    <p>Use one strongly typed RHI across DirectX 12, Metal 4, and Vulkan 1.4 while selecting the graphics API at startup.</p>
                </article>
                <article class="feature-card">
                    <div class="feature-icon feature-icon-pink"><i class="bi bi-cpu" aria-hidden="true"></i></div>
                    <h3>Explicit command model</h3>
                    <p>Record work on graphics, compute, and transfer queues, then coordinate completion through timeline values.</p>
                </article>
                <article class="feature-card">
                    <div class="feature-icon feature-icon-blue"><i class="bi bi-lightning-charge" aria-hidden="true"></i></div>
                    <h3>Bindless resource access</h3>
                    <p>Carry resource handles in constant data and resolve them in Slang shaders with <code>DescriptorHandle&lt;T&gt;</code>.</p>
                </article>
                <article class="feature-card">
                    <div class="feature-icon feature-icon-cyan"><i class="bi bi-bounding-box-circles" aria-hidden="true"></i></div>
                    <h3>Modern pipeline coverage</h3>
                    <p>Combine rasterization, compute, indirect commands, RayQuery, and mesh shading when the device supports them.</p>
                </article>
                <article class="feature-card">
                    <div class="feature-icon feature-icon-orange"><i class="bi bi-boxes" aria-hidden="true"></i></div>
                    <h3>Predictable data and lifetime</h3>
                    <p>Control memory residency, transfer data with spans, define unmanaged GPU data, and release resources deterministically.</p>
                </article>
                <article class="feature-card">
                    <div class="feature-icon feature-icon-green"><i class="bi bi-window-stack" aria-hidden="true"></i></div>
                    <h3>Application integrations</h3>
                    <p>Connect the RHI to .NET UI frameworks, ImageSharp, ImGui, and Skia through focused integration packages.</p>
                </article>
            </div>
        </div>
    </section>
    <section class="architecture-section">
        <div class="landing-shell architecture-grid">
            <div class="architecture-code" aria-label="Zenith.NET architecture example">
                <div class="architecture-code-bar">
                    <span class="architecture-code-title">FrameCommands.cs</span>
                    <button type="button" class="architecture-code-copy" aria-label="Copy code example" title="Copy code example">
                        <i class="bi bi-copy" aria-hidden="true"></i>
                    </button>
                </div>
                <pre><code class="language-csharp nohighlight" data-highlighted="yes"><span class="code-keyword">using</span> <span class="code-namespace">Zenith.NET</span>;
<span class="code-keyword">using</span> <span class="code-namespace">Zenith.NET.Vulkan</span>;
&#10;<span class="code-keyword">using</span> <span class="code-type">GraphicsContext</span> context = <span class="code-type">GraphicsContext</span>.<span class="code-method">CreateVulkan</span>(useValidationLayer: <span class="code-keyword">true</span>);
&#10;<span class="code-type">CommandBuffer</span> commands = context.ComputeQueue.<span class="code-method">CommandBuffer</span>();
&#10;<span class="code-comment">// Prepare the output texture for storage writes.</span>
commands.<span class="code-method">Transition</span>(output, <span class="code-keyword">default</span>, <span class="code-type">TextureLayout</span>.Storage);
&#10;<span class="code-comment">// Bind compute state and dispatch work.</span>
commands.<span class="code-method">SetPipeline</span>(computePipeline);
commands.<span class="code-method">SetConstantBuffer</span>(constantBuffer, <span class="code-number">0</span>);
commands.<span class="code-method">Dispatch</span>(groupCountX, groupCountY, <span class="code-number">1</span>);
&#10;<span class="code-comment">// Prepare the output texture for sampled reads.</span>
commands.<span class="code-method">Transition</span>(output, <span class="code-keyword">default</span>, <span class="code-type">TextureLayout</span>.Sampled);
&#10;<span class="code-comment">// Submit the recorded commands to the compute queue.</span>
<span class="code-type">TimelineValue</span> submission = commands.<span class="code-method">Submit</span>();
&#10;<span class="code-comment">// Wait for this submission to complete.</span>
submission.<span class="code-method">Wait</span>();</code></pre>
            </div>
            <div class="architecture-copy">
                <span class="section-kicker">RHI MODEL</span>
                <h2>One renderer architecture.<br><span>Explicit GPU work.</span></h2>
                <p>Zenith.NET keeps graphics API differences behind a compact RHI while leaving queues, synchronization, memory residency, and resource state under application control.</p>
                <div class="architecture-points">
                    <a href="docs/concepts/command-model.md"><i class="bi bi-check-circle" aria-hidden="true"></i><span><strong>Explicit queue submission</strong><small>Record commands and synchronize completion with timeline values.</small></span></a>
                    <a href="docs/concepts/resource-binding.md"><i class="bi bi-check-circle" aria-hidden="true"></i><span><strong>Bindless shader ABI</strong><small>Pass typed resource handles through explicitly laid-out constant data.</small></span></a>
                    <a href="docs/platform/backend-selection.md"><i class="bi bi-check-circle" aria-hidden="true"></i><span><strong>Graphics API selection</strong><small>Choose DirectX 12, Metal 4, or Vulkan 1.4 without splitting the renderer architecture.</small></span></a>
                </div>
            </div>
        </div>
    </section>
    <section class="install-section">
        <div class="landing-shell install-inner">
            <header class="install-heading">
                <span class="section-kicker">QUICK START</span>
                <h2>Choose a graphics API.<span>Build the renderer once.</span></h2>
                <p>Install the package for your target graphics API, then create the context and first frame.</p>
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
                    <p>Concepts, graphics API guidance, workload details, and practical integration notes.</p>
                    <strong>Browse docs</strong>
                </a>
                <a class="resource-card resource-card-nuget" href="https://www.nuget.org/packages/Zenith.NET">
                    <span class="resource-icon"><i class="bi bi-download" aria-hidden="true"></i></span>
                    <strong class="resource-title">NuGet packages</strong>
                    <p>Install the core RHI and your chosen graphics API package directly into your .NET project.</p>
                    <strong>View packages</strong>
                </a>
                <a class="resource-card resource-card-community" href="https://github.com/qian-o/Zenith.NET">
                    <span class="resource-icon"><i class="bi bi-chat-square" aria-hidden="true"></i></span>
                    <strong class="resource-title">Open source</strong>
                    <p>Report issues, inspect graphics API implementations, and contribute on GitHub.</p>
                    <strong>Visit GitHub</strong>
                </a>
            </div>
        </div>
    </section>
    <footer class="landing-footer">
        <div class="landing-shell landing-footer-grid">
            <div class="landing-footer-brand">
                <img class="landing-footer-logo" src="images/Zenith.NET-Logo.svg" alt="Zenith.NET">
                <p>An explicit, cross-platform rendering hardware interface for modern .NET applications.</p>
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
