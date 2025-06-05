namespace Zenith.NET;

/// <summary>
/// Specifies the format of pixel data for textures, render targets, and other resources.
/// </summary>
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
    /// Single-channel, 32-bit unsigned integer.
    /// </summary>
    R32UInt,

    /// <summary>
    /// Single-channel, 32-bit signed integer.
    /// </summary>
    R32SInt,

    /// <summary>
    /// Single-channel, 32-bit signed floating-point value. Can be used as a depth format.
    /// </summary>
    R32Float,

    /// <summary>
    /// RG, 8-bit unsigned normalized integers per component.
    /// </summary>
    R8G8UNorm,

    /// <summary>
    /// RG, 8-bit signed normalized integers per component.
    /// </summary>
    R8G8SNorm,

    /// <summary>
    /// RG, 8-bit unsigned integers per component.
    /// </summary>
    R8G8UInt,

    /// <summary>
    /// RG, 8-bit signed integers per component.
    /// </summary>
    R8G8SInt,

    /// <summary>
    /// RG, 16-bit unsigned normalized integers per component.
    /// </summary>
    R16G16UNorm,

    /// <summary>
    /// RG, 16-bit signed normalized integers per component.
    /// </summary>
    R16G16SNorm,

    /// <summary>
    /// RG, 16-bit unsigned integers per component.
    /// </summary>
    R16G16UInt,

    /// <summary>
    /// RG, 16-bit signed integers per component.
    /// </summary>
    R16G16SInt,

    /// <summary>
    /// RG, 16-bit floating-point values per component.
    /// </summary>
    R16G16Float,

    /// <summary>
    /// RG, 32-bit unsigned integers per component.
    /// </summary>
    R32G32UInt,

    /// <summary>
    /// RG, 32-bit signed integers per component.
    /// </summary>
    R32G32SInt,

    /// <summary>
    /// RG, 32-bit floating-point values per component.
    /// </summary>
    R32G32Float,

    /// <summary>
    /// RGB, 32-bit unsigned integers per component.
    /// </summary>
    R32G32B32UInt,

    /// <summary>
    /// RGB, 32-bit signed integers per component.
    /// </summary>
    R32G32B32SInt,

    /// <summary>
    /// RGB, 32-bit floating-point values per component.
    /// </summary>
    R32G32B32Float,

    /// <summary>
    /// RGBA, 8-bit unsigned normalized integers per component.
    /// </summary>
    R8G8B8A8UNorm,

    /// <summary>
    /// RGBA, 8-bit unsigned normalized integers per component (sRGB format).
    /// </summary>
    R8G8B8A8UNormSRgb,

    /// <summary>
    /// RGBA, 8-bit signed normalized integers per component.
    /// </summary>
    R8G8B8A8SNorm,

    /// <summary>
    /// RGBA, 8-bit unsigned integers per component.
    /// </summary>
    R8G8B8A8UInt,

    /// <summary>
    /// RGBA, 8-bit signed integers per component.
    /// </summary>
    R8G8B8A8SInt,

    /// <summary>
    /// RGBA, 16-bit unsigned normalized integers per component.
    /// </summary>
    R16G16B16A16UNorm,

    /// <summary>
    /// RGBA, 16-bit signed normalized integers per component.
    /// </summary>
    R16G16B16A16SNorm,

    /// <summary>
    /// RGBA, 16-bit unsigned integers per component.
    /// </summary>
    R16G16B16A16UInt,

    /// <summary>
    /// RGBA, 16-bit signed integers per component.
    /// </summary>
    R16G16B16A16SInt,

    /// <summary>
    /// RGBA, 16-bit floating-point values per component.
    /// </summary>
    R16G16B16A16Float,

    /// <summary>
    /// RGBA, 32-bit unsigned integers per component.
    /// </summary>
    R32G32B32A32UInt,

    /// <summary>
    /// RGBA, 32-bit signed integers per component.
    /// </summary>
    R32G32B32A32SInt,

    /// <summary>
    /// RGBA, 32-bit floating-point values per component.
    /// </summary>
    R32G32B32A32Float,

    /// <summary>
    /// BGRA, 8-bit unsigned normalized integers per component.
    /// </summary>
    B8G8R8A8UNorm,

    /// <summary>
    /// BGRA, 8-bit unsigned normalized integers per component (sRGB format).
    /// </summary>
    B8G8R8A8UNormSRgb,

    /// <summary>
    /// BC1 block compressed format with a single-bit alpha channel.
    /// </summary>
    BC1UNorm,

    /// <summary>
    /// BC1 block compressed format with a single-bit alpha channel (sRGB format).
    /// </summary>
    BC1UNormSRgb,

    /// <summary>
    /// BC2 block compressed format.
    /// </summary>
    BC2UNorm,

    /// <summary>
    /// BC2 block compressed format (sRGB format).
    /// </summary>
    BC2UNormSRgb,

    /// <summary>
    /// BC3 block compressed format.
    /// </summary>
    BC3UNorm,

    /// <summary>
    /// BC3 block compressed format (sRGB format).
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
    /// BC7 block compressed format (sRGB format).
    /// </summary>
    BC7UNormSRgb,

    /// <summary>
    /// Depth-stencil format: 24-bit unsigned normalized depth, 8-bit unsigned integer stencil.
    /// </summary>
    D24UNormS8UInt,

    /// <summary>
    /// Depth-stencil format: 32-bit floating-point depth, 8-bit unsigned integer stencil.
    /// </summary>
    D32FloatS8UInt
}
