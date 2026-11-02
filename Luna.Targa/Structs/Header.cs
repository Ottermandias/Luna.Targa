using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Luna.Targa.Enums;

namespace Luna.Targa.Structs;

/// <summary>The 18-byte TGA file header, serialized explicitly in little-endian order.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Header
{
    /// <summary> The maximum number of pixels allowed for a valid image - corresponds to 16k * 16k. </summary>
    public const int SupportedSize = 1 << 28;

    /// <summary> The length of the ID field in [0, 255]. </summary>
    public byte IdLength;

    /// <summary> Should generally contain a 0 (no color map is present) or 1 (a color map is present). </summary>
    /// <remarks>
    ///   Values 2-127 are reserved for Truevision, values 128-255 are available for developer use.<br/>
    ///   Color-mapped images should always have a 1 here, while true-color images may have a 0 or a 1 (the color map then generally being ignored).
    /// </remarks>
    public byte ColorMapType;

    /// <summary> The image type. </summary>
    public ImageType ImageType;

    /// <summary> The specifications for a color map, if one exists. Otherwise, this is 0. </summary>
    public ColorMapSpecification ColorMap;

    /// <summary> The specifications for the image data. </summary>
    public ImageSpecifications Image;

    /// <summary> The number of pixels in the image. </summary>
    /// <remarks> The size validation ensures that this is not larger than an integer. </remarks>
    public int PixelNumber
        => Image.Height * Image.Width;

    /// <summary> Try to get the pixel data and color map types. </summary>
    /// <param name="dataType"> The type of data in the actual image data, disregarding run-length encoding. </param>
    /// <param name="colorMapType"> The type of data in the color map chunk. </param>
    /// <returns> True if the data types are valid. </returns>
    public readonly bool TryGetPixelType(out PixelType dataType, out PixelType colorMapType)
    {
        dataType     = PixelType.None;
        colorMapType = PixelType.None;
        switch (ImageType)
        {
            case ImageType.NoImageData: return true;

            case ImageType.UncompressedColorMapped:
            case ImageType.RunLengthEncodedColorMapped:
                if (ColorMapType is 0)
                    return false;

                // We treat 15-bit indices (which should not exist) as 16-bit indices.
                dataType = Image.PixelBitSize switch
                {
                    8        => PixelType.Index1,
                    15 or 16 => PixelType.Index2,
                    _        => PixelType.Invalid,
                };
                if (dataType is PixelType.Invalid)
                    return false;

                colorMapType = GetPixelType(ColorMap.EntryBitSize, Image.AlphaDepth);
                // We do not complain about nonsensical combinations like 16-bit indices with 8-bit greyscale colors,
                // since this is about parsing, not creating.
                return colorMapType is not PixelType.Invalid;

            case ImageType.UncompressedTrueColor:
            case ImageType.RunLengthEncodedTrueColor:
                dataType = GetPixelType(Image.PixelBitSize, Image.AlphaDepth);
                return dataType is not PixelType.Invalid;

            case ImageType.UncompressedGrayscale:
            case ImageType.RunLengthEncodedGrayscale:
                dataType = (Image.PixelBitSize, Image.AlphaDepth) switch
                {
                    (16, 8) => PixelType.GrayscaleAlpha,
                    (15, 8) => PixelType.GrayscaleAlpha,
                    (8, 0)  => PixelType.Grayscale,
                    _       => PixelType.Invalid,
                };
                return dataType is not PixelType.Invalid;

            default: return false;
        }

        static PixelType GetPixelType(byte size, byte alpha)
        {
            return (size, alpha) switch
            {
                (8, 0)  => PixelType.Grayscale,
                (16, 8) => PixelType.GrayscaleAlpha,
                (16, 1) => PixelType.Argb1555,
                (16, 0) => PixelType.Rgb555,
                (15, 0) => PixelType.Rgb555,
                (24, 0) => PixelType.Bgr888,
                (32, 0) => PixelType.Bgrx8888,
                (32, 8) => PixelType.Bgra8888,
                _       => PixelType.Invalid,
            };
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly bool ValidatePixelCount()
        => (long)Image.Width * Image.Height <= SupportedSize;
}
