using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXShader(DXGraphicsContext context, ShaderDesc desc) : Shader(context, desc)
{
    public ShaderBytecode GetShaderBytecode(ZenithMarshal.Scope scope)
    {
        byte* shaderBytecode = (byte*)ZenithMarshal.Allocate<byte>(scope, (uint)Desc.ShaderBytes.Length);
        Desc.ShaderBytes.CopyTo(new Span<byte>(shaderBytecode, Desc.ShaderBytes.Length));

        return new()
        {
            PShaderBytecode = shaderBytecode,
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
