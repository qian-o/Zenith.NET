using System.Runtime.InteropServices;

namespace Zenith.NET;

public abstract class ValidationLayer(GraphicsContext context) : GraphicsResource(context)
{
    internal void ValidateDesc(SwapChainDesc desc)
    {
        if (desc.Surface.Handles is null)
        {
            Report(MessageSeverity.Error, "SwapChainDesc.Surface must have valid handles.");

            return;
        }

        switch (desc.Surface.Type)
        {
            case SurfaceType.Win32:
                if (desc.Surface.Handles.Length is not 1)
                {
                    Report(MessageSeverity.Error, "SwapChainDesc.Surface.Handles must have exactly one handle for SurfaceType.Win32.");
                }
                break;

            case SurfaceType.Wayland:
                if (desc.Surface.Handles.Length is not 2)
                {
                    Report(MessageSeverity.Error, "SwapChainDesc.Surface.Handles must have exactly two handles for SurfaceType.Wayland.");
                }
                break;

            case SurfaceType.Xlib:
                if (desc.Surface.Handles.Length is not 2)
                {
                    Report(MessageSeverity.Error, "SwapChainDesc.Surface.Handles must have exactly two handles for SurfaceType.Xlib.");
                }
                break;

            case SurfaceType.Android:
                if (desc.Surface.Handles.Length is not 1)
                {
                    Report(MessageSeverity.Error, "SwapChainDesc.Surface.Handles must have exactly one handle for SurfaceType.Android.");
                }
                break;

            case SurfaceType.IOS:
                if (desc.Surface.Handles.Length is not 1)
                {
                    Report(MessageSeverity.Error, "SwapChainDesc.Surface.Handles must have exactly one handle for SurfaceType.IOS.");
                }
                break;

            case SurfaceType.MacOS:
                if (desc.Surface.Handles.Length is not 1)
                {
                    Report(MessageSeverity.Error, "SwapChainDesc.Surface.Handles must have exactly one handle for SurfaceType.MacOS.");
                }
                break;

            case SurfaceType.PixelBuffer:
                if (desc.Surface.Handles.Length is not 1)
                {
                    Report(MessageSeverity.Error, "SwapChainDesc.Surface.Handles must have exactly one handle for SurfaceType.PixelBuffer.");
                }

                if (GCHandle.FromIntPtr(desc.Surface.Handles[0]).Target is not PixelBuffer)
                {
                    Report(MessageSeverity.Error, "SwapChainDesc.Surface.Handles[0] must be a valid PixelBuffer handle for SurfaceType.PixelBuffer.");
                }
                break;

            default:
                Report(MessageSeverity.Error, $"SwapChainDesc.Surface has unsupported SurfaceType '{desc.Surface.Type}'.");
                break;
        }

        if (!Enum.IsDefined(desc.ColorTargetFormat))
        {
            Report(MessageSeverity.Error, $"SwapChainDesc.ColorTargetFormat has an invalid value '{desc.ColorTargetFormat}'.");
        }

        if (desc.DepthStencilTargetFormat is not null && !Enum.IsDefined(desc.DepthStencilTargetFormat.Value))
        {
            Report(MessageSeverity.Error, $"SwapChainDesc.DepthStencilTargetFormat has an invalid value '{desc.DepthStencilTargetFormat.Value}'.");
        }
    }

