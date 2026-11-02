namespace Luna.Targa.Tests;

internal static class TargaTestData
{
    public static readonly byte[][] Colors =
    [
        [255, 0, 0, 255],
        [0, 255, 0, 255],
        [0, 0, 255, 255],
        [255, 255, 255, 255],
    ];

    public static readonly byte[] Rgba = [1, 2, 3, 0, 4, 5, 6, 127];

    public static byte[] Fixture(byte descriptor, byte type, params byte[] data)
    {
        // A 2 x 2, 24-bit image with no ID or palette.
        byte[] header = [0, 0, type, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 2, 0, 24, descriptor];
        return [..header, ..data];
    }

    public static byte[] Read(byte[] data)
        => new TargaReader(data).CreateRgba32Data();

    public static byte[] EncodeRgba(bool runLength)
    {
        using var stream = new MemoryStream();
        TargaWriter.WriteRgba32(stream, new Structs.Image(2, 1, Rgba), encodeRunLength: runLength);
        return stream.ToArray();
    }
}
