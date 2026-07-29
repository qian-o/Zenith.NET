using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKShader(VKGraphicsContext context, ShaderDesc desc) : Shader(context, desc)
{
    public PipelineShaderStageCreateInfo GetPipelineShaderStageCreateInfo(ZenithMarshal.Scope scope, ShaderStageFlags stage)
    {
        DescriptorSetAndBindingMappingEXT mapping = new()
        {
            SType = StructureType.DescriptorSetAndBindingMappingExt(),
            BindingCount = 1,
            ResourceMask = SpirvResourceTypeFlagsEXT.UniformBufferBitExt,
            Source = DescriptorMappingSourceEXT.PushAddressExt
        };

        ShaderDescriptorSetAndBindingMappingInfoEXT mappingInfo = new()
        {
            SType = StructureType.ShaderDescriptorSetAndBindingMappingInfoExt(),
            MappingCount = 1,
            PMappings = (DescriptorSetAndBindingMappingEXT*)ZenithMarshal.AllocateAndFill(scope, [mapping])
        };

        ShaderModuleCreateInfo createInfo = new()
        {
            SType = StructureType.ShaderModuleCreateInfo,
            PNext = (ShaderDescriptorSetAndBindingMappingInfoEXT*)ZenithMarshal.AllocateAndFill(scope, [mappingInfo]),
            CodeSize = (nuint)Desc.CodeBytes.Length,
            PCode = (uint*)ZenithMarshal.AllocateAndFill(scope, Desc.CodeBytes)
        };

        return new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            PNext = (ShaderModuleCreateInfo*)ZenithMarshal.AllocateAndFill(scope, [createInfo]),
            Stage = stage,
            PName = (byte*)ZenithMarshal.StringToPointer(scope, Desc.Name, StringEncoding.UTF8)
        };
    }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
    }
}
