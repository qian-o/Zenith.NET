using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXShader(DXGraphicsContext context, ShaderDesc desc) : Shader(context, desc)
{
    public ShaderBytecode GetShaderBytecode(ZenithMarshal.Scope scope)
    {
        return new()
        {
            PShaderBytecode = (byte*)ZenithMarshal.AllocateAndFill(scope, Desc.CodeBytes),
            BytecodeLength = (uint)Desc.CodeBytes.Length
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
