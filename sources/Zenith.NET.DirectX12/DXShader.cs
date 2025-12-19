using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXShader(DXGraphicsContext context, ShaderDesc desc) : Shader(context, desc)
{
    public ShaderBytecode GetShaderBytecode(ZenithMarshal.Scope scope)
    {
        return new()
        {
            PShaderBytecode = (byte*)ZenithMarshal.AllocateAndFill(scope, Desc.ShaderBytes),
            BytecodeLength = (uint)Desc.ShaderBytes.Length
        };
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
    }
}
