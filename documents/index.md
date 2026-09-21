---
title: '@home.title'
description: '@home.meta.description'
_layout: landing
_disableToc: true
_disableAffix: true
---

<div class="home-page">
<section class="render-hero">
<div class="render-copy">
<h1><resource key="home.introduction.title"><slot name="accent"><span><resource key="home.introduction.accent"></resource></span></slot><slot name="lineBreak"><br/></slot></resource></h1>
<p class="render-description"><resource key="home.introduction.description"></resource></p>
<div class="hero-actions">
<a class="build-button primary" href="~/learn/first-triangle.md"><resource key="home.actions.tutorial"></resource></a>
<a class="build-button secondary" href="~/api/index.md"><resource key="home.actions.reference"></resource></a>
</div>
<p class="backend-note">DirectX · Metal · Vulkan</p>
</div>
<div class="geometry-stage" data-home-scene="">
<canvas aria-label="@home.scene.description" data-resource-label="home.scene.description" id="home-geometry" role="img" tabindex="0"></canvas>
<div class="scene-toolbar">
<div aria-label="@home.scene.label" class="scene-options" data-resource-label="home.scene.label" role="group">
<button aria-pressed="true" data-scene-mode="surface" type="button"><resource key="home.scene.surface"></resource></button>
<button aria-pressed="false" data-scene-mode="wireframe" type="button"><resource key="home.scene.wireframe"></resource></button>
</div>
</div>
</div>
</section>
<section aria-label="@home.entries.label" class="home-paths" data-resource-label="home.entries.label">
<div class="paths-intro"><h2 class="no-anchor"><resource key="home.entries.title"></resource></h2><p><resource key="home.entries.description"></resource></p></div>
<div class="path-grid">
<a class="path-card path-triangle illustrated-link" href="~/learn/first-triangle.md">
<span class="path-label"><resource key="home.entries.tutorial.category"></resource><span>01</span></span>
<svg aria-hidden="true" class="path-art card-art" viewBox="0 0 160 90">
<path class="art-detail" d="M0 75h160M0 45h160M30 0v90M80 0v90M130 0v90"></path>
<path class="art-face" d="M80 8 137 80H23Z"></path><path class="art-wire" d="M80 8 137 80H23Z"></path>
<path class="art-detail art-mark" d="m80 8 0 47m-57 25 57-25 57 25"></path>
<path class="art-flow" d="M80 8 137 80H23Z" pathLength="100"></path>
<circle class="art-node" cx="80" cy="8" r="3"></circle><circle class="art-node" cx="137" cy="80" r="3" style="--art-delay: 220ms"></circle><circle class="art-node" cx="23" cy="80" r="3" style="--art-delay: 440ms"></circle>
</svg>
<h3 class="no-anchor"><resource key="home.entries.tutorial.title"></resource></h3><p><resource key="home.entries.tutorial.description"></resource></p><span class="path-cta"><resource key="home.entries.tutorial.action"></resource></span>
</a>
<a class="path-card illustrated-link" href="~/learn/samples.md">
<span class="path-label"><resource key="home.entries.samples.category"></resource><span>02</span></span>
<svg aria-hidden="true" class="path-art card-art" viewBox="0 0 160 90">
<g class="art-wire"><rect height="34" rx="3" width="56" x="17" y="6"></rect><rect height="34" rx="3" width="56" x="87" y="6"></rect><rect height="34" rx="3" width="56" x="17" y="50"></rect><rect height="34" rx="3" width="56" x="87" y="50"></rect></g>
<path class="art-face" d="m45 13 19 21H26ZM96 59h38v17H96Z"></path>
<path class="art-detail art-mark" d="m45 13 19 21H26ZM95 29l10-14 12 13 18-12M26 69l10-9 12 15 14-16M96 59h38v17H96Z"></path>
<rect class="art-flow" height="34" pathLength="100" rx="3" width="56" x="17" y="6"></rect><rect class="art-flow" height="34" pathLength="100" rx="3" style="--art-delay: 140ms" width="56" x="87" y="6"></rect><rect class="art-flow" height="34" pathLength="100" rx="3" style="--art-delay: 280ms" width="56" x="17" y="50"></rect><rect class="art-flow" height="34" pathLength="100" rx="3" style="--art-delay: 420ms" width="56" x="87" y="50"></rect>
</svg>
<h3 class="no-anchor"><resource key="home.entries.samples.title"></resource></h3><p><resource key="home.entries.samples.description"></resource></p><span class="path-cta"><resource key="home.entries.samples.action"></resource></span>
</a>
<a class="path-card illustrated-link" href="~/api/index.md">
<span class="path-label"><resource key="home.entries.reference.category"></resource><span>03</span></span>
<svg aria-hidden="true" class="path-art card-art" viewBox="0 0 160 90">
<path class="art-wire art-mark" d="M35 10H23v28l-8 7 8 7v28h12M125 10h12v28l8 7-8 7v28h-12"></path>
<path class="art-code-base" d="M49 23h43M49 45h65M49 67h52"></path><path class="art-detail" d="M99 23h14M108 67h6"></path>
<path class="art-flow art-code-flow" d="M49 23h43" pathLength="100"></path><path class="art-flow art-code-flow" d="M49 45h65" pathLength="100" style="--art-delay: 180ms"></path><path class="art-flow art-code-flow" d="M49 67h52" pathLength="100" style="--art-delay: 360ms"></path>
<path class="art-cursor" d="M109 61v12"></path>
</svg>
<h3 class="no-anchor"><resource key="home.entries.reference.title"></resource></h3><p><resource key="home.entries.reference.description"></resource></p><span class="path-cta"><resource key="home.entries.reference.action"></resource></span>
</a>
</div>
</section>
</div>
