namespace Luna.Targa.Structs;

/// <summary> A Top-left origin, tightly packed, straight-alpha RGBA pixels.</summary>
public readonly struct Image : ICloneable
{
    /// <summary> An empty image with zero dimensions. </summary>
    public static readonly Image Empty = new();

    /// <summary> The width of the image in pixels. </summary>
    public readonly int Width;

    /// <summary> The height of the image in pixels. </summary>
    public readonly int Height;

    /// <summary> The pixel data as RGBA bytes. </summary>
    public readonly byte[] Pixels;

    /// <summary> Create an empty image. </summary>
    public Image()
        : this(0, 0, [])
    { }

    /// <summary> Create an image of given dimensions and allocate its data without setting it. </summary>
    /// <param name="width"> The width of the new image. </param>
    /// <param name="height"> The height of the new image. </param>
    public Image(int width, int height)
        : this(width, height, new byte[(long)width * height * 4])
    { }

    /// <summary> Create an image and take ownership of the pixel data. </summary>
    /// <param name="width"> The width of the new image. </param>
    /// <param name="height"> The height of the new image. </param>
    /// <param name="pixels"> The pixel data. This has to match the width and height exactly. </param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public Image(int width, int height, byte[] pixels)
    {
        if (width is < 0 or > ushort.MaxValue || height is < 0 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(width), "Targa dimensions must be between 0 and 65535.");

        Width  = width;
        Height = height;

        ArgumentNullException.ThrowIfNull(pixels);
        if ((long)width * height * 4 != pixels.Length)
            throw new ArgumentException("Expected width * height * 4 RGBA bytes.", nameof(pixels));

        Pixels = pixels;
    }

    /// <inheritdoc cref="ICloneable.Clone"/>
    public Image Clone()
        => new(Width, Height, Pixels.ToArray());

    /// <inheritdoc/>
    object ICloneable.Clone()
        => new Image(Width, Height, Pixels.ToArray());
}
