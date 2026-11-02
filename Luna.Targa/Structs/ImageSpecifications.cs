using Luna.Targa.Enums;

namespace Luna.Targa.Structs;

/// <summary> The specifications of the image data. </summary>
public struct ImageSpecifications
{
    /// <summary> The X-coordinate (in pixels) of the lower left corner of the image. </summary>
    public ushort OriginX;

    /// <summary> The Y-coordinate (in pixels) of the lower left corner of the image. </summary>
    public ushort OriginY;

    /// <summary> The width of the image in pixels. </summary>
    public ushort Width;

    /// <summary> The height of the image in pixels. </summary>
    public ushort Height;

    /// <summary> The number of bits used by each stored pixel or for color-mapped images each pixel index. </summary>
    public byte PixelBitSize;

    /// <summary> The number of bytes used by each stored pixel or for color-mapped images each pixel index. </summary>
    public byte PixelByteSize
        => (byte)((PixelBitSize + 7) / 8);

    /// <summary> A packed field of descriptive data. </summary>
    internal byte ImageDescriptor;

    /// <summary> The number of bits associated with the alpha channel for each pixel. </summary>
    /// <remarks> This should generally be 0 or 1 for 16 bits, 0 for 24 bits and 8 for 32 bits. </remarks>
    public byte AlphaDepth
    {
        get => (byte)(ImageDescriptor & 0b1111);
        set => ImageDescriptor = (byte)((ImageDescriptor & 0b11110000) | (value & 0b1111));
    }

    /// <summary> The pixel order of the image data. </summary>
    /// <remarks> According to the specification, Right-to-Left order is not supported, and Top-to-Bottom is not supported in Truevision images. </remarks>
    public PixelOrder PixelOrder
    {
        get => (PixelOrder)((ImageDescriptor >> 4) & 0b11);
        set => ImageDescriptor = (byte)((ImageDescriptor & 0b11001111) | (((byte)value & 0b11) << 4));
    }

    /// <summary> The interleave mode of the image data. </summary>
    public InterleaveMode InterleaveMode
    {
        get => (InterleaveMode)((ImageDescriptor >> 6) & 0b11);
        set => ImageDescriptor = (byte)((ImageDescriptor & 0b00111111) | (((byte)value & 0b11) << 6));
    }
}
