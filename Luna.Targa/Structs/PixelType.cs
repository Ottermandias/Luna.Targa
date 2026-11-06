using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Luna.Targa.Structs;

/// <summary> All pixel types for image data supported by the Targa format. </summary>
public enum PixelType : byte
{
    /// <summary> An invalid combination of header specifications. </summary>
    Invalid,

    /// <summary> No image data is contained at all. </summary>
    None,

    /// <summary> 8-bit grayscale luminance pixels. </summary>
    Grayscale,

    /// <summary> 16-bit, split into 8-bit luminance and 8-bit alpha, pixels. </summary>
    GrayscaleAlpha,

    /// <summary> 16-bit, split into 5-bit blue, 5-bit green, and 5-bit red with 1 bit unused, pixels </summary>
    Rgb555,

    /// <summary> 16-bit, split into 5-bit blue, 5-bit green, 5-bit red, and 1-bit alpha unused, pixels </summary>
    Argb1555,

    /// <summary> 24-bit, split into 8-bit red, 8-bit green, and 8-bit blue, pixels. </summary>
    Bgr888,

    /// <summary> 32-bit, split into 8-bit red, 8-bit green, 8-bit blue, and 8 bits unused, pixels. </summary>
    Bgrx8888,

    /// <summary> 32-bit, split into 8-bit red, 8-bit green, 8-bit blue, and 8-bit alpha, pixels. </summary>
    Bgra8888,

    /// <summary> 8-bit indices into a color map. </summary>
    Index1,

    /// <summary> 16-bit indices into a color map. </summary>
    Index2,
}

internal static class PixelTypeExtensions
{
    extension(PixelType type)
    {
        public byte Bytes
            => type switch
            {
                PixelType.Invalid        => 0,
                PixelType.None           => 0,
                PixelType.Grayscale      => 1,
                PixelType.GrayscaleAlpha => 2,
                PixelType.Rgb555         => 2,
                PixelType.Argb1555       => 2,
                PixelType.Bgr888         => 3,
                PixelType.Bgrx8888       => 4,
                PixelType.Bgra8888       => 4,
                PixelType.Index1         => 1,
                PixelType.Index2         => 2,
                _                        => 0,
            };
    }
}

internal interface IPixelType
{
    public                 uint RawValue { get; }
    public abstract static byte Size     { get; }

    protected static uint Combine(byte r, byte g, byte b, byte a)
        => r | ((uint)g << 8) | ((uint)b << 16) | ((uint)a << 24);

    public static void ConvertBatch<TPixel, TContext>(ReadOnlySpan<byte> data, Span<uint> rgba, in TContext context, int repetitions)
        where TPixel : unmanaged, IPixelType<TContext>
        where TContext : allows ref struct
    {
        var pixels = MemoryMarshal.Cast<byte, TPixel>(data);
        if (pixels.Length is 1)
        {
            var pixel = pixels[0];
            var color = pixel.GetRgba(context);
            rgba[..repetitions].Fill(color);
            return;
        }

        Debug.Assert(repetitions is 1, $"Package somehow has repetitions {repetitions} and pixel length {pixels.Length}.");
        for (var i = 0; i < pixels.Length; ++i)
        {
            var pixel = pixels[i];
            var color = pixel.GetRgba(context);
            rgba[i] = color;
        }
    }

    public static byte Convert5Bit(ushort value, int shift)
    {
        var v = (value >> shift) & 0b11111;
        return (byte)((v << 3) | (v >> 2));
    }
}

internal interface IPixelType<TContext> : IPixelType where TContext : allows ref struct
{
    public                 uint GetRgba(in TContext context);
    public abstract static void ConvertBatch(ReadOnlySpan<byte> data, Span<uint> rgba, in TContext context, int repetitions);
}

internal struct NopContext;

internal readonly ref struct ColorMapContext<TPixel>(ReadOnlySpan<TPixel> data, ushort startOffset)
    where TPixel : unmanaged, IPixelType<NopContext>
{
    public ColorMapContext(ReadOnlySpan<byte> data, ushort startOffset)
        : this(MemoryMarshal.Cast<byte, TPixel>(data), startOffset)
    { }

    public readonly ushort               StartOffset = startOffset;
    public readonly ReadOnlySpan<TPixel> Data        = data;
}

internal readonly record struct GrayscalePixel(byte Luminance) : IPixelType<NopContext>
{
    public uint GetRgba(in NopContext context)
        => IPixelType.Combine(Luminance, Luminance, Luminance, byte.MaxValue);

    public static void ConvertBatch(ReadOnlySpan<byte> data, Span<uint> rgba, in NopContext context, int repetitions)
        => IPixelType.ConvertBatch<GrayscalePixel, NopContext>(data, rgba, context, repetitions);

    public uint RawValue
        => Luminance;

    public static byte Size
        => 1;
}

internal readonly struct GrayscaleAlphaPixel(ushort data) : IPixelType<NopContext>
{
    public byte Luminance
        => (byte)data;

    public byte Alpha
        => (byte)(data >> 8);

    public uint GetRgba(in NopContext context)
        => IPixelType.Combine(Luminance, Luminance, Luminance, Alpha);

    public static void ConvertBatch(ReadOnlySpan<byte> data, Span<uint> rgba, in NopContext context, int repetitions)
        => IPixelType.ConvertBatch<GrayscaleAlphaPixel, NopContext>(data, rgba, context, repetitions);

    public uint RawValue
        => data;

    public static byte Size
        => 2;
}