    internal void ValidateDesc(FrameBufferDesc desc)
    {
        if (desc.ColorTargets is null)
        {
            Report(MessageSeverity.Error, "FrameBufferDesc.ColorTargets must not be null.");

            return;
        }

        foreach (FrameBufferAttachment attachment in desc.ColorTargets)
        {
            CheckFrameBufferAttachment(attachment);
        }

        if (desc.DepthStencilTarget is not null)
        {
            CheckFrameBufferAttachment(desc.DepthStencilTarget.Value);
        }

        if (desc.ColorTargets.Length is 0 && desc.DepthStencilTarget is null)
        {
            Report(MessageSeverity.Warning, "FrameBufferDesc has no attachments.");
        }

        void CheckFrameBufferAttachment(FrameBufferAttachment attachment)
        {
            if (attachment.Target is null)
            {
                Report(MessageSeverity.Error, "FrameBufferAttachment.Target must not be null.");

                return;
            }

            if (attachment.Slice.Face is >= 6)
            {
                Report(MessageSeverity.Error, "FrameBufferAttachment.Slice.Face must be less than 6.");
            }

            if (attachment.Slice.Layer >= attachment.Target.Desc.Layers)
            {
                Report(MessageSeverity.Error, "FrameBufferAttachment.Slice.Layer must be less than the number of layers in the texture.");
            }

            if (attachment.Slice.MipLevel >= attachment.Target.Desc.MipLevels)
            {
                Report(MessageSeverity.Error, "FrameBufferAttachment.Slice.MipLevel must be less than the number of mip levels in the texture.");
            }
        }
    }

    internal void ValidateDesc(ShaderDesc desc)
    {
        if (desc.ShaderBytes is null || desc.ShaderBytes.Length is 0)
        {
            Report(MessageSeverity.Error, "ShaderDesc.ShaderBytes must not be null or empty.");
        }

        if (string.IsNullOrWhiteSpace(desc.EntryPoint))
        {
            Report(MessageSeverity.Error, "ShaderDesc.EntryPoint must not be null or whitespace.");
        }

        if (!Enum.IsDefined(desc.Stage))
        {
            Report(MessageSeverity.Error, $"ShaderDesc.Stage has an invalid value '{desc.Stage}'.");
        }
    }

    internal void ValidateDesc(BufferDesc desc)
    {
        if (desc.SizeInBytes is 0)
        {
            Report(MessageSeverity.Error, "BufferDesc.SizeInBytes must be greater than zero.");
        }

        if (desc.StrideInBytes is 0)
        {
            Report(MessageSeverity.Warning, "BufferDesc.StrideInBytes is zero, which may be valid for some buffer types but could indicate an issue.");
        }

        if (desc.Flags is BufferUsageFlags.None)
        {
            Report(MessageSeverity.Warning, "BufferDesc.Flags is set to None, which may be valid but could indicate an issue.");
        }
    }

    internal void ValidateDesc(BufferViewDesc desc)
    {
        if (desc.Buffer is null)
        {
            Report(MessageSeverity.Error, "BufferViewDesc.Buffer must not be null.");

            return;
        }

        if (desc.OffsetInBytes >= desc.Buffer.Desc.SizeInBytes)
        {
            Report(MessageSeverity.Error, "BufferViewDesc.OffsetInBytes must be less than the size of the buffer.");
        }

        if (desc.SizeInBytes is 0 || desc.OffsetInBytes + desc.SizeInBytes > desc.Buffer.Desc.SizeInBytes)
        {
            Report(MessageSeverity.Error, "BufferViewDesc.SizeInBytes must be greater than zero and within the bounds of the buffer.");
        }

        if (desc.StrideInBytes is 0)
        {
            Report(MessageSeverity.Warning, "BufferViewDesc.StrideInBytes is zero, which may be valid for some buffer views but could indicate an issue.");
        }
    }

