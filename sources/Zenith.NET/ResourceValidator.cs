namespace Zenith.NET;

internal class ResourceValidator(GraphicsContext context)
{
    private static readonly PixelFormat[] swapChainFormats =
    [
        PixelFormat.R8G8B8A8UNorm,
        PixelFormat.R8G8B8A8UNormSRgb,
        PixelFormat.R16G16B16A16Float,
        PixelFormat.B8G8R8A8UNorm,
        PixelFormat.B8G8R8A8UNormSRgb
    ];

    private static readonly PixelFormat[] depthStencilFormats =
    [
        PixelFormat.D24UNormS8UInt,
        PixelFormat.D32FloatS8UInt
    ];

    private static readonly TextureType[] arrayTextureTypes =
    [
        TextureType.Texture1DArray,
        TextureType.Texture2DArray,
        TextureType.TextureCubeArray
    ];

    public void ValidateSwapChainDesc(SwapChainDesc desc)
    {
        ValidateSurface(desc.Surface);

        if (!swapChainFormats.Contains(desc.ColorTargetFormat))
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Invalid swap chain color target format '{desc.ColorTargetFormat}'. " +
                                         $"Supported formats are: {string.Join(", ", swapChainFormats.Select(static item => item.ToString()))}.");
        }

        if (desc.DepthStencilTargetFormat.HasValue && !depthStencilFormats.Contains(desc.DepthStencilTargetFormat.Value))
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Invalid swap chain depth-stencil target format '{desc.DepthStencilTargetFormat.Value}'. " +
                                         $"Supported formats are: {string.Join(", ", depthStencilFormats.Select(static item => item.ToString()))}.");
        }
    }

    public void ValidateBufferDesc(BufferDesc desc)
    {
        if (desc.SizeInBytes is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Buffer size must be greater than zero. A buffer with zero size cannot be created.");
        }

        if (desc.StrideInBytes is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Warning,
                                         "Buffer stride is zero. This may lead to unexpected behavior when using structured buffers. " +
                                         "Consider setting a non-zero stride that matches your data structure size.");
        }
    }

    public void ValidateBufferViewDesc(BufferViewDesc desc)
    {
        if (desc.Buffer?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Buffer view must reference a valid buffer that is not null and not disposed. " +
                                         "Ensure the buffer exists and has not been disposed before creating the view.");

            return;
        }

        if (desc.OffsetInBytes >= desc.Buffer.Desc.SizeInBytes)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Buffer view offset ({desc.OffsetInBytes} bytes) must be less than the buffer's total size ({desc.Buffer.Desc.SizeInBytes} bytes). " +
                                         $"Valid range is 0 to {desc.Buffer.Desc.SizeInBytes - 1}.");
        }

        if (desc.SizeInBytes is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Buffer view size must be greater than zero. A view with zero size cannot be created.");
        }
        else if (desc.OffsetInBytes + desc.SizeInBytes > desc.Buffer.Desc.SizeInBytes)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Buffer view range exceeds buffer bounds. View range [{desc.OffsetInBytes}, {desc.OffsetInBytes + desc.SizeInBytes}) " +
                                         $"exceeds buffer size ({desc.Buffer.Desc.SizeInBytes} bytes). " +
                                         $"Ensure that OffsetInBytes + SizeInBytes ≤ {desc.Buffer.Desc.SizeInBytes}.");
        }

        if (desc.StrideInBytes is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Warning,
                                         "Buffer view stride is zero. This may lead to unexpected behavior when using structured buffers. " +
                                         "Consider setting a non-zero stride that matches your data structure size.");
        }
    }

    public void ValidateTextureDesc(TextureDesc desc)
    {
        ValidateDefinedEnum(desc.Type, "texture type");

        ValidateDefinedEnum(desc.Format, "texture format");

        if (desc.Type is TextureType.Texture1D or TextureType.Texture1DArray && desc.Width is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Texture width must be greater than zero for 1D textures. " +
                                         "Specify a valid width for the texture dimensions.");
        }
        else if (desc.Type is TextureType.Texture2D or TextureType.Texture2DArray && (desc.Width is 0 || desc.Height is 0))
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Texture width ({desc.Width}) and height ({desc.Height}) must both be greater than zero for 2D textures. " +
                                         "Specify valid dimensions for both width and height.");
        }
        else if (desc.Type is TextureType.Texture3D && (desc.Width is 0 || desc.Height is 0 || desc.Depth is 0))
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Texture width ({desc.Width}), height ({desc.Height}), and depth ({desc.Depth}) must all be greater than zero for 3D textures. " +
                                         "Specify valid dimensions for all three axes.");
        }
        else if (desc.Type is TextureType.TextureCube or TextureType.TextureCubeArray && (desc.Width is 0 || desc.Height is 0 || desc.Width != desc.Height))
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Cube texture dimensions are invalid. Width ({desc.Width}) and height ({desc.Height}) must be equal and greater than zero. " +
                                         "Cube textures require square faces.");
        }

        if (arrayTextureTypes.Contains(desc.Type))
        {
            if (desc.Layers is 0)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             $"Texture array layers must be greater than zero for {desc.Type}. " +
                                             "Specify at least one layer for the texture array.");
            }
        }
        else if (desc.Layers is not 1)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Non-array texture type '{desc.Type}' must have exactly 1 layer, but {desc.Layers} layers were specified. " +
                                         "Set Layers to 1 for non-array textures.");
        }

        if (desc.MipLevels is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Texture mip levels must be greater than zero. " +
                                         "Specify at least 1 mip level (the base level) for the texture.");
        }

        ValidateDefinedEnum(desc.SampleCount, "texture sample count");
    }

    public void ValidateTextureViewDesc(TextureViewDesc desc)
    {
        if (desc.Texture?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Texture view must reference a valid texture that is not null and not disposed. " +
                                         "Ensure the texture exists and has not been disposed before creating the view.");

            return;
        }

        if (desc.FirstLayer >= desc.Texture.Desc.Layers)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Texture view first layer index ({desc.FirstLayer}) must be less than the texture's total layer count ({desc.Texture.Desc.Layers}). " +
                                         $"Valid range is 0 to {desc.Texture.Desc.Layers - 1}.");
        }

        if (desc.LayerCount is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Texture view layer count must be greater than zero. " +
                                         "Specify at least one layer to include in the view.");
        }
        else if (desc.FirstLayer + desc.LayerCount > desc.Texture.Desc.Layers)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Texture view layer range exceeds texture bounds. View range [{desc.FirstLayer}, {desc.FirstLayer + desc.LayerCount}) " +
                                         $"exceeds texture layer count ({desc.Texture.Desc.Layers}). " +
                                         $"Ensure that FirstLayer + LayerCount ≤ {desc.Texture.Desc.Layers}.");
        }

        if (desc.FirstMipLevel >= desc.Texture.Desc.MipLevels)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Texture view first mip level ({desc.FirstMipLevel}) must be less than the texture's total mip levels ({desc.Texture.Desc.MipLevels}). " +
                                         $"Valid range is 0 to {desc.Texture.Desc.MipLevels - 1}.");
        }

        if (desc.MipLevelCount is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Texture view mip level count must be greater than zero. " +
                                         "Specify at least one mip level to include in the view.");
        }
        else if (desc.FirstMipLevel + desc.MipLevelCount > desc.Texture.Desc.MipLevels)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Texture view mip level range exceeds texture bounds. View range [{desc.FirstMipLevel}, {desc.FirstMipLevel + desc.MipLevelCount}) " +
                                         $"exceeds texture mip levels ({desc.Texture.Desc.MipLevels}). " +
                                         $"Ensure that FirstMipLevel + MipLevelCount ≤ {desc.Texture.Desc.MipLevels}.");
        }
    }

    public void ValidateSamplerDesc(SamplerDesc desc)
    {
        ValidateDefinedEnum(desc.U, "texture U (horizontal) address mode");

        ValidateDefinedEnum(desc.V, "texture V (vertical) address mode");

        ValidateDefinedEnum(desc.W, "texture W (depth) address mode");

        ValidateDefinedEnum(desc.Filter, "texture filter mode");

        ValidateDefinedEnum(desc.ComparisonFunc, "texture comparison function");

        if (desc.MaxAnisotropy is not 1 and not 2 and not 4 and not 8 and not 16)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Invalid max anisotropy value '{desc.MaxAnisotropy}'. " +
                                         "Supported values are: 1, 2, 4, 8, or 16. These values represent the maximum anisotropic filtering level.");
        }

        if (desc.MinLod < 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Sampler MinLod ({desc.MinLod}) must be greater than or equal to zero. " +
                                         "Negative LOD values are not supported.");
        }

        if (desc.MaxLod < desc.MinLod)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Sampler MaxLod ({desc.MaxLod}) must be greater than or equal to MinLod ({desc.MinLod}). " +
                                         "The LOD range must be valid with MaxLod ≥ MinLod.");
        }

        if (desc.LodBias is < -16 or > 16)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Sampler LodBias ({desc.LodBias}) must be within the range [-16, 16]. " +
                                         "This range represents the maximum LOD bias supported by most hardware.");
        }

        ValidateDefinedEnum(desc.BorderColor, "sampler border color");
    }

    public void ValidateResourceLayoutDesc(ResourceLayoutDesc desc)
    {
        if (desc.Elements is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Resource layout elements array cannot be null. " +
                                         "Provide a valid array of resource elements, even if empty.");

            return;
        }

        if (desc.Elements.Length is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Resource layout must have at least one element. " +
                                         "Define the resources that will be bound to this layout.");
        }

        for (int i = 0; i < desc.Elements.Length; i++)
        {
            ResourceElement element = desc.Elements[i];

            ValidateDefinedEnum(element.Type, $"resource element type at binding index {i}");

            if (element.Count is 0)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             $"Resource element at binding index {i} has a count of zero. " +
                                             "Each element must have a count of at least 1 to represent valid resources.");
            }
        }
    }

    public void ValidateResourceSetDesc(ResourceSetDesc desc)
    {
        if (desc.Layout?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Resource set must reference a valid resource layout that is not null and not disposed. " +
                                         "Ensure the layout exists and has not been disposed before creating the resource set.");

            return;
        }

        if (desc.Resources is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Resource set resources array cannot be null. " +
                                         "Provide a valid array of resources matching the layout requirements.");

            return;
        }

        ResourceType[] types = [.. desc.Layout.Desc.Elements.SelectMany(static item => Enumerable.Repeat(item.Type, (int)item.Count))];

        if (desc.Resources.Length != types.Length)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Resource set has incorrect number of resources. Expected {types.Length} resources based on the layout, " +
                                         $"but {desc.Resources.Length} were provided. The resource count must exactly match the layout definition.");

            return;
        }

        for (int i = 0; i < desc.Resources.Length; i++)
        {
            IBindableResource? resource = desc.Resources[i];

            if (resource?.IsDisposed is not false)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             $"Resource at binding slot {i} is null or disposed. " +
                                             $"All resources must be valid, non-null, and not disposed. Expected resource type: {types[i]}.");

                continue;
            }

            switch (types[i])
            {
                case ResourceType.ConstantBuffer:
                case ResourceType.StructuredBuffer:
                case ResourceType.StructuredBufferReadWrite:
                    if (resource is not Buffer and not BufferView)
                    {
                        context.PublishDebugCallback(MessageCategory.System,
                                                     MessageSeverity.Error,
                                                     $"Resource at binding slot {i} is not a valid buffer resource. " +
                                                     $"Expected Buffer or BufferView for resource type '{types[i]}', but got '{resource.GetType().Name}'.");
                    }
                    break;
                case ResourceType.Texture:
                case ResourceType.TextureReadWrite:
                    if (resource is not Texture and not TextureView)
                    {
                        context.PublishDebugCallback(MessageCategory.System,
                                                     MessageSeverity.Error,
                                                     $"Resource at binding slot {i} is not a valid texture resource. " +
                                                     $"Expected Texture or TextureView for resource type '{types[i]}', but got '{resource.GetType().Name}'.");
                    }
                    break;
                case ResourceType.Sampler:
                    if (resource is not Sampler)
                    {
                        context.PublishDebugCallback(MessageCategory.System,
                                                     MessageSeverity.Error,
                                                     $"Resource at binding slot {i} is not a valid sampler. " +
                                                     $"Expected Sampler for resource type '{types[i]}', but got '{resource.GetType().Name}'.");
                    }
                    break;
                case ResourceType.AccelerationStructure:
                    if (resource is not TopLevelAccelerationStructure)
                    {
                        context.PublishDebugCallback(MessageCategory.System,
                                                     MessageSeverity.Error,
                                                     $"Resource at binding slot {i} is not a valid acceleration structure. " +
                                                     $"Expected TopLevelAccelerationStructure for resource type '{types[i]}', but got '{resource.GetType().Name}'.");
                    }
                    break;
                default:
                    context.PublishDebugCallback(MessageCategory.System,
                                                 MessageSeverity.Error,
                                                 $"Unknown resource type '{types[i]}' at binding slot {i}. " +
                                                 $"Supported resource types are: {string.Join(", ", Enum.GetNames<ResourceType>())}.");
                    break;
            }
        }
    }

    public void ValidateFrameBufferDesc(FrameBufferDesc desc)
    {
        if (desc.ColorTargets is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Frame buffer color targets array cannot be null. " +
                                         "Provide a valid array of color targets, even if empty.");

            return;
        }

        if (desc.ColorTargets.Length is 0 && desc.DepthStencilTarget is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Frame buffer must have at least one render target. " +
                                         "Provide either one or more color targets, or a depth-stencil target, or both.");

            return;
        }

        if (desc.ColorTargets.Length > 8)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Frame buffer has too many color targets ({desc.ColorTargets.Length}). " +
                                         "Maximum supported color targets is 8. This is a hardware limitation on most GPUs.");
        }

        for (int i = 0; i < desc.ColorTargets.Length; i++)
        {
            ValidateFrameBufferAttachment(desc.ColorTargets[i], $"color target at attachment slot {i}");
        }

        if (desc.DepthStencilTarget is not null)
        {
            ValidateFrameBufferAttachment(desc.DepthStencilTarget.Value, "depth-stencil target");
        }
    }

    public void ValidateShaderDesc(ShaderDesc desc)
    {
        if (desc.ShaderBytes is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Shader bytecode cannot be null. " +
                                         "Provide valid compiled shader bytecode.");

            return;
        }

        if (desc.ShaderBytes.Length is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Shader bytecode is empty. " +
                                         "Provide valid compiled shader bytecode with non-zero length.");
        }

        if (string.IsNullOrEmpty(desc.EntryPoint))
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Shader entry point name is missing. " +
                                         "Specify the function name that serves as the shader's entry point (e.g., 'main', 'VSMain', etc.).");
        }

        ValidateDefinedEnum(desc.Stage, "shader stage");
    }

    public void ValidateGraphicsPipelineDesc(GraphicsPipelineDesc desc)
    {
        // RenderStates
        {
            RenderStates renderStates = desc.RenderStates;

            // RasterizerState
            {
                RasterizerState rasterizerState = renderStates.RasterizerState;

                ValidateDefinedEnum(rasterizerState.CullMode, "rasterizer cull mode");

                ValidateDefinedEnum(rasterizerState.FillMode, "rasterizer fill mode");

                ValidateDefinedEnum(rasterizerState.FrontFace, "rasterizer front face winding order");

                if (rasterizerState.DepthBias < 0)
                {
                    context.PublishDebugCallback(MessageCategory.System,
                                                 MessageSeverity.Error,
                                                 $"Rasterizer depth bias ({rasterizerState.DepthBias}) must be greater than or equal to zero. " +
                                                 "Negative depth bias values are not supported.");
                }

                if (rasterizerState.DepthBiasClamp < 0)
                {
                    context.PublishDebugCallback(MessageCategory.System,
                                                 MessageSeverity.Error,
                                                 $"Rasterizer depth bias clamp ({rasterizerState.DepthBiasClamp}) must be greater than or equal to zero. " +
                                                 "This value limits the maximum depth bias applied.");
                }

                if (rasterizerState.SlopeScaledDepthBias < 0)
                {
                    context.PublishDebugCallback(MessageCategory.System,
                                                 MessageSeverity.Error,
                                                 $"Rasterizer slope scaled depth bias ({rasterizerState.SlopeScaledDepthBias}) must be greater than or equal to zero. " +
                                                 "This value scales the depth bias based on polygon slope.");
                }
            }

            // DepthStencilState
            {
                DepthStencilState depthStencilState = renderStates.DepthStencilState;

                ValidateDefinedEnum(depthStencilState.DepthFunc, "depth comparison function");

                ValidateDepthStencilStateOp(depthStencilState.FrontFace, "front-facing polygons");

                ValidateDepthStencilStateOp(depthStencilState.BackFace, "back-facing polygons");
            }

            // BlendState
            {
                BlendState blendState = renderStates.BlendState;

                BlendStateRenderTarget[] renderTargets = blendState.IndependentBlendEnable
                    ? [blendState.RenderTarget0, blendState.RenderTarget1, blendState.RenderTarget2, blendState.RenderTarget3,
                       blendState.RenderTarget4, blendState.RenderTarget5, blendState.RenderTarget6, blendState.RenderTarget7]
                    : [blendState.RenderTarget0];

                for (int i = 0; i < renderTargets.Length; i++)
                {
                    ValidateBlendStateRenderTarget(renderTargets[i], $"blend state render target {i}");
                }
            }
        }

        // Shaders
        {
            GraphicsShaders shaders = desc.Shaders;

            ValidateShader(shaders.Vertex, "Vertex shader");

            if (shaders.Hull is not null)
            {
                ValidateShader(shaders.Hull, "Hull (tessellation control) shader");
            }

            if (shaders.Domain is not null)
            {
                ValidateShader(shaders.Domain, "Domain (tessellation evaluation) shader");
            }

            if (shaders.Geometry is not null)
            {
                ValidateShader(shaders.Geometry, "Geometry shader");
            }

            ValidateShader(shaders.Pixel, "Pixel (fragment) shader");
        }

        if (desc.InputLayouts is null || desc.InputLayouts.Length is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Graphics pipeline must have at least one input layout. " +
                                         "Define the vertex input layout that describes how vertex data is organized.");
        }

        ValidateResourceLayouts(desc.ResourceLayouts);

        ValidateOutput(desc.Outputs);
    }

    public void ValidateComputePipelineDesc(ComputePipelineDesc desc)
    {
        if (desc.Shader?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Compute pipeline must reference a valid compute shader that is not null and not disposed. " +
                                         "Ensure the compute shader exists and has not been disposed.");
        }

        ValidateResourceLayouts(desc.ResourceLayouts);
    }

    public void ValidateRayTracingPipelineDesc(RayTracingPipelineDesc desc)
    {
        // Shaders
        {
            RayTracingShaders shaders = desc.Shaders;

            ValidateShader(shaders.RayGeneration, "Ray generation shader");

            ValidateShaders(shaders.Miss, "Miss shader array");

            ValidateShaders(shaders.AnyHit, "Any-hit shader array");

            ValidateShaders(shaders.Intersection, "Intersection shader array");

            ValidateShaders(shaders.ClosestHit, "Closest-hit shader array");
        }

        if (desc.HitGroups is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Ray tracing pipeline hit groups array cannot be null. " +
                                         "Provide a valid array of hit groups that define shader combinations for ray-triangle intersections.");
        }
        else
        {
            for (int i = 0; i < desc.HitGroups.Length; i++)
            {
                HitGroup hitGroup = desc.HitGroups[i];

                ValidateDefinedEnum(hitGroup.Type, $"hit group type at index {i}");

                if (string.IsNullOrEmpty(hitGroup.Name))
                {
                    context.PublishDebugCallback(MessageCategory.System,
                                                 MessageSeverity.Error,
                                                 $"Hit group at index {i} has an empty or null name. " +
                                                 "Each hit group must have a unique, non-empty name for shader table indexing.");
                }
            }

            string[] hitGroupNames = [.. desc.HitGroups.Select(static item => item.Name)];

            if (hitGroupNames.Distinct().Count() != hitGroupNames.Length)
            {
                var duplicates = hitGroupNames.GroupBy(static x => x)
                                              .Where(static g => g.Count() > 1)
                                              .Select(static g => g.Key);

                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             $"Ray tracing pipeline contains duplicate hit group names: {string.Join(", ", duplicates)}. " +
                                             "Each hit group must have a unique name for proper shader table addressing.");
            }
        }

        ValidateResourceLayouts(desc.ResourceLayouts);

        if (desc.MaxTraceRecursionDepth > 31)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Max trace recursion depth ({desc.MaxTraceRecursionDepth}) exceeds the maximum supported value of 31. " +
                                         "This is a hardware limitation for ray tracing recursion depth.");
        }

        if (desc.MaxPayloadSizeInBytes % 4 is not 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Max payload size ({desc.MaxPayloadSizeInBytes} bytes) must be a multiple of 4. " +
                                         "Ray tracing payloads must be aligned to 4-byte boundaries.");
        }

        if (desc.MaxAttributeSizeInBytes % 4 is not 0 or > 32)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Max attribute size ({desc.MaxAttributeSizeInBytes} bytes) must be a multiple of 4 and not exceed 32 bytes. " +
                                         "This is a hardware limitation for ray-triangle intersection attributes.");
        }
    }

    private void ValidateSurface(Surface surface)
    {
        ValidateDefinedEnum(surface.Type, "surface type");

        if (surface.Handles is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Surface handles array cannot be null. " +
                                         "Provide the platform-specific window/surface handles required for presentation.");

            return;
        }

        string expectedHandles = surface.Type switch
        {
            SurfaceType.Win32 => "1 handle (HWND)",
            SurfaceType.Wayland => "2 handles (display, surface)",
            SurfaceType.Xlib => "2 handles (display, window)",
            SurfaceType.Android => "1 handle (ANativeWindow)",
            SurfaceType.IOS => "1 handle (UIView/CAMetalLayer)",
            SurfaceType.MacOS => "1 handle (NSView/CAMetalLayer)",
            _ => "unknown number of handles"
        };

        int expectedCount = surface.Type switch
        {
            SurfaceType.Win32 or SurfaceType.Android or SurfaceType.IOS or SurfaceType.MacOS => 1,
            SurfaceType.Wayland or SurfaceType.Xlib => 2,
            _ => -1
        };

        if (expectedCount != -1 && surface.Handles.Length != expectedCount)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Surface type '{surface.Type}' requires exactly {expectedHandles}, but {surface.Handles.Length} were provided. " +
                                         "Ensure you provide the correct number of platform-specific handles.");
        }

        if (surface.Handles.Any(static item => item is 0))
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Surface handles contain null (zero) values. " +
                                         "All platform handles must be valid, non-zero pointers.");
        }

        if (surface.Width is 0 || surface.Height is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Surface dimensions are invalid. Width ({surface.Width}) and height ({surface.Height}) must both be greater than zero. " +
                                         "Specify valid window/surface dimensions.");
        }
    }

    private void ValidateFrameBufferAttachment(FrameBufferAttachment attachment, string name)
    {
        if (attachment.Target?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Frame buffer {name} references an invalid texture. " +
                                         "The texture must be valid, non-null, and not disposed.");

            return;
        }

        ObtainTextureValues(attachment.Target, out TextureType type, out uint layers, out uint mipLevels, name);

        if (type is not TextureType.Texture2D and not TextureType.Texture2DArray)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Frame buffer {name} has invalid texture type '{type}'. " +
                                         "Only Texture2D and Texture2DArray are supported as frame buffer attachments.");
        }

        ValidateTextureSlice(type, layers, mipLevels, attachment.Slice, name);
    }

    private void ValidateDepthStencilStateOp(DepthStencilStateOp stateOp, string name)
    {
        ValidateDefinedEnum(stateOp.StencilFailOp, $"stencil fail operation for {name}");

        ValidateDefinedEnum(stateOp.StencilDepthFailOp, $"stencil depth fail operation for {name}");

        ValidateDefinedEnum(stateOp.StencilPassOp, $"stencil pass operation for {name}");

        ValidateDefinedEnum(stateOp.StencilFunc, $"stencil comparison function for {name}");
    }

    private void ValidateBlendStateRenderTarget(BlendStateRenderTarget renderTarget, string name)
    {
        ValidateDefinedEnum(renderTarget.SrcBlend, $"{name} source color blend factor");

        ValidateDefinedEnum(renderTarget.DestBlend, $"{name} destination color blend factor");

        ValidateDefinedEnum(renderTarget.BlendOp, $"{name} color blend operation");

        ValidateDefinedEnum(renderTarget.SrcBlendAlpha, $"{name} source alpha blend factor");

        ValidateDefinedEnum(renderTarget.DestBlendAlpha, $"{name} destination alpha blend factor");

        ValidateDefinedEnum(renderTarget.BlendOpAlpha, $"{name} alpha blend operation");
    }

    private void ValidateShaders(Shader[]? shaders, string name)
    {
        if (shaders is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name} cannot be null. " +
                                         "Provide a valid array of shaders, even if empty.");

            return;
        }

        for (int i = 0; i < shaders.Length; i++)
        {
            ValidateShader(shaders[i], $"{name} at index {i}");
        }
    }

    private void ValidateResourceLayouts(ResourceLayout[]? resourceLayouts)
    {
        if (resourceLayouts is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Pipeline resource layouts array cannot be null. " +
                                         "Provide a valid array of resource layouts that define the pipeline's resource bindings.");

            return;
        }

        for (int i = 0; i < resourceLayouts.Length; i++)
        {
            ResourceLayout? resourceLayout = resourceLayouts[i];

            if (resourceLayout?.IsDisposed is not false)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             $"Resource layout at set index {i} is null or disposed. " +
                                             $"All resource layouts must be valid, non-null, and not disposed.");
            }
        }
    }

    private void ValidateOutput(Output output)
    {
        if (output.ColorAttachments is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Pipeline output color attachments array cannot be null. " +
                                         "Provide a valid array of color attachment formats, even if empty.");

            return;
        }

        if (output.ColorAttachments.Length is 0 && output.DepthStencilAttachment is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Pipeline output must have at least one render target. " +
                                         "Specify either one or more color attachment formats, or a depth-stencil format, or both.");

            return;
        }

        if (output.ColorAttachments.Length > 8)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Pipeline output has too many color attachments ({output.ColorAttachments.Length}). " +
                                         "Maximum supported color attachments is 8. This is a hardware limitation on most GPUs.");
        }

        if (output.DepthStencilAttachment is not null && !depthStencilFormats.Contains(output.DepthStencilAttachment.Value))
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Pipeline output depth-stencil format '{output.DepthStencilAttachment.Value}' is not supported. " +
                                         $"Supported depth-stencil formats are: {string.Join(", ", depthStencilFormats.Select(static item => item.ToString()))}.");
        }
    }

    #region Universal Validation
    private void ValidateDefinedEnum<TEnum>(TEnum value, string name) where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Invalid {name} value '{value}'. " +
                                         $"Valid values are: {string.Join(", ", Enum.GetNames<TEnum>())}.");
        }
    }

    private void ValidateShader(Shader? shader, string name)
    {
        if (shader?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name} is required but is null or disposed. " +
                                         "Ensure the shader is created and not disposed before using it in a pipeline.");
        }
    }
    #endregion

    #region Universal Texture Validation
    private void ObtainTextureValues(ITexture iTexture,
                                     out TextureType type,
                                     out uint layers,
                                     out uint mipLevels,
                                     string name)
    {
        if (iTexture is Texture texture)
        {
            type = texture.Desc.Type;
            layers = texture.Desc.Layers;
            mipLevels = texture.Desc.MipLevels;
        }
        else if (iTexture is TextureView textureView)
        {
            type = textureView.Desc.Texture.Desc.Type;
            layers = textureView.Desc.LayerCount;
            mipLevels = textureView.Desc.MipLevelCount;
        }
        else
        {
            type = default;
            layers = default;
            mipLevels = default;

            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Unexpected texture type in {name}. " +
                                         $"Expected Texture or TextureView, but got '{iTexture.GetType().Name}'.");
        }
    }

    private void ValidateTextureSlice(TextureType type,
                                      uint layers,
                                      uint mipLevels,
                                      TextureSlice slice,
                                      string name)
    {
        if (type is TextureType.TextureCube or TextureType.TextureCubeArray)
        {
            if (slice.Face >= 6)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             $"Invalid cube face index {slice.Face} for {name}. " +
                                             $"Cube textures have 6 faces (0-5). Valid range is 0 to 5.");
            }
        }
        else if (slice.Face is not 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Invalid face index {slice.Face} for {name} with texture type '{type}'. " +
                                         "Only TextureCube and TextureCubeArray support multiple faces. Use Face = 0 for other texture types.");
        }

        if (arrayTextureTypes.Contains(type))
        {
            if (slice.Layer >= layers)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             $"Invalid layer index {slice.Layer} for {name}. " +
                                             $"The texture has {layers} layers. Valid range is 0 to {layers - 1}.");
            }
        }
        else if (slice.Layer is not 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Invalid layer index {slice.Layer} for {name} with texture type '{type}'. " +
                                         "Only array texture types support multiple layers. Use Layer = 0 for non-array textures.");
        }

        if (slice.MipLevel >= mipLevels)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Invalid mip level {slice.MipLevel} for {name}. " +
                                         $"The texture has {mipLevels} mip levels. Valid range is 0 to {mipLevels - 1}.");
        }
    }
    #endregion
}