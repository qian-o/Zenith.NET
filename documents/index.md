---
title: Zenith.NET - Cross-platform RHI for .NET
description: An explicit, bindless rendering hardware interface for DirectX 12, Metal 4, and Vulkan 1.4.
_layout: landing
---

<div class="landing-page">
    <section class="render-hero">
        <img class="render-hero-media" src="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/ray-tracing.png" alt="Ray-traced scene rendered with Zenith.NET">
        <div class="render-hero-scrim"></div>
        <div class="render-grid"></div>
        <div class="landing-shell render-hero-inner">
            <div class="hero-copy">
                <h1>Zenith.NET<span>Explicit RHI for .NET.</span></h1>
                <p class="hero-lede">A cross-platform rendering hardware interface that keeps queues, resource state, memory, and synchronization visible across DirectX 12, Metal 4, and Vulkan 1.4.</p>
                <div class="hero-actions">
                    <a class="landing-button landing-button-primary" href="docs/index.md">
                        Explore the RHI
                    </a>
                    <a class="landing-button landing-button-secondary" href="tutorials/index.md">
                        Build a renderer
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
                <span class="section-kicker">THE RHI</span>
                <h2>Portable where it should be.<span>Explicit where it matters.</span></h2>
                <p>Zenith.NET presents one compact model without replacing the decisions that define a renderer.</p>
            </header>
            <div class="feature-grid">
                <article class="feature-card">
                    <div class="feature-icon feature-icon-violet"><i class="bi bi-globe2" aria-hidden="true"></i></div>
                    <h3>One renderer model</h3>
                    <p>Share resource, pipeline, command, and presentation code across three native graphics APIs.</p>
                </article>
                <article class="feature-card">
                    <div class="feature-icon feature-icon-pink"><i class="bi bi-cpu" aria-hidden="true"></i></div>
                    <h3>Explicit GPU work</h3>
                    <p>Record commands, transition textures, place barriers, and connect queues with timeline values.</p>
                </article>
                <article class="feature-card">
                    <div class="feature-icon feature-icon-blue"><i class="bi bi-lightning-charge" aria-hidden="true"></i></div>
                    <h3>Bindless by design</h3>
                    <p>Pass compact resource handles through constant data and resolve them as typed Slang descriptors.</p>
                </article>
                <article class="feature-card">
                    <div class="feature-icon feature-icon-cyan"><i class="bi bi-bounding-box-circles" aria-hidden="true"></i></div>
                    <h3>Modern workloads</h3>
                    <p>Use rasterization, compute, indirect drawing, inline RayQuery, and mesh shading in the same command model.</p>
                </article>
            </div>
        </div>
    </section>
    <section class="architecture-section">
        <div class="landing-shell architecture-grid">
            <div class="architecture-copy">
                <span class="section-kicker">DESIGN BOUNDARY</span>
                <h2>The abstraction stops<br><span>before control disappears.</span></h2>
                <p>Zenith.NET presents one RHI while resource ownership and execution order remain application decisions.</p>
            </div>
            <div class="architecture-copy">
                <div class="architecture-points">
                    <a href="docs/fundamentals/runtime.md"><i class="bi bi-check-circle" aria-hidden="true"></i><span><strong>Explicit device selection</strong><small>Select the graphics API and query the capabilities your workload needs.</small></span></a>
                    <a href="docs/fundamentals/synchronization.md"><i class="bi bi-check-circle" aria-hidden="true"></i><span><strong>Explicit synchronization</strong><small>Express texture roles, memory dependencies, and queue ordering directly.</small></span></a>
                    <a href="docs/fundamentals/bindless-resources.md"><i class="bi bi-check-circle" aria-hidden="true"></i><span><strong>Bindless shader access</strong><small>Pass resource handles through explicitly laid-out constant data.</small></span></a>
                </div>
            </div>
        </div>
    </section>
    <section class="resources-section">
        <div class="landing-shell">
            <header class="resources-heading">
                <span class="section-kicker">DOCUMENTATION</span>
                <h2>Choose the level of detail.</h2>
            </header>
            <div class="resource-grid">
                <a class="resource-card resource-card-docs" href="docs/index.md">
                    <span class="resource-icon"><i class="bi bi-book" aria-hidden="true"></i></span>
                    <strong class="resource-title">RHI Guide</strong>
                    <p>Understand the runtime, command, resource, synchronization, workload, and presentation models.</p>
                    <strong>Read the guide</strong>
                </a>
                <a class="resource-card resource-card-nuget" href="tutorials/index.md">
                    <span class="resource-icon"><i class="bi bi-play-circle" aria-hidden="true"></i></span>
                    <strong class="resource-title">Tutorials</strong>
                    <p>Build rasterization, compute, Ray Tracing, and Mesh Shading workloads with the RHI.</p>
                    <strong>Follow the tutorials</strong>
                </a>
                <a class="resource-card resource-card-community" href="api/index.md">
                    <span class="resource-icon"><i class="bi bi-braces" aria-hidden="true"></i></span>
                    <strong class="resource-title">API Reference</strong>
                    <p>Inspect generated namespaces, types, members, descriptors, and enum values from the current source.</p>
                    <strong>Browse the API</strong>
                </a>
            </div>
        </div>
    </section>
    <footer class="landing-footer">
        <div class="landing-shell landing-footer-grid">
            <div class="landing-footer-brand">
                <img class="landing-footer-logo" src="images/Zenith.NET-Logo.svg" alt="Zenith.NET">
                <p>An explicit, bindless rendering hardware interface for DirectX 12, Metal 4, and Vulkan 1.4.</p>
                <a class="landing-footer-github" href="https://github.com/qian-o/Zenith.NET" aria-label="Zenith.NET on GitHub"><i class="bi bi-github" aria-hidden="true"></i></a>
            </div>
            <div class="landing-footer-column">
                <strong class="landing-footer-heading">Explore</strong>
                <a href="docs/index.md">RHI guide</a>
                <a href="tutorials/index.md">Tutorials</a>
                <a href="api/index.md">API reference</a>
                <a href="https://www.nuget.org/packages/Zenith.NET">NuGet packages</a>
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
