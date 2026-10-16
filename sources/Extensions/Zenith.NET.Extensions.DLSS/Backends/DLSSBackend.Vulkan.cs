using System.Runtime.InteropServices;
using NGX.NET;

namespace Zenith.NET.Extensions.DLSS;

internal unsafe partial class DLSSBackend
{
    private const uint ImageViewCreateInfoType = 15;
    private const uint PipelineLayoutCreateInfoType = 30;
    private const uint DescriptorSetLayoutCreateInfoType = 32;
    private const uint DescriptorPoolCreateInfoType = 33;
    private const uint DescriptorSetAllocateInfoType = 34;
    private const uint ImageViewType2D = 1;
    private const uint ComputeBindPoint = 1;

    private readonly nint descriptorSetLayout;
    private readonly nint pipelineLayout;
    private readonly nint descriptorPool;
    private readonly nint descriptorSet;
    private readonly Dictionary<nint, NGXResourceVK> resources = [];

    private delegate* unmanaged<nint, void*, void*, nint*, int> vkCreateImageView;
    private delegate* unmanaged<nint, nint, void*, void> vkDestroyImageView;
    private delegate* unmanaged<nint, nint, void*, void> vkDestroyDescriptorSetLayout;
    private delegate* unmanaged<nint, nint, void*, void> vkDestroyPipelineLayout;
    private delegate* unmanaged<nint, nint, void*, void> vkDestroyDescriptorPool;
    private delegate* unmanaged<nint, uint, nint, uint, uint, nint*, uint, uint*, void> vkCmdBindDescriptorSets;

    private void InitializeVulkan(byte* projectId, byte* engineVersion, void* dataPath, NGXFeatureCommonInfo* featureInfo)
    {
        Ngx.Vulkan.InitWithProjectID(projectId,
                                     NGXEngineType.Custom,
                                     engineVersion,
                                     dataPath,
                                     Context.GetNativeObject(NativeObjectType.VulkanInstance),
                                     Context.GetNativeObject(NativeObjectType.VulkanPhysicalDevice),
                                     Device,
                                     (delegate* unmanaged[Cdecl]<nint, byte*, delegate* unmanaged[Cdecl]<void>>)Context.GetNativeObject(NativeObjectType.VulkanGetInstanceProcAddr),
                                     (delegate* unmanaged[Cdecl]<nint, byte*, delegate* unmanaged[Cdecl]<void>>)Context.GetNativeObject(NativeObjectType.VulkanGetDeviceProcAddr),
                                     featureInfo,
                                     NGXVersion.Api).Success();

        delegate* unmanaged<nint, byte*, nint> getDeviceProcAddr = (delegate* unmanaged<nint, byte*, nint>)Context.GetNativeObject(NativeObjectType.VulkanGetDeviceProcAddr);

        delegate* unmanaged<nint, void*, void*, nint*, int> vkCreateDescriptorSetLayout = (delegate* unmanaged<nint, void*, void*, nint*, int>)GetProcedureAddress("vkCreateDescriptorSetLayout");
        delegate* unmanaged<nint, void*, void*, nint*, int> vkCreatePipelineLayout = (delegate* unmanaged<nint, void*, void*, nint*, int>)GetProcedureAddress("vkCreatePipelineLayout");
        delegate* unmanaged<nint, void*, void*, nint*, int> vkCreateDescriptorPool = (delegate* unmanaged<nint, void*, void*, nint*, int>)GetProcedureAddress("vkCreateDescriptorPool");
        delegate* unmanaged<nint, void*, nint*, int> vkAllocateDescriptorSets = (delegate* unmanaged<nint, void*, nint*, int>)GetProcedureAddress("vkAllocateDescriptorSets");

        fixed (nint* pDescriptorSetLayout = &descriptorSetLayout)
        {
            fixed (nint* pPipelineLayout = &pipelineLayout)
            {
                fixed (nint* pDescriptorPool = &descriptorPool)
                {
                    fixed (nint* pDescriptorSet = &descriptorSet)
                    {
                        VkDescriptorSetLayoutCreateInfo descriptorSetLayoutCreateInfo = new() { SType = DescriptorSetLayoutCreateInfoType };
                        vkCreateDescriptorSetLayout(Device, &descriptorSetLayoutCreateInfo, null, pDescriptorSetLayout).Success();

                        VkPipelineLayoutCreateInfo pipelineLayoutCreateInfo = new()
                        {
                            SType = PipelineLayoutCreateInfoType,
                            SetLayoutCount = 1,
                            PSetLayouts = pDescriptorSetLayout
                        };
                        vkCreatePipelineLayout(Device, &pipelineLayoutCreateInfo, null, pPipelineLayout).Success();

                        VkDescriptorPoolCreateInfo descriptorPoolCreateInfo = new()
                        {
                            SType = DescriptorPoolCreateInfoType,
                            MaxSets = 1
                        };
                        vkCreateDescriptorPool(Device, &descriptorPoolCreateInfo, null, pDescriptorPool).Success();

                        VkDescriptorSetAllocateInfo descriptorSetAllocateInfo = new()
                        {
                            SType = DescriptorSetAllocateInfoType,
                            DescriptorPool = descriptorPool,
                            DescriptorSetCount = 1,
                            PSetLayouts = pDescriptorSetLayout
                        };
                        vkAllocateDescriptorSets(Device, &descriptorSetAllocateInfo, pDescriptorSet).Success();
                    }
                }
            }
        }

        vkCreateImageView = (delegate* unmanaged<nint, void*, void*, nint*, int>)GetProcedureAddress("vkCreateImageView");
        vkDestroyImageView = (delegate* unmanaged<nint, nint, void*, void>)GetProcedureAddress("vkDestroyImageView");
        vkDestroyDescriptorSetLayout = (delegate* unmanaged<nint, nint, void*, void>)GetProcedureAddress("vkDestroyDescriptorSetLayout");
        vkDestroyPipelineLayout = (delegate* unmanaged<nint, nint, void*, void>)GetProcedureAddress("vkDestroyPipelineLayout");
        vkDestroyDescriptorPool = (delegate* unmanaged<nint, nint, void*, void>)GetProcedureAddress("vkDestroyDescriptorPool");
        vkCmdBindDescriptorSets = (delegate* unmanaged<nint, uint, nint, uint, uint, nint*, uint, uint*, void>)GetProcedureAddress("vkCmdBindDescriptorSets");

        nint GetProcedureAddress(string name)
        {
            using ZenithMarshal.Scope scope = new();

            return getDeviceProcAddr(Device, (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8));
        }
    }

