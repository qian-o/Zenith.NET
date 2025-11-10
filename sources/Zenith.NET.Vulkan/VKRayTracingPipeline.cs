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

        uint groupCount = 1 + (uint)desc.Miss.Length + (uint)desc.HitGroups.Length;

        RayTracingPipelineCreateInfoKHR createInfo = new()
        {
            SType = StructureType.RayTracingPipelineCreateInfoKhr,
            MaxPipelineRayRecursionDepth = desc.MaxTraceRecursionDepth
        };

        // RayGeneration - Miss - AnyHit - Intersection - ClosestHit - HitGroups
        {
            Shader[] shaders =
            [
                desc.RayGeneration,
                .. desc.Miss,
                .. desc.AnyHit,
                .. desc.Intersection,
                .. desc.ClosestHit
            ];

            string[] entryPoints = [.. shaders.Select(static item => item.Desc.EntryPoint)];

            PipelineShaderStageCreateInfo* stages = (PipelineShaderStageCreateInfo*)ZenithMarshal.Allocate<PipelineShaderStageCreateInfo>(scope, (uint)shaders.Length);
            for (int i = 0; i < shaders.Length; i++)
            {
                stages[i] = shaders[i].Vulkan().GetPipelineShaderStageCreateInfo(scope);
            }

            RayTracingShaderGroupCreateInfoKHR* groups = (RayTracingShaderGroupCreateInfoKHR*)ZenithMarshal.Allocate<RayTracingShaderGroupCreateInfoKHR>(scope, groupCount);

            uint index = 0;
            groups[index++] = new()
            {
                SType = StructureType.RayTracingShaderGroupCreateInfoKhr,
                Type = RayTracingShaderGroupTypeKHR.GeneralKhr,
                GeneralShader = 0,
                ClosestHitShader = Vk.ShaderUnusedKhr,
                AnyHitShader = Vk.ShaderUnusedKhr,
                IntersectionShader = Vk.ShaderUnusedKhr
            };

            for (int i = 0; i < desc.Miss.Length; i++)
            {
                groups[index++] = new()
                {
                    SType = StructureType.RayTracingShaderGroupCreateInfoKhr,
                    Type = RayTracingShaderGroupTypeKHR.GeneralKhr,
                    GeneralShader = (uint)(i + 1),
                    ClosestHitShader = Vk.ShaderUnusedKhr,
                    AnyHitShader = Vk.ShaderUnusedKhr,
                    IntersectionShader = Vk.ShaderUnusedKhr
                };
            }

            foreach (HitGroup hitGroup in desc.HitGroups)
            {
                groups[index++] = new()
                {
                    SType = StructureType.RayTracingShaderGroupCreateInfoKhr,
                    Type = VKFormats.Vulkan(hitGroup.Type),
                    GeneralShader = Vk.ShaderUnusedKhr,
                    ClosestHitShader = hitGroup.ClosestHit is not null ? (uint)Array.IndexOf(entryPoints, hitGroup.ClosestHit) : Vk.ShaderUnusedKhr,
                    AnyHitShader = hitGroup.AnyHit is not null ? (uint)Array.IndexOf(entryPoints, hitGroup.AnyHit) : Vk.ShaderUnusedKhr,
                    IntersectionShader = hitGroup.Intersection is not null ? (uint)Array.IndexOf(entryPoints, hitGroup.Intersection) : Vk.ShaderUnusedKhr
                };
            }

            createInfo.StageCount = (uint)shaders.Length;
            createInfo.PStages = stages;
            createInfo.GroupCount = groupCount;
            createInfo.PGroups = groups;
        }

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

            context.Vk.CreatePipelineLayout(context.Device, &pipelineLayoutCreateInfo, null, (PipelineLayout*)Unsafe.AsPointer(ref PipelineLayout)).Success();

            createInfo.Layout = PipelineLayout;
        }

        context.RayTracingPipeline?.CreateRayTracingPipelines(context.Device, default, default, 1, &createInfo, null, (VkPipeline*)Unsafe.AsPointer(ref Pipeline)).Success();

        // Shader Binding Tables
        {
            const uint HandleSize = 32;
            const uint HandleSizeAligned = 64;

            uint shaderHandleStorageSize = HandleSize * groupCount;
            byte* shaderHandleStorage = (byte*)ZenithMarshal.Allocate<byte>(scope, shaderHandleStorageSize);

            context.RayTracingPipeline?.GetRayTracingShaderGroupHandles(context.Device, Pipeline, 0, groupCount, shaderHandleStorageSize, shaderHandleStorage).Success();

            RayGenerationBuffer = new(context, new() { SizeInBytes = HandleSizeAligned, StrideInBytes = HandleSizeAligned, Flags = BufferUsageFlags.Dynamic }, VkBufferUsageFlags.ShaderBindingTableBitKhr);
            MissBuffer = new(context, new() { SizeInBytes = HandleSizeAligned * (uint)desc.Miss.Length, StrideInBytes = HandleSizeAligned, Flags = BufferUsageFlags.Dynamic }, VkBufferUsageFlags.ShaderBindingTableBitKhr);
            HitGroupsBuffer = new(context, new() { SizeInBytes = HandleSizeAligned * (uint)desc.HitGroups.Length, StrideInBytes = HandleSizeAligned, Flags = BufferUsageFlags.Dynamic }, VkBufferUsageFlags.ShaderBindingTableBitKhr);

            CopyHandles(RayGenerationBuffer, 1);
            CopyHandles(MissBuffer, (uint)desc.Miss.Length);
            CopyHandles(HitGroupsBuffer, (uint)desc.HitGroups.Length);

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

            void CopyHandles(VKBuffer buffer, uint count)
            {
                MappedMemory mappedMemory = buffer.Map();

                for (uint i = 0; i < count; i++)
                {
                    Unsafe.CopyBlock((byte*)(mappedMemory.Pointer + (i * HandleSizeAligned)), shaderHandleStorage, HandleSize);

                    shaderHandleStorage += HandleSize;
                }

                buffer.Unmap();
            }
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
