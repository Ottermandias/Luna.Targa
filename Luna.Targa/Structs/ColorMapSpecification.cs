using System.Runtime.InteropServices;

namespace Luna.Targa.Structs;

/// <summary> Specifications for the color map if in use. Otherwise, filled with 0. </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ColorMapSpecification
{
    /// <summary> The index of the first color map entry. </summary>
    public ushort FirstEntry;

    /// <summary> The total number of color map entries. </summary>
    public ushort Length;

    /// <summary> The number of bits for each color map entry. </summary>
    public byte EntryBitSize;

    /// <summary> The number of bytes for each color map entry. </summary>
    public byte EntryByteSize
        => (byte)((EntryBitSize + 7) / 8);
}
