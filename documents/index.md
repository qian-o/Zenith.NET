---
title: Modern RHI for .NET
description: A consistent C# API for graphics and compute across DirectX, Metal, and Vulkan.
_layout: landing
_disableToc: true
_disableAffix: true
---

<div class="home-page">
  <section class="render-hero">
    <div class="render-copy">
      <h1>Build your renderer.<br><span>In C#.</span></h1>
      <p class="render-description">Create resources, record commands, and submit graphics or compute work through one .NET API.</p>
      <div class="hero-actions">
        <a class="build-button primary" href="learn/first-triangle.md">Start with a triangle</a>
        <a class="build-button secondary" href="api/index.md">Browse API</a>
      </div>
      <p class="backend-note">DirectX · Metal · Vulkan</p>
    </div>
    <div class="geometry-stage" data-home-scene>
      <canvas id="home-geometry" tabindex="0" role="img" aria-label="Rotating geometry study. Drag or use the arrow keys to adjust the view."></canvas>
      <div class="scene-toolbar">
        <div class="scene-options" role="group" aria-label="Geometry display">
          <button type="button" data-scene-mode="surface" aria-pressed="true">Surface</button>
          <button type="button" data-scene-mode="wireframe" aria-pressed="false">Wireframe</button>
        </div>
      </div>
    </div>
  </section>
  <section class="home-paths" aria-label="Explore the documentation">
    <div class="paths-intro"><h2 class="no-anchor">Start building.</h2><p>A first frame. A focused example. A closer look at the API.</p></div>
    <div class="path-grid">
      <a class="path-card path-triangle illustrated-link" href="learn/first-triangle.md">
        <span class="path-label">Tutorial <span>01</span></span>
        <svg class="path-art card-art" viewBox="0 0 160 90" aria-hidden="true">
          <path class="art-detail" d="M0 75h160M0 45h160M30 0v90M80 0v90M130 0v90"/>
          <path class="art-face" d="M80 8 137 80H23Z"/><path class="art-wire" d="M80 8 137 80H23Z"/>
          <path class="art-detail art-mark" d="m80 8 0 47m-57 25 57-25 57 25"/>
          <path class="art-flow" pathLength="100" d="M80 8 137 80H23Z"/>
          <circle class="art-node" cx="80" cy="8" r="3"/><circle class="art-node" style="--art-delay: 220ms" cx="137" cy="80" r="3"/><circle class="art-node" style="--art-delay: 440ms" cx="23" cy="80" r="3"/>
        </svg>
        <h3 class="no-anchor">First Triangle</h3><p>Build a complete rendering application, from setup to the first frame.</p><span class="path-cta">Start tutorial</span>
      </a>
      <a class="path-card illustrated-link" href="learn/samples.md">
        <span class="path-label">Examples <span>02</span></span>
        <svg class="path-art card-art" viewBox="0 0 160 90" aria-hidden="true">
          <g class="art-wire"><rect x="17" y="6" width="56" height="34" rx="3"/><rect x="87" y="6" width="56" height="34" rx="3"/><rect x="17" y="50" width="56" height="34" rx="3"/><rect x="87" y="50" width="56" height="34" rx="3"/></g>
          <path class="art-face" d="m45 13 19 21H26ZM96 59h38v17H96Z"/>
          <path class="art-detail art-mark" d="m45 13 19 21H26ZM95 29l10-14 12 13 18-12M26 69l10-9 12 15 14-16M96 59h38v17H96Z"/>
          <rect class="art-flow" pathLength="100" x="17" y="6" width="56" height="34" rx="3"/><rect class="art-flow" style="--art-delay: 140ms" pathLength="100" x="87" y="6" width="56" height="34" rx="3"/><rect class="art-flow" style="--art-delay: 280ms" pathLength="100" x="17" y="50" width="56" height="34" rx="3"/><rect class="art-flow" style="--art-delay: 420ms" pathLength="100" x="87" y="50" width="56" height="34" rx="3"/>
        </svg>
        <h3 class="no-anchor">Samples</h3><p>Explore focused rendering and compute techniques through their source code.</p><span class="path-cta">Browse samples</span>
      </a>
      <a class="path-card illustrated-link" href="api/index.md">
        <span class="path-label">Reference <span>03</span></span>
        <svg class="path-art card-art" viewBox="0 0 160 90" aria-hidden="true">
          <path class="art-wire art-mark" d="M35 10H23v28l-8 7 8 7v28h12M125 10h12v28l8 7-8 7v28h-12"/>
          <path class="art-code-base" d="M49 23h43M49 45h65M49 67h52"/><path class="art-detail" d="M99 23h14M108 67h6"/>
          <path class="art-flow art-code-flow" pathLength="100" d="M49 23h43"/><path class="art-flow art-code-flow" style="--art-delay: 180ms" pathLength="100" d="M49 45h65"/><path class="art-flow art-code-flow" style="--art-delay: 360ms" pathLength="100" d="M49 67h52"/>
          <path class="art-cursor" d="M109 61v12"/>
        </svg>
        <h3 class="no-anchor">API Reference</h3><p>Find the types, signatures, and members behind your application.</p><span class="path-cta">Explore the API</span>
      </a>
    </div>
  </section>
</div>
