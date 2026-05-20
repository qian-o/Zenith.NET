using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SlangResourceHeap.BindingModel;

[AttributeUsage(AttributeTargets.Struct, Inherited = false)]
public sealed class ConstantsAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Struct, Inherited = false)]
public sealed class ResourceHandleArrayAttribute(int length) : Attribute
{
    public int Length { get; } = length;
}

[StructLayout(LayoutKind.Sequential)]
public struct ResourceHandle
{
    public uint X;

    public uint Y;
}

[ResourceHandleArray(4)]
public partial struct TextureHandle4
{
}

[InlineArray(4)]
public partial struct TextureHandle4
{
    private ResourceHandle element0;
}

public enum ResourceKind
{
    Buffer,

    Texture,

    Sampler,

    AccelerationStructure
}

public enum ResourceAccess
{
    Read,

    ReadWrite
}

public sealed class ConstantsLayout(int sizeInBytes, ResourceFieldLayout[] resourceFields)
{
    public int SizeInBytes { get; } = sizeInBytes;

    public ResourceFieldLayout[] ResourceFields { get; } = resourceFields;
}

public sealed class ResourceFieldLayout(
    string name,
    ResourceKind kind,
    ResourceAccess access,
    int offsetInBytes,
    int metalIndex,
    int count = 1,
    int strideInBytes = 0,
    ConstantsLayout? elementLayout = null)
{
    public string Name { get; } = name;

    public ResourceKind Kind { get; } = kind;

    public ResourceAccess Access { get; } = access;

    public int OffsetInBytes { get; } = offsetInBytes;

    public int MetalIndex { get; } = metalIndex;

    public int Count { get; } = count;

    public int StrideInBytes { get; } = strideInBytes;

    public ConstantsLayout? ElementLayout { get; } = elementLayout;
}

public interface IConstantsLayout<TSelf>
    where TSelf : unmanaged, IConstantsLayout<TSelf>
{
    static abstract ConstantsLayout GetLayout();
}

public sealed class CommandBufferSketch
{
    public void SetConstants<TConstants>(in TConstants constants)
        where TConstants : unmanaged, IConstantsLayout<TConstants>
    {
        ConstantsLayout layout = TConstants.GetLayout();

        _ = constants;
        _ = layout;
    }
}

[Constants]
public partial struct DrawParams
{
    public Matrix4x4 World;

    public Matrix4x4 ViewProjection;

    public ResourceHandle Buffers;

    public ResourceHandle BaseColor;

    public ResourceHandle LinearSampler;

    public uint MaterialIndex;
}

public partial struct DrawParams : IConstantsLayout<DrawParams>
{
    public static ConstantsLayout GetLayout() => DrawParamsLayout.Value;
}

file static class DrawParamsLayout
{
    public static readonly ConstantsLayout Value = new(
        sizeInBytes: 160,
        resourceFields:
        [
            new("Buffers", ResourceKind.Buffer, ResourceAccess.Read, offsetInBytes: 128, metalIndex: 0),
            new("BaseColor", ResourceKind.Texture, ResourceAccess.Read, offsetInBytes: 136, metalIndex: 0),
            new("LinearSampler", ResourceKind.Sampler, ResourceAccess.Read, offsetInBytes: 144, metalIndex: 0),
        ]);
}

[Constants]
public partial struct TextureIndexParams
{
    public ResourceHandle TextureIndices;

    public ResourceHandle LinearSampler;

    public uint TextureIndex;
}

public partial struct TextureIndexParams : IConstantsLayout<TextureIndexParams>
{
    public static ConstantsLayout GetLayout() => TextureIndexParamsLayout.Value;
}

file static class TextureIndexParamsLayout
{
    public static readonly ConstantsLayout Value = new(
        sizeInBytes: 32,
        resourceFields:
        [
            new("TextureIndices", ResourceKind.Buffer, ResourceAccess.Read, offsetInBytes: 0, metalIndex: 0),
            new("LinearSampler", ResourceKind.Sampler, ResourceAccess.Read, offsetInBytes: 8, metalIndex: 0),
        ]);
}

[Constants]
public partial struct FixedTextureArrayParams
{
    public TextureHandle4 Textures;

    public ResourceHandle LinearSampler;

    public uint TextureIndex;

    public float UvScale;
}

public partial struct FixedTextureArrayParams : IConstantsLayout<FixedTextureArrayParams>
{
    public static ConstantsLayout GetLayout() => FixedTextureArrayParamsLayout.Spirv;
}

file static class FixedTextureArrayParamsLayout
{
    public static readonly ConstantsLayout Dxil = new(
        sizeInBytes: 72,
        resourceFields:
        [
            new("Textures", ResourceKind.Texture, ResourceAccess.Read, offsetInBytes: 0, metalIndex: 0, count: 4, strideInBytes: 16),
            new("LinearSampler", ResourceKind.Sampler, ResourceAccess.Read, offsetInBytes: 56, metalIndex: 0),
        ]);

    public static readonly ConstantsLayout Spirv = new(
        sizeInBytes: 80,
        resourceFields:
        [
            new("Textures", ResourceKind.Texture, ResourceAccess.Read, offsetInBytes: 0, metalIndex: 0, count: 4, strideInBytes: 16),
            new("LinearSampler", ResourceKind.Sampler, ResourceAccess.Read, offsetInBytes: 64, metalIndex: 0),
        ]);

    public static readonly ConstantsLayout Metal = new(
        sizeInBytes: 8,
        resourceFields:
        [
            new("Textures", ResourceKind.Texture, ResourceAccess.Read, offsetInBytes: 0, metalIndex: 0, count: 4),
            new("LinearSampler", ResourceKind.Sampler, ResourceAccess.Read, offsetInBytes: 32, metalIndex: 0),
        ]);
}
