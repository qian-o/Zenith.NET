using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKShader : Shader
{
    public ShaderModule ShaderModule;

    public VKShader(VKGraphicsContext context, ShaderDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        ShaderModuleCreateInfo createInfo = new()
        {
            SType = StructureType.ShaderModuleCreateInfo,
            CodeSize = (uint)desc.ShaderBytes.Length,
            PCode = (uint*)ZenithMarshal.AllocateAndFill(scope, desc.ShaderBytes)
        };

        context.Vk.CreateShaderModule(context.Device, &createInfo, null, out ShaderModule).Success();
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public PipelineShaderStageCreateInfo GetPipelineShaderStageCreateInfo(ZenithMarshal.Scope scope)
    {
        return new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = VKFormats.Vulkan(Desc.Stage),
            Module = ShaderModule,
            PName = (byte*)ZenithMarshal.StringToPointer(scope, Desc.EntryPoint, StringEncoding.UTF8)
        };
    }

    protected override void SetResourceName(string name)
    {
        using ZenithMarshal.Scope scope = new();

        DebugUtilsObjectNameInfoEXT nameInfo = new()
        {
            SType = StructureType.DebugUtilsObjectNameInfoExt,
            ObjectType = ObjectType.ShaderModule,
            ObjectHandle = ShaderModule.Handle,
            PObjectName = (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8)
        };

        Context.DebugUtils?.SetDebugUtilsObjectName(Context.Device, &nameInfo).Success();
    }

    protected override void Destroy()
    {
        Context.Vk.DestroyShaderModule(Context.Device, ShaderModule, null);
    }
}
