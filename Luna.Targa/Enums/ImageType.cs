namespace Luna.Targa.Enums;

/// <summary> The specified Targa image types. </summary>
public enum ImageType : byte
{
    /// <summary> No image data is included. </summary>
    NoImageData = 0,

    /// <summary> An uncompressed, color-mapped image. </summary>
    UncompressedColorMapped = 1,

    /// <summary> An uncompressed RGB(A) image. </summary>
    UncompressedTrueColor = 2,

    /// <summary> An uncompressed black and white image. </summary>
    UncompressedGrayscale = 3,

    /// <summary> A run-length encoded, color-mapped image. </summary>
    RunLengthEncodedColorMapped = 9,

    /// <summary> A run-length encoded RGB(A) image. </summary>
    RunLengthEncodedTrueColor = 10,

    /// <summary> A run-length encoded black and white image. </summary>
    RunLengthEncodedGrayscale = 11,

    /// <summary> A compressed, color-mapped image using Huffman-, Delta-, and runlength encoding. </summary>
    HuffmanEncodedColorMapped = 32,

    /// <summary> A compressed, color-mapped image using Huffman-, Delta-, and runlength encoding with a 4-pass quadtree-type process. </summary>
    HuffmanQuadtreeColorMapped = 33,
}

internal static class ImageTypeExtensions
{
    extension(ImageType type)
    {
        public bool IsRunlength
            => type is ImageType.RunLengthEncodedColorMapped or ImageType.RunLengthEncodedGrayscale or ImageType.RunLengthEncodedTrueColor;

        public long MinimumImageSize(long numberOfPixels, byte pixelByteSize)
            => type switch
            {
                ImageType.NoImageData => 0,
                ImageType.UncompressedColorMapped
                    or ImageType.UncompressedTrueColor
                    or ImageType.UncompressedGrayscale => numberOfPixels * pixelByteSize, // Exact
                ImageType.RunLengthEncodedColorMapped
                    or ImageType.RunLengthEncodedTrueColor
                    or ImageType.RunLengthEncodedGrayscale => (numberOfPixels + 127) / 128 * (pixelByteSize + 1), // Minimum
                _ => long.MaxValue,
            };
    }
}
