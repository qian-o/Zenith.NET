---
title: Zenith.NET - Modern RHI for .NET
description: A modern rendering hardware interface for graphics and compute applications built with C#.
_layout: landing
---

<div class="landing-page">
    <section class="render-hero">
        <div class="render-hero-scrim"></div>
        <div class="render-grid"></div>
        <div class="landing-shell render-hero-inner">
            <div class="hero-copy">
                <h1>Zenith.NET<span>Modern RHI for .NET.</span></h1>
                <p class="hero-lede">Build graphics and compute applications with one consistent C# API for resources, pipelines, commands, and presentation.</p>
                <div class="hero-actions">
                    <a class="landing-button landing-button-primary" href="docs/index.md">
                        Read the guide
                    </a>
                    <a class="landing-button landing-button-secondary" href="tutorials/index.md">
                        Start a tutorial
                    </a>
                </div>
                <div class="backend-support" role="list" aria-label="Supported graphics APIs">
                    <span class="backend-label" aria-hidden="true">GRAPHICS APIs</span>
                    <span class="backend-pill backend-directx" role="listitem">DirectX 12</span>
                    <span class="backend-pill backend-metal" role="listitem">Metal 4</span>
                    <span class="backend-pill backend-vulkan" role="listitem">Vulkan 1.4</span>
                </div>
            </div>
            <div class="render-viewport" data-tutorial-carousel role="region" aria-roledescription="carousel" aria-label="Zenith.NET tutorial outputs">
                <div class="render-viewport-toolbar">
                    <span class="render-viewport-title"><i class="bi bi-image" aria-hidden="true"></i><span data-carousel-title>Hello Triangle</span></span>
                    <span class="render-viewport-details"><span class="render-viewport-position" data-carousel-position>1 / 7</span></span>
                    <span class="zenith-carousel-status" data-carousel-status aria-live="polite" aria-atomic="true"></span>
                    <a class="render-viewport-open" data-carousel-open href="tutorials/rasterization/hello-triangle.md" aria-label="Open Hello Triangle tutorial" title="Open tutorial"><i class="bi bi-arrow-up-right" aria-hidden="true"></i></a>
                </div>
                <div class="render-viewport-canvas">
                    <a class="render-carousel-slide is-active" data-carousel-title="Hello Triangle" href="tutorials/rasterization/hello-triangle.md" aria-label="1 of 7: Hello Triangle" aria-current="true">
                        <img class="render-hero-media" src="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/hello-triangle.png" alt="Hello Triangle tutorial output rendered with Zenith.NET" fetchpriority="high">
                    </a>
                    <a class="render-carousel-slide" data-carousel-title="Textured Quad" href="tutorials/rasterization/textured-quad.md" aria-label="2 of 7: Textured Quad" aria-hidden="true" tabindex="-1">
                        <img class="render-hero-media" data-src="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/textured-quad.png" alt="Textured Quad tutorial output rendered with Zenith.NET">
                    </a>
                    <a class="render-carousel-slide" data-carousel-title="Spinning Cube" href="tutorials/rasterization/spinning-cube.md" aria-label="3 of 7: Spinning Cube" aria-hidden="true" tabindex="-1">
                        <img class="render-hero-media" data-src="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/spinning-cube.png" alt="Spinning Cube tutorial output rendered with Zenith.NET">
                    </a>
                    <a class="render-carousel-slide" data-carousel-title="Image Processing" href="tutorials/workloads/image-processing.md" aria-label="4 of 7: Image Processing" aria-hidden="true" tabindex="-1">
                        <img class="render-hero-media" data-src="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/compute-shader.png" alt="Image Processing tutorial output rendered with Zenith.NET">
                    </a>
                    <a class="render-carousel-slide" data-carousel-title="Indirect Drawing" href="tutorials/rasterization/indirect-drawing.md" aria-label="5 of 7: Indirect Drawing" aria-hidden="true" tabindex="-1">
                        <img class="render-hero-media" data-src="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/indirect-drawing.png" alt="Indirect Drawing tutorial output rendered with Zenith.NET">
                    </a>
                    <a class="render-carousel-slide" data-carousel-title="Ray Tracing" href="tutorials/workloads/ray-tracing.md" aria-label="6 of 7: Ray Tracing" aria-hidden="true" tabindex="-1">
                        <img class="render-hero-media" data-src="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/ray-tracing.png" alt="Ray Tracing tutorial output rendered with Zenith.NET">
                    </a>
                    <a class="render-carousel-slide" data-carousel-title="Mesh Shading" href="tutorials/workloads/mesh-shading.md" aria-label="7 of 7: Mesh Shading" aria-hidden="true" tabindex="-1">
                        <img class="render-hero-media" data-src="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/mesh-shading.png" alt="Mesh Shading tutorial output rendered with Zenith.NET">
                    </a>
                    <button class="render-carousel-arrow render-carousel-arrow-prev" type="button" data-carousel-prev aria-label="Previous tutorial" title="Previous tutorial"><i class="bi bi-chevron-left" aria-hidden="true"></i></button>
                    <button class="render-carousel-arrow render-carousel-arrow-next" type="button" data-carousel-next aria-label="Next tutorial" title="Next tutorial"><i class="bi bi-chevron-right" aria-hidden="true"></i></button>
                    <div class="render-carousel-progress" aria-hidden="true"><span data-carousel-progress></span></div>
                </div>
            </div>
        </div>
    </section>
    <section class="features-section">
        <div class="landing-shell">
            <header class="section-heading">
                <span class="section-kicker">THE RHI</span>
                <h2>One model for rendering.<span>Designed for modern C#.</span></h2>
                <p>Start with a small set of objects and use the same workflow across supported graphics APIs.</p>
            </header>
            <div class="feature-grid">
                <article class="feature-card">
                    <div class="feature-icon feature-icon-violet"><i class="bi bi-globe2" aria-hidden="true"></i></div>
                    <h3>Consistent C# API</h3>
                    <p>Create resources, pipelines, commands, and swap chains through a focused object model.</p>
                </article>
                <article class="feature-card">
                    <div class="feature-icon feature-icon-pink"><i class="bi bi-cpu" aria-hidden="true"></i></div>
                    <h3>Clear command flow</h3>
                    <p>Record work in order, submit it to a queue, and track completion with timeline values.</p>
                </article>
                <article class="feature-card">
                    <div class="feature-icon feature-icon-blue"><i class="bi bi-lightning-charge" aria-hidden="true"></i></div>
                    <h3>Simple shader binding</h3>
                    <p>Pass compact resource handles in constant data and use them as typed Slang resources.</p>
                </article>
                <article class="feature-card">
                    <div class="feature-icon feature-icon-cyan"><i class="bi bi-bounding-box-circles" aria-hidden="true"></i></div>
                    <h3>Modern workloads</h3>
                    <p>Use rasterization, compute, and indirect commands, with capability-gated Ray Tracing and mesh shading.</p>
                </article>
            </div>
        </div>
    </section>
    <section class="architecture-section">
        <div class="landing-shell architecture-grid">
            <div class="architecture-copy">
                <span class="section-kicker">PROGRAMMING MODEL</span>
                <h2>Small building blocks.<br><span>Predictable application flow.</span></h2>
                <p>Create a context, choose a queue, record commands, and submit the result.</p>
            </div>
            <div class="architecture-copy">
                <div class="architecture-points">
                    <a href="docs/fundamentals/runtime.md"><span class="architecture-step" aria-hidden="true">01</span><span><strong>Create the runtime</strong><small>Select a graphics API, inspect capabilities, and create application resources.</small></span></a>
                    <a href="docs/fundamentals/commands.md"><span class="architecture-step" aria-hidden="true">02</span><span><strong>Record GPU work</strong><small>Use command buffers for rendering, compute, copies, and resource transitions.</small></span></a>
                    <a href="docs/fundamentals/bindless-resources.md"><span class="architecture-step" aria-hidden="true">03</span><span><strong>Bind shader resources</strong><small>Store typed resource handles in C# constant data shared with Slang.</small></span></a>
                </div>
            </div>
        </div>
    </section>
    <section class="resources-section">
        <div class="landing-shell">
            <header class="resources-heading">
                <span class="section-kicker">DOCUMENTATION</span>
                <h2>Learn at your own pace.</h2>
            </header>
            <div class="resource-grid">
                <a class="resource-card resource-card-docs" href="docs/index.md">
                    <span class="resource-icon"><i class="bi bi-book" aria-hidden="true"></i></span>
                    <strong class="resource-title">RHI Guide</strong>
                    <p>Learn the core objects and workflows used by Zenith.NET applications.</p>
                    <strong>Read the guide</strong>
                </a>
                <a class="resource-card resource-card-nuget" href="tutorials/index.md">
                    <span class="resource-icon"><i class="bi bi-play-circle" aria-hidden="true"></i></span>
                    <strong class="resource-title">Tutorials</strong>
                    <p>Build focused examples that progress from project setup to modern GPU workloads.</p>
                    <strong>Follow the tutorials</strong>
                </a>
                <a class="resource-card resource-card-community" href="api/index.md">
                    <span class="resource-icon"><i class="bi bi-braces" aria-hidden="true"></i></span>
                    <strong class="resource-title">API Reference</strong>
                    <p>Look up public namespaces, types, members, and enum values.</p>
                    <strong>Browse the API</strong>
                </a>
            </div>
        </div>
    </section>
    <footer class="landing-footer">
        <div class="landing-shell landing-footer-grid">
            <div class="landing-footer-brand">
                <img class="landing-footer-logo" src="images/Zenith.NET-Logo.svg" alt="Zenith.NET">
                <p>A modern rendering hardware interface for graphics and compute applications built with C#.</p>
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
