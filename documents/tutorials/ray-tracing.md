# Ray Tracing

Render three reflective spheres over a checkerboard floor with an orbiting camera, soft shadows, rough reflections, Fresnel response, and ACES tone mapping. The scene traces inline Slang `RayQuery` operations from a compute pipeline. Start from [Project Setup](project-setup.md), then choose **Ray Tracing** from the tutorial selector.

![Ray Tracing](https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Screenshots/ray-tracing.png)

> [!NOTE]
> This tutorial requires `App.Context.Capabilities.RayTracingSupported`.

## Source

### Renderer

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Renderers/RayTracingRenderer.cs" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Renderers/RayTracingRenderer.cs" data-language="csharp"></div>

### Shader

<div data-remote-source="https://raw.githubusercontent.com/qian-o/ZenithTutorials/master/ZenithTutorials/Assets/Shaders/RayTracing.slang" data-source-link="https://github.com/qian-o/ZenithTutorials/blob/master/ZenithTutorials/Assets/Shaders/RayTracing.slang" data-language="slang"></div>

## Scene

The renderer builds two bottom-level acceleration structures and one top-level scene:

| Structure | Geometry |
|-----------|----------|
| Floor BLAS | Two indexed triangles forming the checkerboard floor |
| Sphere BLAS | Three AABBs enclosing the procedural spheres |
| TLAS | One instance of each BLAS |

The AABBs accelerate candidate discovery. The shader performs the exact ray-sphere intersection and commits accepted procedural hits. Floor hits use checkerboard shading, while sphere hits use the corresponding material record.

## Trace and Display

The constructor uploads the floor, AABBs, and sphere records before building the acceleration structures. Each frame updates the orbiting camera and dispatches `CSMain` over a floating-point storage texture.

Primary, shadow, and reflection rays query the same TLAS. The shader distinguishes triangle and procedural commits, applies rough reflection and Fresnel response, and maps the final HDR color through `ACESFilm` before storage.

After the compute pass, the renderer transitions the output texture to `Sampled` and displays it with a fullscreen graphics pass. `Resize` replaces that texture so the trace dimensions continue to match the drawable.