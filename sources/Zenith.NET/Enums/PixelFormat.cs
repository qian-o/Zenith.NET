namespace Zenith.NET;

public enum PixelFormat
{
    /// <summary>
    /// Single-channel, 8-bit unsigned normalized integer.
    /// </summary>
    R8UNorm,

    /// <summary>
    /// Single-channel, 8-bit signed normalized integer.
    /// </summary>
    R8SNorm,

    /// <summary>
    /// Single-channel, 8-bit unsigned integer.
    /// </summary>
    R8UInt,

    /// <summary>
    /// Single-channel, 8-bit signed integer.
    /// </summary>
    R8SInt,

    /// <summary>
    /// Single-channel, 16-bit unsigned normalized integer. Can be used as a depth format.
    /// </summary>
    R16UNorm,

    /// <summary>
    /// Single-channel, 16-bit signed normalized integer.
    /// </summary>
    R16SNorm,

    /// <summary>
    /// Single-channel, 16-bit unsigned integer.
    /// </summary>
    R16UInt,

    /// <summary>
    /// Single-channel, 16-bit signed integer.
    /// </summary>
    R16SInt,

    /// <summary>
    /// Single-channel, 16-bit signed floating-point value.
    /// </summary>
    R16Float,

    /// <summary>
    /// Single-channel, 32-bit unsigned integer
    /// </summary>
    R32UInt,

    /// <summary>
    /// Single-channel, 32-bit signed integer
    /// </summary>
    R32SInt,

    /// <summary>
    /// Single-channel, 32-bit signed floating-point value. Can be used as a depth format.
    /// </summary>
    R32Float,

    /// <summary>
    /// RG component order. Each component is an 8-bit unsigned normalized integer.
    /// </summary>
    R8G8UNorm,

    /// <summary>
    /// RG component order. Each component is an 8-bit signed normalized integer.
    /// </summary>
    R8G8SNorm,

    /// <summary>
    /// RG component order. Each component is an 8-bit unsigned integer.
    /// </summary>
    R8G8UInt,

    /// <summary>
    /// RG component order. Each component is an 8-bit signed integer.
    /// </summary>
    R8G8SInt,

    /// <summary>
    /// RG component order. Each component is a 16-bit unsigned normalized integer.
    /// </summary>
    R16G16UNorm,

    /// <summary>
    /// RG component order. Each component is a 16-bit signed normalized integer.
    /// </summary>
    R16G16SNorm,

    /// <summary>
    /// RG component order. Each component is a 16-bit unsigned integer.
    /// </summary>
    R16G16UInt,

    /// <summary>
    /// RG component order. Each component is a 16-bit signed integer.
    /// </summary>
    R16G16SInt,

    /// <summary>
    /// RG component order. Each component is a 16-bit signed floating-point value.
    /// </summary>
    R16G16Float,

    /// <summary>
    /// RG component order. Each component is a 32-bit unsigned integer.
    /// </summary>
    R32G32UInt,

    /// <summary>
    /// RG component order. Each component is a 32-bit signed integer.
    /// </summary>
    R32G32SInt,

    /// <summary>
    /// RG component order. Each component is a 32-bit signed floating-point value.
    /// </summary>
    R32G32Float,

    /// <summary>
    /// RGB component order. Each component is a 32-bit unsigned integer.
    /// </summary>
    R32G32B32UInt,

    /// <summary>
    /// RGB component order. Each component is a 32-bit signed integer.
    /// </summary>
    R32G32B32SInt,

    /// <summary>
    /// RGB component order. Each component is a 32-bit signed floating-point value.
    /// </summary>
    R32G32B32Float,

    /// <summary>
    /// RGBA component order. Each component is an 8-bit unsigned normalized integer.
    /// </summary>
    R8G8B8A8UNorm,

    /// <summary>
    /// RGBA component order. Each component is an 8-bit unsigned normalized integer.
    /// This is an sRGB format.
    /// </summary>
    R8G8B8A8UNormSRgb,

