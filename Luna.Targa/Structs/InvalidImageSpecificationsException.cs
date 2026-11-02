using Luna.Targa.Enums;

namespace Luna.Targa.Structs;

/// <summary> Exception thrown for unsupported or invalid Targa headers. </summary>
public sealed class InvalidImageSpecificationsException(ImageType imageType, byte pixelSize, byte alphaDepth, byte colorMapSize)
    : NotSupportedException($"An image with type {imageType} and pixel specifications {pixelSize}|{colorMapSize}|{alphaDepth} is invalid.")
{
    public InvalidImageSpecificationsException(in Header header)
        : this(header.ImageType, header.Image.PixelBitSize, header.Image.AlphaDepth,
            header.ColorMapType is 0 ? (byte)0 : header.ColorMap.EntryBitSize)
    { }
}