internal readonly struct Rgb555Pixel(ushort data) : IPixelType<NopContext>
{
    public byte R
        => IPixelType.Convert5Bit(data, 10);

    public byte G
        => IPixelType.Convert5Bit(data, 5);

    public byte B
        => IPixelType.Convert5Bit(data, 0);

    public uint GetRgba(in NopContext context)
        => IPixelType.Combine(R, G, B, byte.MaxValue);

    public static void ConvertBatch(ReadOnlySpan<byte> data, Span<uint> rgba, in NopContext context, int repetitions)
        => IPixelType.ConvertBatch<Rgb555Pixel, NopContext>(data, rgba, context, repetitions);

    public uint RawValue
        => data;

    public static byte Size
        => 2;
}

internal readonly struct Argb1555Pixel(ushort data) : IPixelType<NopContext>
{
    public byte A
        => (data & 0x8000) is 0 ? (byte)0 : byte.MaxValue;

    public byte R
        => IPixelType.Convert5Bit(data, 10);

    public byte G
        => IPixelType.Convert5Bit(data, 5);

    public byte B
        => IPixelType.Convert5Bit(data, 0);


    public uint GetRgba(in NopContext context)
        => IPixelType.Combine(R, G, B, A);

    public static void ConvertBatch(ReadOnlySpan<byte> data, Span<uint> rgba, in NopContext context, int repetitions)
        => IPixelType.ConvertBatch<Argb1555Pixel, NopContext>(data, rgba, context, repetitions);

    public uint RawValue
        => data;

    public static byte Size
        => 2;
}

internal readonly struct Bgr888Pixel(byte b, byte g, byte r) : IPixelType<NopContext>
{
    public uint GetRgba(in NopContext context)
        => IPixelType.Combine(r, g, b, byte.MaxValue);

    public static void ConvertBatch(ReadOnlySpan<byte> data, Span<uint> rgba, in NopContext context, int repetitions)
        => IPixelType.ConvertBatch<Bgr888Pixel, NopContext>(data, rgba, context, repetitions);

    public uint RawValue
        => IPixelType.Combine(b, g, r, 0);

    public static byte Size
        => 3;
}

internal readonly struct Bgrx8888Pixel(uint value) : IPixelType<NopContext>
{
    public byte R
        => (byte)(value >> 16);

    public byte G
        => (byte)(value >> 8);

    public byte B
        => (byte)value;

    public uint GetRgba(in NopContext context)
        => IPixelType.Combine(R, G, B, byte.MaxValue);

    public static void ConvertBatch(ReadOnlySpan<byte> data, Span<uint> rgba, in NopContext context, int repetitions)
        => IPixelType.ConvertBatch<Bgrx8888Pixel, NopContext>(data, rgba, context, repetitions);

    public uint RawValue
        => value;

    public static byte Size
        => 4;
}

internal readonly struct Bgra8888Pixel(uint value) : IPixelType<NopContext>
{
    public byte A
        => (byte)(value >> 24);

    public byte R
        => (byte)(value >> 16);

    public byte G
        => (byte)(value >> 8);

    public byte B
        => (byte)value;

    public uint GetRgba(in NopContext context)
        => IPixelType.Combine(R, G, B, A);

    public static void ConvertBatch(ReadOnlySpan<byte> data, Span<uint> rgba, in NopContext context, int repetitions)
        => IPixelType.ConvertBatch<Bgra8888Pixel, NopContext>(data, rgba, context, repetitions);

    public uint RawValue
        => value;

    public static byte Size
        => 4;
}

internal readonly struct Index1<TPixelType>(byte value)
    : IPixelType<ColorMapContext<TPixelType>> where TPixelType : unmanaged, IPixelType<NopContext>
{
    public uint GetRgba(in ColorMapContext<TPixelType> context)
    {
        var idx = value - context.StartOffset;
        if ((uint)idx >= (uint)context.Data.Length)
            throw new InvalidDataException("Unmapped color map index encountered.");

        var pixel = context.Data[idx];
        return pixel.GetRgba(default);
    }

    public static void ConvertBatch(ReadOnlySpan<byte> data, Span<uint> rgba, in ColorMapContext<TPixelType> context, int repetitions)
        => IPixelType.ConvertBatch<Index1<TPixelType>, ColorMapContext<TPixelType>>(data, rgba, context, repetitions);

    public uint RawValue
        => value;

    public static byte Size
        => 1;
}

internal readonly struct Index2<TPixelType>(ushort value)
    : IPixelType<ColorMapContext<TPixelType>> where TPixelType : unmanaged, IPixelType<NopContext>
{
    public uint GetRgba(in ColorMapContext<TPixelType> context)
    {
        var idx = value - context.StartOffset;
        if ((uint)idx >= (uint)context.Data.Length)
            throw new InvalidDataException("Unmapped color map index encountered.");

        var pixel = context.Data[idx];
        return pixel.GetRgba(default);
    }

    public static void ConvertBatch(ReadOnlySpan<byte> data, Span<uint> rgba, in ColorMapContext<TPixelType> context, int repetitions)
        => IPixelType.ConvertBatch<Index2<TPixelType>, ColorMapContext<TPixelType>>(data, rgba, context, repetitions);

    public uint RawValue
        => value;

    public static byte Size
        => 2;
}
