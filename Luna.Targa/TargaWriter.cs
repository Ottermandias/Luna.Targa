using System.Buffers.Binary;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Luna.Targa.Enums;
using Luna.Targa.Structs;

namespace Luna.Targa;

/// <summary> Write-routines for Targa images based on left-to-right, top-to-bottom RGBA32 pixel data. </summary>
public static class TargaWriter
{
    /// <summary> Shuffle the RGBA32 bytes to give the correct channel layout for Targa. </summary>
    private static ReadOnlySpan<byte> ShuffleIndices
        =>
        [
            2, 1, 0, 3,
            6, 5, 4, 7,
            10, 9, 8, 11,
            14, 13, 12, 15,
            2, 1, 0, 3,
            6, 5, 4, 7,
            10, 9, 8, 11,
            14, 13, 12, 15,
        ];

    /// <summary> Write RGBA32 data encoded as a true color Targa to a stream. </summary>
    /// <param name="stream"> The stream to write to. </param>
    /// <param name="data"> The RGBA32 image to write as Targa. </param>
    /// <param name="id"> A custom identifier to write to the image. May be 0 up to 255 bytes. </param>
    /// <param name="encodeRunLength"> Whether to use run-length encoding compression when writing the true color data. </param>
    public static void WriteRgba32(Stream stream, in Image data, ReadOnlySpan<byte> id = default, bool encodeRunLength = true)
    {
        using var b = WriteHeader(stream, id,
            encodeRunLength ? ImageType.RunLengthEncodedTrueColor : ImageType.UncompressedTrueColor,
            data.Width, data.Height, -1);

        Span<byte> buffer = stackalloc byte[4096];
        var        i      = 0;
        if (encodeRunLength)
        {
            var pixels = MemoryMarshal.Cast<byte, uint>(data.Pixels);
            while (i < pixels.Length)
            {
                var identicalColors = 1;
                var color           = pixels[i];
                while (identicalColors < 128 && i + identicalColors < pixels.Length && color == pixels[i + identicalColors])
                    ++identicalColors;

                if (identicalColors >= 2)
                {
                    buffer[0] = (byte)(0x80 | (identicalColors - 1));
                    WriteShuffled(buffer[1..], color);
                    b.Write(buffer[..5]);
                    i += identicalColors;
                }
                else
                {
                    var start = i++;
                    while (i - start < 128 && i < pixels.Length)
                    {
                        if (i + 1 < pixels.Length && pixels[i] == pixels[i + 1])
                            break;

                        ++i;
                    }

                    buffer[0] = (byte)(i - start - 1);
                    var byteCount = 4 * (i - start);
                    WriteShuffled(buffer[1..], data.Pixels.AsSpan(4 * start, byteCount));
                    b.Write(buffer[..(byteCount + 1)]);
                }
            }
        }
        else
        {
            var remaining = data.Pixels.AsSpan();
            while (!remaining.IsEmpty)
            {
                var count = Math.Min(remaining.Length, buffer.Length);
                var output = buffer[..count];
                WriteShuffled(output, remaining[..count]);
                b.Write(output);
                remaining = remaining[count..];
            }
        }
    }

    /// <summary> Write RGBA32 data encoded as a color mapped Targa to a stream. </summary>
    /// <param name="stream"> The stream to write to. </param>
    /// <param name="data"> The RGBA32 image to write as Targa. </param>
    /// <param name="id"> A custom identifier to write to the image. May be 0 up to 255 bytes. </param>
    /// <param name="encodeRunLength"> Whether to use run-length encoding compression when writing the color map indices. </param>
    /// <param name="allow16BitIndices"> Whether to only allow color maps of at most 256 entries. Many applications can not handle 16-bit color map indices. </param>
    /// <remarks> When not using run-length encoding, if the resulting color mapped image is larger than the true color image would be, a true color image is written instead. </remarks>
    public static void WriteColorMapped(Stream stream, in Image data, ReadOnlySpan<byte> id = default, bool encodeRunLength = true,
        bool allow16BitIndices = false)
    {
        if (!CreateColorMap(data.Pixels, out var colorMap, out var indices, allow16BitIndices)
         || colorMap.Count > byte.MaxValue + 1 && colorMap.Count * 4 > indices.Length * 2
         || allow16BitIndices && !encodeRunLength && colorMap.Count * 4 > indices.Length * 3)
        {
            WriteRgba32(stream, data, id, encodeRunLength);
            return;
        }

        using var b = WriteHeader(stream, id,
            encodeRunLength ? ImageType.RunLengthEncodedColorMapped : ImageType.UncompressedColorMapped,
            data.Width,                   data.Height, colorMap.Count);
        Span<byte> buffer = stackalloc byte[4096];
        var bufferedBytes = 0;
        foreach (var color in colorMap)
        {
            WriteShuffled(buffer[bufferedBytes..], color);
            bufferedBytes += 4;
            if (bufferedBytes == buffer.Length)
            {
                b.Write(buffer);
                bufferedBytes = 0;
            }
        }
        if (bufferedBytes > 0)
            b.Write(buffer[..bufferedBytes]);

        var indexSize = colorMap.Count > byte.MaxValue + 1 ? 2 : 1;
        if (encodeRunLength)
        {
            WriteRunLength(b, indices, indexSize, buffer);
        }
        else
        {
            var remaining = indices.AsSpan();
            while (!remaining.IsEmpty)
            {
                var count = Math.Min(remaining.Length, buffer.Length / indexSize);
                WriteIndices(buffer, remaining[..count], indexSize);
                b.Write(buffer[..(count * indexSize)]);
                remaining = remaining[count..];
            }
        }

        return;

        static void WriteRunLength(BinaryWriter b, ReadOnlySpan<ushort> indices, int size, Span<byte> buffer)
        {
            var i = 0;
            while (i < indices.Length)
            {
                var identicalColors = 1;
                var index           = indices[i];
                while (identicalColors < 128 && i + identicalColors < indices.Length && index == indices[i + identicalColors])
                    ++identicalColors;

                if (identicalColors >= 2)
                {
                    buffer[0] = (byte)(0x80 | (identicalColors - 1));
                    WriteIndices(buffer[1..], indices.Slice(i, 1), size);
                    b.Write(buffer[..(1 + size)]);
                    i += identicalColors;
                }
                else
                {
                    var start = i++;
                    while (i - start < 128 && i < indices.Length)
                    {
                        if (i + 1 < indices.Length && indices[i] == indices[i + 1])
                            break;

                        ++i;
                    }

                    buffer[0] = (byte)(i - start - 1);
                    WriteIndices(buffer[1..], indices[start..i], size);
                    b.Write(buffer[..(1 + (i - start) * size)]);
                }
            }
        }
    }