    internal void ValidateDesc(TextureDesc desc)
    {
        if (!Enum.IsDefined(desc.Type))
        {
            Report(MessageSeverity.Error, $"TextureDesc.Type has an invalid value '{desc.Type}'.");
        }

        if (!Enum.IsDefined(desc.Format))
        {
            Report(MessageSeverity.Error, $"TextureDesc.Format has an invalid value '{desc.Format}'.");
        }

        if (desc.Width is 0 || desc.Height is 0 || desc.Depth is 0)
        {
            Report(MessageSeverity.Error, "TextureDesc dimensions (Width, Height, Depth) must be greater than zero.");
        }

        if (desc.Layers is 0)
        {
            Report(MessageSeverity.Error, "TextureDesc.Layers must be greater than zero.");
        }

        if (desc.MipLevels is 0)
        {
            Report(MessageSeverity.Error, "TextureDesc.MipLevels must be greater than zero.");
        }

        if (!Enum.IsDefined(desc.SampleCount))
        {
            Report(MessageSeverity.Error, $"TextureDesc.SampleCount has an invalid value '{desc.SampleCount}'.");
        }

        if (desc.Flags is TextureUsageFlags.None)
        {
            Report(MessageSeverity.Warning, "TextureDesc.Flags is set to None, which may be valid but could indicate an issue.");
        }
    }

    internal void ValidateDesc(TextureViewDesc desc)
    {
        if (desc.Texture is null)
        {
            Report(MessageSeverity.Error, "TextureViewDesc.Texture must not be null.");

            return;
        }

        if (desc.FirstLayer >= desc.Texture.Desc.Layers)
        {
            Report(MessageSeverity.Error, "TextureViewDesc.FirstLayer must be less than the number of layers in the texture.");
        }

        if (desc.LayerCount is 0 || desc.FirstLayer + desc.LayerCount > desc.Texture.Desc.Layers)
        {
            Report(MessageSeverity.Error, "TextureViewDesc.LayerCount must be greater than zero and within the bounds of the texture layers.");
        }

        if (desc.FirstMipLevel >= desc.Texture.Desc.MipLevels)
        {
            Report(MessageSeverity.Error, "TextureViewDesc.FirstMipLevel must be less than the number of mip levels in the texture.");
        }

        if (desc.MipLevelCount is 0 || desc.FirstMipLevel + desc.MipLevelCount > desc.Texture.Desc.MipLevels)
        {
            Report(MessageSeverity.Error, "TextureViewDesc.MipLevelCount must be greater than zero and within the bounds of the texture mip levels.");
        }
    }

    internal void ValidateDesc(SamplerDesc desc)
    {
        if (!Enum.IsDefined(desc.U))
        {
            Report(MessageSeverity.Error, $"SamplerDesc.U has an invalid value '{desc.U}'.");
        }

        if (!Enum.IsDefined(desc.V))
        {
            Report(MessageSeverity.Error, $"SamplerDesc.V has an invalid value '{desc.V}'.");
        }

        if (!Enum.IsDefined(desc.W))
        {
            Report(MessageSeverity.Error, $"SamplerDesc.W has an invalid value '{desc.W}'.");
        }

        if (!Enum.IsDefined(desc.Filter))
        {
            Report(MessageSeverity.Error, $"SamplerDesc.Filter has an invalid value '{desc.Filter}'.");
        }

        if (!Enum.IsDefined(desc.ComparisonFunc))
        {
            Report(MessageSeverity.Error, $"SamplerDesc.ComparisonFunc has an invalid value '{desc.ComparisonFunc}'.");
        }

        if (desc.MinLod > desc.MaxLod)
        {
            Report(MessageSeverity.Error, "SamplerDesc.MinLod must be less than or equal to MaxLod.");
        }

        if (!Enum.IsDefined(desc.BorderColor))
        {
            Report(MessageSeverity.Error, $"SamplerDesc.BorderColor has an invalid value '{desc.BorderColor}'.");
        }
    }

    internal void ValidateDesc(ResourceLayoutDesc desc)
    {
        if (desc.Bindings is null)
        {
            Report(MessageSeverity.Error, "ResourceLayoutDesc.Bindings must not be null.");

            return;
        }

        foreach (ResourceBinding binding in desc.Bindings)
        {
            if (!Enum.IsDefined(binding.Type))
            {
                Report(MessageSeverity.Error, $"ResourceLayoutBinding.Type has an invalid value '{binding.Type}'.");
            }
        }

        if (desc.Bindings.Length is 0)
        {
            Report(MessageSeverity.Warning, "ResourceLayoutDesc has no bindings.");
        }
    }