    private void ShutdownVulkan()
    {
        foreach (NGXResourceVK resource in resources.Values)
        {
            DestroyVulkanResource(resource);
        }
        resources.Clear();

        vkDestroyDescriptorPool(Device, descriptorPool, null);
        vkDestroyPipelineLayout(Device, pipelineLayout, null);
        vkDestroyDescriptorSetLayout(Device, descriptorSetLayout, null);

        Ngx.Vulkan.Shutdown1(Device).Success();
    }

    private void CreateVulkan(nint commandList, DLSSSuperResolution feature, NGXDLSSCreateParams* createParams)
    {
        BindDescriptorSet(commandList);

        fixed (NGXHandle* handle = &feature.Handle)
        {
            Ngx.Vulkan.CreateDLSSExt1(Device, commandList, 1, 1, handle, feature.Parameters, createParams).Success();
        }
    }

    private void CreateVulkan(nint commandList, DLSSRayReconstruction feature, NGXDLSSDCreateParams* createParams)
    {
        BindDescriptorSet(commandList);

        fixed (NGXHandle* handle = &feature.Handle)
        {
            Ngx.Vulkan.CreateDLSSDExt1(Device, commandList, 1, 1, handle, feature.Parameters, createParams).Success();
        }
    }

    private void CreateVulkan(nint commandList, DLSSFrameGeneration feature, NGXDLSSGCreateParams* createParams)
    {
        BindDescriptorSet(commandList);

        fixed (NGXHandle* handle = &feature.Handle)
        {
            Ngx.Vulkan.CreateDLSSG(commandList, 1, 1, handle, feature.Parameters, createParams).Success();
        }
    }

