using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKShader(VKGraphicsContext context, ShaderDesc desc) : Shader(context, desc)
{
    public PipelineShaderStageCreateInfo GetPipelineShaderStageCreateInfo(ZenithMarshal.Scope scope)
    {
        ShaderModuleCreateInfo createInfo = new()
        {
            SType = StructureType.ShaderModuleCreateInfo,
            CodeSize = (uint)Desc.ShaderBytes.Length,
            PCode = (uint*)ZenithMarshal.AllocateAndFill(scope, Desc.ShaderBytes)
        };

        return new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = VKFormats.Vulkan(Desc.Stage),
            PName = (byte*)ZenithMarshal.StringToPointer(scope, Desc.EntryPoint, StringEncoding.UTF8),
            PNext = (ShaderModuleCreateInfo*)ZenithMarshal.AllocateAndFill(scope, [createInfo])
        };
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
    }
}