    /// <summary>
    /// RGBA component order. Each component is an 8-bit signed normalized integer.
    /// </summary>
    R8G8B8A8SNorm,

    /// <summary>
    /// RGBA component order. Each component is an 8-bit unsigned integer.
    /// </summary>
    R8G8B8A8UInt,

    /// <summary>
    /// RGBA component order. Each component is an 8-bit signed integer.
    /// </summary>
    R8G8B8A8SInt,

    /// <summary>
    /// RGBA component order. Each component is a 16-bit unsigned normalized integer.
    /// </summary>
    R16G16B16A16UNorm,

    /// <summary>
    /// RGBA component order. Each component is a 16-bit signed normalized integer.
    /// </summary>
    R16G16B16A16SNorm,

    /// <summary>
    /// RGBA component order. Each component is a 16-bit unsigned integer.
    /// </summary>
    R16G16B16A16UInt,

    /// <summary>
    /// RGBA component order. Each component is a 16-bit signed integer.
    /// </summary>
    R16G16B16A16SInt,

    /// <summary>
    /// RGBA component order. Each component is a 16-bit floating-point value.
    /// </summary>
    R16G16B16A16Float,

    /// <summary>
    /// RGBA component order. Each component is a 32-bit unsigned integer.
    /// </summary>
    R32G32B32A32UInt,

    /// <summary>
    /// RGBA component order. Each component is a 32-bit signed integer.
    /// </summary>
    R32G32B32A32SInt,

    /// <summary>
    /// RGBA component order. Each component is a 32-bit signed floating-point value.
    /// </summary>
    R32G32B32A32Float,

    /// <summary>
    /// BGRA component order. Each component is an 8-bit unsigned normalized integer.
    /// </summary>
    B8G8R8A8UNorm,

    /// <summary>
    /// BGRA component order. Each component is an 8-bit unsigned normalized integer.
    /// This is an sRGB format.
    /// </summary>
    B8G8R8A8UNormSRgb,

    /// <summary>
    /// BC1 block compressed format with a single-bit alpha channel.
    /// </summary>
    BC1UNorm,

    /// <summary>
    /// BC1 block compressed format with a single-bit alpha channel.
    /// This is an sRGB format.
    /// </summary>
    BC1UNormSRgb,

    /// <summary>
    /// BC2 block compressed format.
    /// </summary>
    BC2UNorm,

    /// <summary>
    /// BC2 block compressed format.
    /// This is an sRGB format.
    /// </summary>
    BC2UNormSRgb,

    /// <summary>
    /// BC3 block compressed format.
    /// </summary>
    BC3UNorm,

    /// <summary>
    /// BC3 block compressed format.
    /// This is an sRGB format.
    /// </summary>
    BC3UNormSRgb,

    /// <summary>
    /// BC4 block compressed format, unsigned normalized values.
    /// </summary>
    BC4UNorm,

    /// <summary>
    /// BC4 block compressed format, signed normalized values.
    /// </summary>
    BC4SNorm,

    /// <summary>
    /// BC5 block compressed format, unsigned normalized values.
    /// </summary>
    BC5UNorm,

    /// <summary>
    /// BC5 block compressed format, signed normalized values.
    /// </summary>
    BC5SNorm,

    /// <summary>
    /// BC7 block compressed format.
    /// </summary>
    BC7UNorm,

    /// <summary>
    /// BC7 block compressed format.
    /// This is an sRGB format.
    /// </summary>
    BC7UNormSRgb,

    /// <summary>
    /// A depth-stencil format where the depth is stored in a 24-bit unsigned normalized integer, and the stencil is stored
    /// in an 8-bit unsigned integer.
    /// </summary>
    D24UNormS8UInt,

    /// <summary>
    /// A depth-stencil format where the depth is stored in a 32-bit signed floating-point value, and the stencil is stored
    /// in an 8-bit unsigned integer.
    /// </summary>
    D32FloatS8UInt
}
