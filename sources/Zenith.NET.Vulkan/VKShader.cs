using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKShader : Shader
{
    public ShaderModule ShaderModule;

    public VKShader(GraphicsContext context, ShaderDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        byte* code = (byte*)ZenithMarshal.Allocate<byte>(scope, (uint)desc.ShaderBytes.Length);
        desc.ShaderBytes.CopyTo(new Span<byte>(code, desc.ShaderBytes.Length));

        ShaderModuleCreateInfo createInfo = new()
        {
            SType = StructureType.ShaderModuleCreateInfo,
            CodeSize = (uint)desc.ShaderBytes.Length,
            PCode = (uint*)code
        };

        Context.Vk.CreateShaderModule(Context.Device, &createInfo, null, (ShaderModule*)Unsafe.AsPointer(ref ShaderModule)).Success();
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public PipelineShaderStageCreateInfo GetPipelineShaderStageCreateInfo(ZenithMarshal.Scope scope)
    {
        return new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = Desc.Stage.Vulkan(),
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
