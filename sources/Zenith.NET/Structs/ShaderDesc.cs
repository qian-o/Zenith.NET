namespace Zenith.NET;

public struct ShaderDesc
{
    public byte[] Bytecode;

    public string EntryPoint;

    public ShaderStages Stage;

    public static ShaderDesc Vertex(byte[] bytecode, string entryPoint)
    {
        return new()
        {
            Bytecode = bytecode,
            EntryPoint = entryPoint,
            Stage = ShaderStages.Vertex
        };
    }

    public static ShaderDesc Fragment(byte[] bytecode, string entryPoint)
    {
        return new()
        {
            Bytecode = bytecode,
            EntryPoint = entryPoint,
            Stage = ShaderStages.Fragment
        };
    }

    public static ShaderDesc Compute(byte[] bytecode, string entryPoint)
    {
        return new()
        {
            Bytecode = bytecode,
            EntryPoint = entryPoint,
            Stage = ShaderStages.Compute
        };
    }

    public static ShaderDesc Task(byte[] bytecode, string entryPoint)
    {
        return new()
        {
            Bytecode = bytecode,
            EntryPoint = entryPoint,
            Stage = ShaderStages.Task
        };
    }

    public static ShaderDesc Mesh(byte[] bytecode, string entryPoint)
    {
        return new()
        {
            Bytecode = bytecode,
            EntryPoint = entryPoint,
            Stage = ShaderStages.Mesh
        };
    }
}
