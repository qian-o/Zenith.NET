namespace Zenith.NET;

public struct SamplerDesc
{
    public FilterMode MinFilter;

    public FilterMode MagFilter;

    public FilterMode MipFilter;

    public AddressMode AddressU;

    public AddressMode AddressV;

    public AddressMode AddressW;

    public CompareOp CompareOp;

    public uint MaxAnisotropy;

    public float LodBias;

    public float MinLod;

    public float MaxLod;

    public BorderColor BorderColor;

    public static SamplerDesc LinearWrap()
    {
        return new()
        {
            MinFilter = FilterMode.Linear,
            MagFilter = FilterMode.Linear,
            MipFilter = FilterMode.Linear,
            AddressU = AddressMode.Wrap,
            AddressV = AddressMode.Wrap,
            AddressW = AddressMode.Wrap,
            CompareOp = CompareOp.Never,
            MaxAnisotropy = 1,
            LodBias = 0.0f,
            MinLod = 0.0f,
            MaxLod = float.MaxValue,
            BorderColor = BorderColor.TransparentBlack
        };
    }

    public static SamplerDesc LinearClamp()
    {
        return new()
        {
            MinFilter = FilterMode.Linear,
            MagFilter = FilterMode.Linear,
            MipFilter = FilterMode.Linear,
            AddressU = AddressMode.Clamp,
            AddressV = AddressMode.Clamp,
            AddressW = AddressMode.Clamp,
            CompareOp = CompareOp.Never,
            MaxAnisotropy = 1,
            LodBias = 0.0f,
            MinLod = 0.0f,
            MaxLod = float.MaxValue,
            BorderColor = BorderColor.TransparentBlack
        };
    }

    public static SamplerDesc PointWrap()
    {
        return new()
        {
            MinFilter = FilterMode.Point,
            MagFilter = FilterMode.Point,
            MipFilter = FilterMode.Point,
            AddressU = AddressMode.Wrap,
            AddressV = AddressMode.Wrap,
            AddressW = AddressMode.Wrap,
            CompareOp = CompareOp.Never,
            MaxAnisotropy = 1,
            LodBias = 0.0f,
            MinLod = 0.0f,
            MaxLod = float.MaxValue,
            BorderColor = BorderColor.TransparentBlack
        };
    }

    public static SamplerDesc PointClamp()
    {
        return new()
        {
            MinFilter = FilterMode.Point,
            MagFilter = FilterMode.Point,
            MipFilter = FilterMode.Point,
            AddressU = AddressMode.Clamp,
            AddressV = AddressMode.Clamp,
            AddressW = AddressMode.Clamp,
            CompareOp = CompareOp.Never,
            MaxAnisotropy = 1,
            LodBias = 0.0f,
            MinLod = 0.0f,
            MaxLod = float.MaxValue,
            BorderColor = BorderColor.TransparentBlack
        };
    }

    public static SamplerDesc Anisotropic(uint maxAnisotropy)
    {
        return new()
        {
            MinFilter = FilterMode.Linear,
            MagFilter = FilterMode.Linear,
            MipFilter = FilterMode.Linear,
            AddressU = AddressMode.Wrap,
            AddressV = AddressMode.Wrap,
            AddressW = AddressMode.Wrap,
            CompareOp = CompareOp.Never,
            MaxAnisotropy = maxAnisotropy,
            LodBias = 0.0f,
            MinLod = 0.0f,
            MaxLod = float.MaxValue,
            BorderColor = BorderColor.TransparentBlack
        };
    }
}
