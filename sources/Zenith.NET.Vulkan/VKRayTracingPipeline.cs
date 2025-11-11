using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKRayTracingPipeline : RayTracingPipeline
{
    public PipelineLayout PipelineLayout;

    public VkPipeline Pipeline;

    public StridedDeviceAddressRegionKHR RayGenerationRegion;

    public StridedDeviceAddressRegionKHR MissRegion;

    public StridedDeviceAddressRegionKHR HitGroupsRegion;

    public VKRayTracingPipeline(VKGraphicsContext context, RayTracingPipelineDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        Shader[] shaders = [desc.RayGeneration, .. desc.Miss, .. desc.AnyHit, .. desc.Intersection, .. desc.ClosestHit];

        PipelineShaderStageCreateInfo* stages = (PipelineShaderStageCreateInfo*)ZenithMarshal.Allocate<PipelineShaderStageCreateInfo>(scope, (uint)shaders.Length);
        for (int i = 0; i < shaders.Length; i++)
        {
            stages[i] = shaders[i].Vulkan().GetPipelineShaderStageCreateInfo(scope);
        }

        uint groupCount = 1 + (uint)desc.Miss.Length + (uint)desc.HitGroups.Length;
        string[] entryPoints = [.. shaders.Select(static item => item.Desc.EntryPoint)];

        RayTracingShaderGroupCreateInfoKHR* groups = (RayTracingShaderGroupCreateInfoKHR*)ZenithMarshal.Allocate<RayTracingShaderGroupCreateInfoKHR>(scope, groupCount);
        for (uint i = 0; i < groupCount; i++)
        {
            RayTracingShaderGroupTypeKHR type = RayTracingShaderGroupTypeKHR.GeneralKhr;
            uint generalShader = Vk.ShaderUnusedKhr;
            uint closestHitShader = Vk.ShaderUnusedKhr;
            uint anyHitShader = Vk.ShaderUnusedKhr;
            uint intersectionShader = Vk.ShaderUnusedKhr;

            if (i <= desc.Miss.Length)
            {
                generalShader = i;
            }
            else
            {
                HitGroup hitGroup = desc.HitGroups[i - desc.Miss.Length - 1];

                type = VKFormats.Vulkan(hitGroup.Type);

                if (hitGroup.ClosestHit is not null)
                {
                    closestHitShader = (uint)Array.IndexOf(entryPoints, hitGroup.ClosestHit);
                }

                if (hitGroup.AnyHit is not null)
                {
                    anyHitShader = (uint)Array.IndexOf(entryPoints, hitGroup.AnyHit);
                }

                if (hitGroup.Intersection is not null)
                {
                    intersectionShader = (uint)Array.IndexOf(entryPoints, hitGroup.Intersection);
                }
            }

            groups[i] = new()
            {
                SType = StructureType.RayTracingShaderGroupCreateInfoKhr,
                Type = type,
                GeneralShader = generalShader,
                ClosestHitShader = closestHitShader,
                AnyHitShader = anyHitShader,
                IntersectionShader = intersectionShader
            };
        }

        RayTracingPipelineCreateInfoKHR createInfo = new()
        {
            SType = StructureType.RayTracingPipelineCreateInfoKhr,
            MaxPipelineRayRecursionDepth = desc.MaxTraceRecursionDepth,
            StageCount = (uint)shaders.Length,
            PStages = stages,
            GroupCount = groupCount,
            PGroups = groups
        };

        // ResourceLayouts
        {
            DescriptorSetLayout* setLayouts = (DescriptorSetLayout*)ZenithMarshal.Allocate<DescriptorSetLayout>(scope, (uint)desc.ResourceLayouts.Length);
            for (int i = 0; i < desc.ResourceLayouts.Length; i++)
            {
                setLayouts[i] = desc.ResourceLayouts[i].Vulkan().DescriptorSetLayout;
            }

            PipelineLayoutCreateInfo pipelineLayoutCreateInfo = new()
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = (uint)desc.ResourceLayouts.Length,
                PSetLayouts = setLayouts
            };

            context.Vk.CreatePipelineLayout(context.Device, &pipelineLayoutCreateInfo, null, out PipelineLayout).Success();

            createInfo.Layout = PipelineLayout;
        }

        context.RayTracingPipeline?.CreateRayTracingPipelines(context.Device, default, default, 1, &createInfo, null, out Pipeline).Success();

        const uint HandleSize = 32;
        const uint HandleSizeAligned = 64;

        uint shaderHandleStorageSize = HandleSize * groupCount;
        byte* shaderHandleStorage = (byte*)ZenithMarshal.Allocate<byte>(scope, shaderHandleStorageSize);

        context.RayTracingPipeline?.GetRayTracingShaderGroupHandles(context.Device, Pipeline, 0, groupCount, shaderHandleStorageSize, shaderHandleStorage).Success();

        RayGenerationBuffer = CreateSectionBuffer(1);
        MissBuffer = CreateSectionBuffer((uint)desc.Miss.Length);
        HitGroupsBuffer = CreateSectionBuffer((uint)desc.HitGroups.Length);

        RayGenerationRegion = new()
        {
            DeviceAddress = RayGenerationBuffer.DeviceAddress,
            Stride = HandleSizeAligned,
            Size = RayGenerationBuffer.Desc.SizeInBytes
        };

        MissRegion = new()
        {
            DeviceAddress = MissBuffer.DeviceAddress,
            Stride = HandleSizeAligned,
            Size = MissBuffer.Desc.SizeInBytes
        };

        HitGroupsRegion = new()
        {
            DeviceAddress = HitGroupsBuffer.DeviceAddress,
            Stride = HandleSizeAligned,
            Size = HitGroupsBuffer.Desc.SizeInBytes
        };

        VKBuffer CreateSectionBuffer(uint count)
        {
            BufferDesc bufferDesc = new()
            {
                SizeInBytes = HandleSizeAligned * count,
                StrideInBytes = HandleSizeAligned,
                Flags = BufferUsageFlags.Dynamic
            };

            VKBuffer buffer = new(context, bufferDesc, VkBufferUsageFlags.ShaderBindingTableBitKhr);

            MappedMemory mappedMemory = buffer.Map();

            for (uint i = 0; i < count; i++)
            {
                Unsafe.CopyBlock((byte*)(mappedMemory.Pointer + (i * HandleSizeAligned)), shaderHandleStorage, HandleSize);

                shaderHandleStorage += HandleSize;
            }

            buffer.Unmap();

            return buffer;
        }
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public VKBuffer RayGenerationBuffer { get; }

    public VKBuffer MissBuffer { get; }

    public VKBuffer HitGroupsBuffer { get; }

    protected override void SetResourceName(string name)
    {
        using ZenithMarshal.Scope scope = new();

        DebugUtilsObjectNameInfoEXT nameInfo = new()
        {
            SType = StructureType.DebugUtilsObjectNameInfoExt,
            ObjectType = ObjectType.Pipeline,
            ObjectHandle = Pipeline.Handle,
            PObjectName = (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8)
        };

        Context.DebugUtils?.SetDebugUtilsObjectName(Context.Device, &nameInfo).Success();
    }

    protected override void Destroy()
    {
        HitGroupsBuffer.Dispose();
        MissBuffer.Dispose();
        RayGenerationBuffer.Dispose();

        Context.Vk.DestroyPipeline(Context.Device, Pipeline, null);
        Context.Vk.DestroyPipelineLayout(Context.Device, PipelineLayout, null);
    }
}
