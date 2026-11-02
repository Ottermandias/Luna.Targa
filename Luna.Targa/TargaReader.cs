using System.Buffers;
using System.Runtime.InteropServices;
using Luna.Targa.Enums;
using Luna.Targa.Structs;

namespace Luna.Targa;

/// <summary> An efficient, zero-allocation, zero-copy, in-memory reader for Targa files. </summary>
public readonly unsafe ref struct TargaReader
{
    /// <summary> The original full image data. </summary>
    public readonly ReadOnlySpan<byte> Data;

    /// <summary> The data type of the image data. </summary>
    private readonly PixelType _dataType;

    /// <summary> The data type of the color map data. </summary>
    private readonly PixelType _colorMapType;

    /// <summary> Create a Targa reader and verify that the header yields valid data and the image data fulfills the minimum size requirements for the image. </summary>
    /// <param name="data"> The image data. </param>
    /// <exception cref="InvalidDataException"> Thrown if not enough data exists to read the header, or to contain a valid image with the specifications from the parsed header. </exception>
    /// <exception cref="InvalidImageSpecificationsException"> Thrown if the specifications from the header are incompatible, invalid or not supported. </exception>
    /// <remarks> This does not verify run-length encoding or color map indices being valid. </remarks>
    public TargaReader(ReadOnlySpan<byte> data)
    {
        if (!ValidateSize(data, out var header))
            throw new InvalidDataException("The data is not large enough to contain a valid targa file with the read specifications.");

        if (!header.TryGetPixelType(out _dataType, out _colorMapType))
            throw new InvalidImageSpecificationsException(header);

        if (header.Image.InterleaveMode is not InterleaveMode.None)
            throw new InvalidDataException("The image specifies an interleaved mode, which is not supported.");

        Data = data;
    }

    private TargaReader(ReadOnlySpan<byte> data, PixelType dataType, PixelType colorMapType)
    {
        Data          = data;
        _dataType     = dataType;
        _colorMapType = colorMapType;
    }

    /// <summary> Try to create a Targa reader only if the header yields valid data and the image data fulfills the minimum size requirements for the image. </summary>
    /// <param name="data"> The image data. </param>
    /// <param name="targa"> On success, the reader. </param>
    /// <returns> True if the image can be read successfully. </returns>
    /// <remarks> This does not verify run-length encoding or color map indices being valid. </remarks>
    public static bool TryReadTarga(ReadOnlySpan<byte> data, out TargaReader targa)
    {
        if (!ValidateSize(data, out var header)
         || !header.TryGetPixelType(out var dataType, out var colorMapType)
         || header.Image.InterleaveMode is not InterleaveMode.None)
        {
            targa = default;
            return false;
        }

        targa = new TargaReader(data, dataType, colorMapType);
        return true;
    }

    /// <summary> Get the header of the Targa image in memory. </summary>
    public ref readonly Header Header
        => ref MemoryMarshal.AsRef<Header>(Data);

    /// <summary> Get the custom identifier of the Targa image in memory as raw bytes. This may be empty. </summary>
    public ReadOnlySpan<byte> Id
    {
        get
        {
            ref readonly var header = ref Header;
            if (header.IdLength is 0)
                return [];

            return Data[sizeof(Header)..(sizeof(Header) + header.IdLength)];
        }
    }

    /// <summary> Get the raw color map data if a color map exists. </summary>
    public ReadOnlySpan<byte> ColorMap
    {
        get
        {
            ref readonly var header = ref Header;
            if (header.ColorMapType is 0)
                return [];

            var start = sizeof(Header) + header.IdLength;
            var end   = start + header.ColorMap.Length * header.ColorMap.EntryByteSize;
            return Data[start..end];
        }
    }

    /// <summary> Get the raw image data as bytes. </summary>
    /// <remarks> If the image is run-length encoded, all bytes from the start of the data block are returned, regardless of whether this contains a footer or additional data. Otherwise, the exact length of the image data is returned. </remarks>
    public ReadOnlySpan<byte> ImageData
    {
        get
        {
            if (_dataType is PixelType.None)
                return [];

            ref readonly var header      = ref Header;
            var              pixelNumber = header.PixelNumber;
            if (pixelNumber is 0)
                return [];

            var start = sizeof(Header)
              + header.IdLength
              + (header.ColorMapType is 0 ? 0 : header.ColorMap.Length * header.ColorMap.EntryByteSize);

            if (!header.ImageType.IsRunlength)
            {
                var end = start + pixelNumber * header.Image.PixelByteSize;
                return Data[start..end];
            }

            return Data[start..];
        }
    }

    /// <summary> Validate that the passed image data is large enough to contain a Targa header, and the minimum amount of data necessary for the specifications from the header. </summary>
    /// <param name="data"> The image data to validate. </param>
    /// <param name="header"> The parsed header, if enough data for one was available. </param>
    /// <returns> True if the sizes are large enough and a header is returned. </returns>
    public static bool ValidateSize(ReadOnlySpan<byte> data, out Header header)
    {
        header = default;
        if (data.Length < sizeof(Header))
            return false;

        header = MemoryMarshal.Read<Header>(data);
        long requiredSize = sizeof(Header) + header.IdLength;
        if (header.ColorMapType > 0)
            requiredSize += header.ColorMap.Length * header.ColorMap.EntryByteSize;
        if (!header.ValidatePixelCount())
            return false;

        var minimumDataSize = header.ImageType.MinimumImageSize(header.PixelNumber, header.Image.PixelByteSize);
        if (minimumDataSize is long.MaxValue)
            return false;

        return data.Length >= requiredSize + minimumDataSize;
    }

    /// <summary> Validate that the image data in the span only contains valid indices for the color map length. </summary>
    /// <param name="data"> The index data to validate. </param>
    /// <param name="header"> The header for the image we are validating. </param>
    /// <returns> True if all indices are present in the color map. </returns>
    public static bool ValidateColorMap(ReadOnlySpan<byte> data, in Header header)
    {
        if (data.Length is 0)
            return true;

        if (!header.TryGetPixelType(out var dataType, out _))
            return false;

        switch (dataType)
        {
            case PixelType.Index1:
            {
                if (header.ColorMap.Length is 0)
                    return false;
                if (header.ColorMap.FirstEntry > byte.MaxValue)
                    return false;

                var start = (byte)header.ColorMap.FirstEntry;
                var end   = (byte)Math.Clamp(header.ColorMap.FirstEntry + header.ColorMap.Length - 1, 0, byte.MaxValue);
                return !data.ContainsAnyExceptInRange(start, end);
            }
            case PixelType.Index2:
            {
                if (header.ColorMap.Length is 0)
                    return false;
                if ((data.Length & 1) is 1)
                    return false;

                var start = header.ColorMap.FirstEntry;
                var end   = (ushort)Math.Clamp(header.ColorMap.FirstEntry + header.ColorMap.Length - 1, 0, ushort.MaxValue);
                return !MemoryMarshal.Cast<byte, ushort>(data).ContainsAnyExceptInRange(start, end);
            }
            default: return true;
        }
    }

    /// <summary> Validate that the run-length encoded pixel data is valid and matches the image dimensions, without validating the color-map indices. </summary>
    /// <param name="header"> The header of the image. </param>
    /// <param name="imageData"> The actual image data. </param>
    /// <param name="length"> The length of the actual used data of the run-length encoding in bytes. </param>
    /// <returns> True if the run-length encoding is valid. </returns>
    public static bool ValidateRunLength(in Header header, ReadOnlySpan<byte> imageData, out int length)
        => ValidateRunLength<TrueColorDataType>(header, imageData, out length);

    /// <summary> Validate that the run-length encoded pixel data is valid and matches the image dimensions, also validating that all color-map indices are mapped. </summary>
    /// <param name="header"> The header of the image. </param>
    /// <param name="imageData"> The actual image data. </param>
    /// <param name="length"> The length of the actual used data of the run-length encoding in bytes. </param>
    /// <returns> True if the run-length encoding is valid. </returns>
    public static bool ValidateColorMapRunLength(in Header header, ReadOnlySpan<byte> imageData, out int length)
        => ValidateRunLength<ColorMapDataType>(header, imageData, out length);

    /// <summary> Return a newly allocated array containing the uncompressed RGBA32 pixel data, going left to right and top to bottom. </summary>
    /// <exception cref="InvalidDataException"> Thrown if color map indices are unmapped or the run-length encoding is invalid. </exception>
    public byte[] CreateRgba32Data()
    {
        if (_dataType is PixelType.None)
            return [];

        ref readonly var header      = ref Header;
        var              pixelNumber = header.PixelNumber;
        if (pixelNumber is 0)
            return [];

        var       ret              = new byte[pixelNumber * 4];
        var       span             = MemoryMarshal.Cast<byte, uint>(ret.AsSpan());
        using var packetEnumerator = new PackageEnumerator(ImageData, _dataType.Bytes, header.ImageType.IsRunlength ? -1 : pixelNumber);
        var success = (_dataType, _colorMapType) switch
        {
            (PixelType.Grayscale, _)      => CopyTrueColor<GrayscalePixel>(packetEnumerator, span),
            (PixelType.GrayscaleAlpha, _) => CopyTrueColor<GrayscaleAlphaPixel>(packetEnumerator, span),
            (PixelType.Rgb555, _)         => CopyTrueColor<Rgb555Pixel>(packetEnumerator, span),
            (PixelType.Argb1555, _)       => CopyTrueColor<Argb1555Pixel>(packetEnumerator, span),
            (PixelType.Bgr888, _)         => CopyTrueColor<Bgr888Pixel>(packetEnumerator, span),
            (PixelType.Bgra8888, _)       => CopyTrueColor<Bgra8888Pixel>(packetEnumerator, span),
            (PixelType.Bgrx8888, _)       => CopyTrueColor<Bgrx8888Pixel>(packetEnumerator, span),

            // We handle nonsensical combinations since this is just about parsing.
            // If someone decides to create a color-mapped image with 16-bit indices and 8-bit color values, why not.
            (PixelType.Index1, PixelType.Grayscale) => CopyColorMap<Index1<GrayscalePixel>, GrayscalePixel>(packetEnumerator, span,
                new ColorMapContext<GrayscalePixel>(ColorMap, header.ColorMap.FirstEntry)),
            (PixelType.Index1, PixelType.GrayscaleAlpha) => CopyColorMap<Index1<GrayscaleAlphaPixel>, GrayscaleAlphaPixel>(packetEnumerator,
                span, new ColorMapContext<GrayscaleAlphaPixel>(ColorMap, header.ColorMap.FirstEntry)),
            (PixelType.Index1, PixelType.Rgb555) => CopyColorMap<Index1<Rgb555Pixel>, Rgb555Pixel>(packetEnumerator, span,
                new ColorMapContext<Rgb555Pixel>(ColorMap, header.ColorMap.FirstEntry)),
            (PixelType.Index1, PixelType.Argb1555) => CopyColorMap<Index1<Argb1555Pixel>, Argb1555Pixel>(packetEnumerator, span,
                new ColorMapContext<Argb1555Pixel>(ColorMap, header.ColorMap.FirstEntry)),
            (PixelType.Index1, PixelType.Bgr888) => CopyColorMap<Index1<Bgr888Pixel>, Bgr888Pixel>(packetEnumerator, span,
                new ColorMapContext<Bgr888Pixel>(ColorMap, header.ColorMap.FirstEntry)),
            (PixelType.Index1, PixelType.Bgra8888) => CopyColorMap<Index1<Bgra8888Pixel>, Bgra8888Pixel>(packetEnumerator, span,
                new ColorMapContext<Bgra8888Pixel>(ColorMap, header.ColorMap.FirstEntry)),
            (PixelType.Index1, PixelType.Bgrx8888) => CopyColorMap<Index1<Bgrx8888Pixel>, Bgrx8888Pixel>(packetEnumerator, span,
                new ColorMapContext<Bgrx8888Pixel>(ColorMap, header.ColorMap.FirstEntry)),

            (PixelType.Index2, PixelType.Grayscale) => CopyColorMap<Index2<GrayscalePixel>, GrayscalePixel>(packetEnumerator, span,
                new ColorMapContext<GrayscalePixel>(ColorMap, header.ColorMap.FirstEntry)),
            (PixelType.Index2, PixelType.GrayscaleAlpha) => CopyColorMap<Index2<GrayscaleAlphaPixel>, GrayscaleAlphaPixel>(packetEnumerator,
                span, new ColorMapContext<GrayscaleAlphaPixel>(ColorMap, header.ColorMap.FirstEntry)),
            (PixelType.Index2, PixelType.Rgb555) => CopyColorMap<Index2<Rgb555Pixel>, Rgb555Pixel>(packetEnumerator, span,
                new ColorMapContext<Rgb555Pixel>(ColorMap, header.ColorMap.FirstEntry)),
            (PixelType.Index2, PixelType.Argb1555) => CopyColorMap<Index2<Argb1555Pixel>, Argb1555Pixel>(packetEnumerator, span,
                new ColorMapContext<Argb1555Pixel>(ColorMap, header.ColorMap.FirstEntry)),
            (PixelType.Index2, PixelType.Bgr888) => CopyColorMap<Index2<Bgr888Pixel>, Bgr888Pixel>(packetEnumerator, span,
                new ColorMapContext<Bgr888Pixel>(ColorMap, header.ColorMap.FirstEntry)),
            (PixelType.Index2, PixelType.Bgra8888) => CopyColorMap<Index2<Bgra8888Pixel>, Bgra8888Pixel>(packetEnumerator, span,
                new ColorMapContext<Bgra8888Pixel>(ColorMap, header.ColorMap.FirstEntry)),
            (PixelType.Index2, PixelType.Bgrx8888) => CopyColorMap<Index2<Bgrx8888Pixel>, Bgrx8888Pixel>(packetEnumerator, span,
                new ColorMapContext<Bgrx8888Pixel>(ColorMap, header.ColorMap.FirstEntry)),

            _ => false,
        };
        if (!success)
            throw new InvalidDataException("Image does not specify enough pixels.");

        OrientPixels(header, span);

        return ret;
    }

    private static void OrientPixels(in Header header, Span<uint> pixelData)
    {
        var width  = header.Image.Width;
        var height = header.Image.Height;

        if (header.Image.PixelOrder is PixelOrder.RightLeftBottomTop)
        {
            pixelData.Reverse();
            return;
        }

        if (!header.Image.PixelOrder.HasFlag(PixelOrder.TopToBottom))
        {
            var buffer  = ArrayPool<uint>.Shared.Rent(width);
            var scratch = buffer.AsSpan(0, width);
            try
            {
                for (var y = 0; y < height / 2; ++y)
                {
                    var top    = pixelData.Slice(y * width,                width);
                    var bottom = pixelData.Slice((height - 1 - y) * width, width);
                    top.CopyTo(scratch);
                    bottom.CopyTo(top);
                    scratch.CopyTo(bottom);
                }
            }
            finally
            {
                ArrayPool<uint>.Shared.Return(buffer);
            }
        }


        if (header.Image.PixelOrder.HasFlag(PixelOrder.RightToLeft))
            for (var y = 0; y < height; ++y)
                pixelData.Slice(y * width, width).Reverse();
    }

    private static bool CopyTrueColor<TPixel>(PackageEnumerator p, Span<uint> s) where TPixel : IPixelType<NopContext>
    {
        while (p.MoveNext())
        {
            var nextSize = p.Current.Count * p.Current.Repetitions;
            if (nextSize > s.Length)
                throw new InvalidDataException("Image specifies too many pixels.");

            TPixel.ConvertBatch(p.Current.Data, s, default, p.Current.Repetitions);
            s = s[nextSize..];
            if (s.Length is 0)
                return true;
        }

        if (p.HasMissingData)
            throw new InvalidDataException("Image did not have enough data to read specified pixels.");

        return false;
    }

    private static bool CopyColorMap<TIndex, TPixel>(PackageEnumerator p, Span<uint> s, in ColorMapContext<TPixel> context)
        where TIndex : IPixelType<ColorMapContext<TPixel>>
        where TPixel : unmanaged, IPixelType<NopContext>
    {
        while (p.MoveNext())
        {
            var nextSize = p.Current.Count * p.Current.Repetitions;
            if (nextSize > s.Length)
                throw new InvalidDataException("Image specifies too many pixels.");

            TIndex.ConvertBatch(p.Current.Data, s, context, p.Current.Repetitions);
            s = s[nextSize..];
            if (s.Length is 0)
                return true;
        }

        if (p.HasMissingData)
            throw new InvalidDataException("Image did not have enough data to read specified pixels.");

        return false;
    }

    private static bool ValidateRunLength<T>(in Header header, ReadOnlySpan<byte> imageData, out int length) where T : IDataType
    {
        length = -1;
        if (!header.ValidatePixelCount())
            return false;

        var       numPixels   = header.PixelNumber;
        var       foundPixels = 0;
        using var enumerator  = new PackageEnumerator(imageData, header.Image.PixelByteSize, -1);
        while (enumerator.MoveNext())
        {
            var package = enumerator.Current;
            foundPixels += package.Count * package.Repetitions;

            // Too many pixels specified.
            if (foundPixels > numPixels)
                return false;

            if (!T.ValidatePacketData(header, package))
                return false;

            length = enumerator.CurrentIndex + 1;
            if (foundPixels == numPixels)
                return true;
        }

        // Not enough pixels specified, or not enough data to read.
        return false;
    }
}
