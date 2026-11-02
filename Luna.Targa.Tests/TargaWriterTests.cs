using Luna.Targa.Structs;

namespace Luna.Targa.Tests;

public class TargaWriterTests
{
    [Fact]
    public void WriteUncompressedRgbaProducesBgraPixelsAnd32BitHeader()
    {
        var encoded = TargaTestData.EncodeRgba(false);

        Assert.Equal(26,                                      encoded.Length);
        Assert.Equal(2,                                       encoded[2]);
        Assert.Equal(32,                                      encoded[16]);
        Assert.Equal(40,                                      encoded[17]);
        Assert.Equal(new byte[] { 3, 2, 1, 0, 6, 5, 4, 127 }, encoded[18..]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RgbaRoundTripPreservesPixelsAndAlpha(bool runLength)
        => Assert.Equal(TargaTestData.Rgba, TargaTestData.Read(TargaTestData.EncodeRgba(runLength)));

    [Theory]
    [InlineData(2,   false)]
    [InlineData(2,   true)]
    [InlineData(256, false)]
    [InlineData(256, true)]
    [InlineData(257, false)]
    [InlineData(257, true)]
    public void PaletteRoundTripUsesCorrectIndexWidthAndPreservesSource(int count, bool runLength)
    {
        // Repeat the palette enough times that the size heuristic retains color mapping.
        var pixels = new byte[count * 8 * 4];
        for (var i = 0; i < pixels.Length / 4; ++i)
        {
            var color = i % count;
            pixels[4 * i]     = (byte)color;
            pixels[4 * i + 1] = (byte)(color >> 8);
            pixels[4 * i + 2] = 73;
            pixels[4 * i + 3] = (byte)(color % 251);
        }

        var       original = pixels.ToArray();
        using var stream   = new MemoryStream();
        TargaWriter.WriteColorMapped(stream, new Image(count, 8, pixels), encodeRunLength: runLength);
        var encoded = stream.ToArray();

        Assert.Equal(runLength ? 9 : 1,     encoded[2]);
        Assert.Equal(count <= 256 ? 8 : 16, encoded[16]);
        Assert.Equal(original,              pixels);
        Assert.Equal(original,              TargaTestData.Read(encoded));
    }
}