    private void EvaluateVulkan(nint commandList, DLSSSuperResolution feature, DLSSSuperResolutionArgs args)
    {
        BindDescriptorSet(commandList);

        NGXResourceVK color = GetVulkanResource(args.Color) with { ReadWrite = false };
        NGXResourceVK depth = GetVulkanResource(args.Depth) with { ReadWrite = false };
        NGXResourceVK motionVectors = GetVulkanResource(args.MotionVectors) with { ReadWrite = false };
        NGXResourceVK output = GetVulkanResource(args.Output) with { ReadWrite = true };

        NGXVKDLSSEvalParams evalParams = new()
        {
            Feature = new()
            {
                Color = &color,
                Output = &output
            },
            Depth = &depth,
            MotionVectors = &motionVectors,
            JitterOffsetX = args.JitterOffset.X,
            JitterOffsetY = args.JitterOffset.Y,
            MVScaleX = args.MotionVectorScale.X,
            MVScaleY = args.MotionVectorScale.Y,
            PreExposure = args.PreExposure,
            RenderSubrectDimensions = new()
            {
                Width = args.Color.Desc.Width,
                Height = args.Color.Desc.Height
            },
            Reset = args.Reset ? 1 : 0
        };

        NGXResourceVK exposure;

        if (args.Exposure is not null)
        {
            exposure = GetVulkanResource(args.Exposure) with { ReadWrite = false };
            evalParams.ExposureTexture = &exposure;
        }

        Ngx.Vulkan.EvaluateDLSSExt(commandList, feature.Handle, feature.Parameters, &evalParams).Success();
    }

    private void EvaluateVulkan(nint commandList, DLSSRayReconstruction feature, DLSSRayReconstructionArgs args)
    {
        BindDescriptorSet(commandList);

        NGXResourceVK color = GetVulkanResource(args.Color) with { ReadWrite = false };
        NGXResourceVK depth = GetVulkanResource(args.Depth) with { ReadWrite = false };
        NGXResourceVK motionVectors = GetVulkanResource(args.MotionVectors) with { ReadWrite = false };
        NGXResourceVK diffuseAlbedo = GetVulkanResource(args.DiffuseAlbedo) with { ReadWrite = false };
        NGXResourceVK specularAlbedo = GetVulkanResource(args.SpecularAlbedo) with { ReadWrite = false };
        NGXResourceVK normals = GetVulkanResource(args.Normals) with { ReadWrite = false };
        NGXResourceVK roughness = GetVulkanResource(args.Roughness) with { ReadWrite = false };
        NGXResourceVK specularHitDistance = GetVulkanResource(args.SpecularHitDistance) with { ReadWrite = false };
        NGXResourceVK output = GetVulkanResource(args.Output) with { ReadWrite = true };

        NGXVKDLSSDEvalParams evalParams = new()
        {
            Color = &color,
            Depth = &depth,
            MotionVectors = &motionVectors,
            DiffuseAlbedo = &diffuseAlbedo,
            SpecularAlbedo = &specularAlbedo,
            Normals = &normals,
            Roughness = &roughness,
            SpecularHitDistance = &specularHitDistance,
            Output = &output,
            JitterOffsetX = args.JitterOffset.X,
            JitterOffsetY = args.JitterOffset.Y,
            MVScaleX = args.MotionVectorScale.X,
            MVScaleY = args.MotionVectorScale.Y,
            WorldToViewMatrix = &args.WorldToView,
            ViewToClipMatrix = &args.ViewToClip,
            PreExposure = args.PreExposure,
            RenderSubrectDimensions = new()
            {
                Width = args.Color.Desc.Width,
                Height = args.Color.Desc.Height
            },
            Reset = args.Reset ? 1 : 0
        };

        NGXResourceVK transparencyLayer;
        if (args.TransparencyLayer is not null)
        {
            transparencyLayer = GetVulkanResource(args.TransparencyLayer) with { ReadWrite = false };

            evalParams.TransparencyLayer = &transparencyLayer;
        }

        Ngx.Vulkan.EvaluateDLSSDExt(commandList, feature.Handle, feature.Parameters, &evalParams).Success();
    }