    private static void WriteIndices(Span<byte> buffer, ReadOnlySpan<ushort> indices, int size)
    {
        if (size is 1)
        {
            for (var i = 0; i < indices.Length; ++i)
                buffer[i] = (byte)indices[i];
        }
        else
        {
            for (var i = 0; i < indices.Length; ++i)
                BinaryPrimitives.WriteUInt16LittleEndian(buffer[(2 * i)..], indices[i]);
        }
    }

    private static unsafe BinaryWriter WriteHeader(Stream stream, ReadOnlySpan<byte> id, ImageType type, int width, int height,
        int colorMapLength)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(id.Length, byte.MaxValue);
        var colorMap = new ColorMapSpecification
        {
            EntryBitSize = (byte)(colorMapLength >= 0 ? 32 : 0),
            FirstEntry   = 0,
            Length       = (ushort)Math.Clamp(colorMapLength, 0, ushort.MaxValue),
        };
        var header = new Header
        {
            ColorMapType = (byte)(colorMapLength >= 0 ? 1 : 0),
            IdLength     = (byte)id.Length,
            ImageType    = type,
            ColorMap     = colorMap,
            Image = new ImageSpecifications
            {
                AlphaDepth     = 8,
                Width          = (ushort)width,
                Height         = (ushort)height,
                InterleaveMode = InterleaveMode.None,
                OriginX        = 0,
                OriginY        = 0,
                PixelOrder     = PixelOrder.LeftRightTopBottom,
                PixelBitSize   = (byte)(colorMapLength < 0 ? 32 : colorMapLength > byte.MaxValue + 1 ? 16 : 8),
            },
        };
        var b = new BinaryWriter(stream, Encoding.UTF8, true);
        b.Write(new ReadOnlySpan<byte>(&header, sizeof(Header)));
        b.Write(id);
        return b;
    }

    private static bool CreateColorMap(ReadOnlySpan<byte> data, out IReadOnlyCollection<uint> colorMap, out ushort[] indices,
        bool allow16BitIndices)
    {
        var dict   = new Dictionary<uint, ushort>(256);
        var colors = MemoryMarshal.Cast<byte, uint>(data);
        indices  = new ushort[colors.Length];
        colorMap = dict.Keys;
        var maximum = allow16BitIndices ? ushort.MaxValue : byte.MaxValue;
        for (var i = 0; i < indices.Length; ++i)
        {
            var color = colors[i];
            if (dict.TryGetValue(color, out var index))
            {
                indices[i] = index;
            }
            else if (dict.Count == maximum)
            {
                return false;
            }
            else
            {
                indices[i]  = (ushort)dict.Count;
                dict[color] = (ushort)dict.Count;
            }
        }

        return true;
    }

    private static void WriteShuffled(Span<byte> buffer, ReadOnlySpan<byte> colors)
    {
        var i = 0;
        if (Avx2.IsSupported)
        {
            var end  = colors.Length - 32;
            var mask = Vector256.Create(ShuffleIndices);
            for (; i <= end; i += 32)
            {
                var value    = Vector256.LoadUnsafe(in colors[i]);
                var shuffled = Avx2.Shuffle(value, mask);
                MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref shuffled, 1)).CopyTo(buffer[i..]);
            }
        }

        var end2 = colors.Length - 4;
        for (; i <= end2; i += 4)
        {
            buffer[i]     = colors[i + 2];
            buffer[i + 1] = colors[i + 1];
            buffer[i + 2] = colors[i];
            buffer[i + 3] = colors[i + 3];
        }
    }

    private static void WriteShuffled(Span<byte> buffer, uint color)
    {
        buffer[0] = (byte)(color >> 16);
        buffer[1] = (byte)(color >> 8);
        buffer[2] = (byte)color;
        buffer[3] = (byte)(color >> 24);
    }
}
