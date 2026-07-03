using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKShader(VKGraphicsContext context, ShaderDesc desc) : Shader(context, desc)
{
    public ShaderModuleCreateInfo GetShaderModuleCreateInfo(ZenithMarshal.Scope scope)
    {
        return new()
        {
            SType = StructureType.ShaderModuleCreateInfo,
            CodeSize = (nuint)Desc.CodeBytes.Length,
            PCode = (uint*)ZenithMarshal.AllocateAndFill(scope, Desc.CodeBytes)
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