    internal void ValidateDesc(ResourceSetDesc desc)
    {
        if (desc.Layout is null)
        {
            Report(MessageSeverity.Error, "ResourceSetDesc.Layout must not be null.");

            return;
        }

        if (desc.Resources is null)
        {
            Report(MessageSeverity.Error, "ResourceSetDesc.Resources must not be null.");

            return;
        }

        if (desc.Resources.Length != desc.Layout.Desc.Bindings.Length)
        {
            Report(MessageSeverity.Error, "ResourceSetDesc.Resources length must match the number of bindings in the layout.");

            return;
        }

        int i = 0;
        foreach (IBindableResource resource in desc.Resources)
        {
            switch (desc.Layout.Desc.Bindings[i++].Type)
            {
                case ResourceType.ConstantBuffer:
                    if (resource is not Buffer or BufferView)
                    {
                        Report(MessageSeverity.Error, "ResourceSetDesc.Resources item must be a Buffer or BufferView for ConstantBuffer binding.");
                    }
                    break;

                case ResourceType.StructuredBuffer:
                    if (resource is not Buffer or BufferView)
                    {
                        Report(MessageSeverity.Error, "ResourceSetDesc.Resources item must be a Buffer or BufferView for StructuredBuffer binding.");
                    }
                    break;

                case ResourceType.StructuredBufferReadWrite:
                    if (resource is not Buffer or BufferView)
                    {
                        Report(MessageSeverity.Error, "ResourceSetDesc.Resources item must be a Buffer or BufferView for StructuredBufferReadWrite binding.");
                    }
                    break;

                case ResourceType.Texture:
                    if (resource is not Texture or TextureView)
                    {
                        Report(MessageSeverity.Error, "ResourceSetDesc.Resources item must be a Texture or TextureView for Texture binding.");
                    }
                    break;

                case ResourceType.TextureReadWrite:
                    if (resource is not Texture or TextureView)
                    {
                        Report(MessageSeverity.Error, "ResourceSetDesc.Resources item must be a Texture or TextureView for TextureReadWrite binding.");
                    }
                    break;

                case ResourceType.Sampler:
                    if (resource is not Sampler)
                    {
                        Report(MessageSeverity.Error, "ResourceSetDesc.Resources item must be a Sampler for Sampler binding.");
                    }
                    break;

                case ResourceType.AccelerationStructure:
                    if (resource is not TopLevelAccelerationStructure)
                    {
                        Report(MessageSeverity.Error, "ResourceSetDesc.Resources item must be a TopLevelAccelerationStructure for AccelerationStructure binding.");
                    }
                    break;
            }
        }
    }

    internal void ValidateDesc(GraphicsPipelineDesc desc)
    {
        throw new NotImplementedException();
    }

    internal void ValidateDesc(ComputePipelineDesc desc)
    {
        throw new NotImplementedException();
    }

    internal void ValidateDesc(RayTracingPipelineDesc desc)
    {
        throw new NotImplementedException();
    }

    internal void ValidateDesc(MeshShadingPipelineDesc desc)
    {
        throw new NotImplementedException();
    }

    internal void ValidateDesc(BottomLevelAccelerationStructureDesc desc)
    {
        throw new NotImplementedException();
    }

    internal void ValidateDesc(TopLevelAccelerationStructureDesc desc)
    {
        throw new NotImplementedException();
    }

    internal void ValidateDesc(TopLevelAccelerationStructureDesc oldDesc, TopLevelAccelerationStructureDesc newDesc)
    {
        throw new NotImplementedException();
    }

    private void Report(MessageSeverity severity, string message)
    {
        Context.OnValidationMessage(new(MessageSource.Framework, severity, message));
    }
}