    private void EvaluateVulkan(nint commandList, DLSSFrameGeneration feature, DLSSFrameGenerationArgs args, NGXDLSSGOptEvalParams* optEvalParams)
    {
        BindDescriptorSet(commandList);

        NGXResourceVK color = GetVulkanResource(args.Color) with { ReadWrite = false };
        NGXResourceVK depth = GetVulkanResource(args.Depth) with { ReadWrite = false };
        NGXResourceVK motionVectors = GetVulkanResource(args.MotionVectors) with { ReadWrite = false };
        NGXResourceVK output = GetVulkanResource(args.Output) with { ReadWrite = true };

        NGXVKDLSSGEvalParams evalParams = new()
        {
            Backbuffer = &color,
            Depth = &depth,
            MVecs = &motionVectors,
            OutputInterpFrame = &output
        };

        NGXResourceVK outputDisableInterpolation;
        if (args.OutputDisableInterpolation is not null)
        {
            outputDisableInterpolation = new()
            {
                Resource = new()
                {
                    BufferInfo = new()
                    {
                        Buffer = args.OutputDisableInterpolation.GetNativeObject(NativeObjectType.VulkanBuffer),
                        SizeInBytes = args.OutputDisableInterpolation.Desc.SizeInBytes
                    }
                },
                Type = NGXResourceVKType.VkBuffer,
                ReadWrite = true
            };

            evalParams.OutputDisableInterpolation = &outputDisableInterpolation;
        }

        Ngx.Vulkan.EvaluateDLSSG(commandList, feature.Handle, feature.Parameters, &evalParams, optEvalParams).Success();
    }

    private void BindDescriptorSet(nint commandList)
    {
        fixed (nint* pDescriptorSet = &descriptorSet)
        {
            vkCmdBindDescriptorSets(commandList, ComputeBindPoint, pipelineLayout, 0, 1, pDescriptorSet, 0, null);
        }
    }

    private NGXResourceVK GetVulkanResource(Texture texture)
    {
        nint image = texture.GetNativeObject(NativeObjectType.VulkanImage);

        if (!resources.TryGetValue(image, out NGXResourceVK resource))
        {
            (uint format, uint aspectFlags) = DLSSFormats.Vulkan(texture.Desc.Format);

            NGXVkImageSubresourceRange subresourceRange = new()
            {
                AspectMask = aspectFlags,
                LevelCount = 1,
                LayerCount = 1
            };

            VkImageViewCreateInfo createInfo = new()
            {
                SType = ImageViewCreateInfoType,
                Image = image,
                ViewType = ImageViewType2D,
                Format = format,
                SubresourceRange = subresourceRange
            };

            nint view;
            vkCreateImageView(Device, &createInfo, null, &view).Success();

            resources[image] = resource = new()
            {
                Resource = new()
                {
                    ImageViewInfo = new()
                    {
                        ImageView = view,
                        Image = image,
                        SubresourceRange = subresourceRange,
                        Format = (NGXVkFormat)format,
                        Width = texture.Desc.Width,
                        Height = texture.Desc.Height
                    }
                },
                Type = NGXResourceVKType.VkImageView
            };

            texture.Disposing += (_, _) =>
            {
                if (resources.Remove(image, out NGXResourceVK disposedResource))
                {
                    DestroyVulkanResource(disposedResource);
                }
            };
        }

        return resource;
    }

    private void DestroyVulkanResource(NGXResourceVK resource)
    {
        vkDestroyImageView(Device, resource.Resource.ImageViewInfo.ImageView, null);
    }
}

[StructLayout(LayoutKind.Sequential)]
file unsafe struct VkImageViewCreateInfo
{
    public uint SType;

    public void* PNext;

    public uint Flags;

    public nint Image;

    public uint ViewType;

    public uint Format;

    public fixed uint Components[4];

    public NGXVkImageSubresourceRange SubresourceRange;
}

[StructLayout(LayoutKind.Sequential)]
file unsafe struct VkDescriptorSetLayoutCreateInfo
{
    public uint SType;

    public void* PNext;

    public uint Flags;

    public uint BindingCount;

    public void* PBindings;
}

[StructLayout(LayoutKind.Sequential)]
file unsafe struct VkPipelineLayoutCreateInfo
{
    public uint SType;

    public void* PNext;

    public uint Flags;

    public uint SetLayoutCount;

    public nint* PSetLayouts;

    public uint PushConstantRangeCount;

    public void* PPushConstantRanges;
}

[StructLayout(LayoutKind.Sequential)]
file unsafe struct VkDescriptorPoolCreateInfo
{
    public uint SType;

    public void* PNext;

    public uint Flags;

    public uint MaxSets;

    public uint PoolSizeCount;

    public void* PPoolSizes;
}

[StructLayout(LayoutKind.Sequential)]
file unsafe struct VkDescriptorSetAllocateInfo
{
    public uint SType;

    public void* PNext;

    public nint DescriptorPool;

    public uint DescriptorSetCount;

    public nint* PSetLayouts;
}
