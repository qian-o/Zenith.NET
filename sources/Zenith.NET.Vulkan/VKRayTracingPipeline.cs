using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKRayTracingPipeline : RayTracingPipeline
{
    public PipelineLayout PipelineLayout;

    public VkPipeline Pipeline;

    public VKRayTracingPipeline(VKGraphicsContext context, RayTracingPipelineDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

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

            PipelineShaderStageCreateInfo* stages = (PipelineShaderStageCreateInfo*)ZenithMarshal.Allocate<PipelineShaderStageCreateInfo>(scope, (uint)shaders.Length);
            for (int i = 0; i < shaders.Length; i++)
            {
                stages[i] = shaders[i].Vulkan().GetPipelineShaderStageCreateInfo(scope);
            }

            RayTracingShaderGroupCreateInfoKHR* groups = (RayTracingShaderGroupCreateInfoKHR*)ZenithMarshal.Allocate<RayTracingShaderGroupCreateInfoKHR>(scope, (uint)(1 + desc.Miss.Length + desc.HitGroups.Length));

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
                groups[index] = new()
                {
                    SType = StructureType.RayTracingShaderGroupCreateInfoKhr,
                    Type = RayTracingShaderGroupTypeKHR.GeneralKhr,
                    GeneralShader = index,
                    ClosestHitShader = Vk.ShaderUnusedKhr,
                    AnyHitShader = Vk.ShaderUnusedKhr,
                    IntersectionShader = Vk.ShaderUnusedKhr
                };

                index++;
            }

            string[] entryPoints = [.. shaders.Select(static item => item.Desc.EntryPoint)];

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
            createInfo.GroupCount = (uint)(1 + desc.Miss.Length + desc.HitGroups.Length);
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
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

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
        Context.Vk.DestroyPipeline(Context.Device, Pipeline, null);
        Context.Vk.DestroyPipelineLayout(Context.Device, PipelineLayout, null);
    }
}
