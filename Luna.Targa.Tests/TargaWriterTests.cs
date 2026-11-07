using Luna.Targa.Structs;

namespace Luna.Targa.Tests;

public class TargaWriterTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(129)]
    public void RleRawPacketsIncludeHeadersAndOnlyTheirPixelBytes(int count)
    {
        var pixels = CreatePixels(count, false);
        using var stream = new MemoryStream();
        TargaWriter.WriteRgba32(stream, new Image(count, 1, pixels));
        var encoded = stream.ToArray();

        Assert.Equal(18 + count * 4 + (count + 127) / 128, encoded.Length);
        Assert.Equal(Math.Min(count, 128) - 1, encoded[18]);
        if (count > 128)
            Assert.Equal(0, encoded[18 + 513]);
        Assert.Equal(pixels, TargaTestData.Read(encoded));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void WritesPreservePixelsAndLeaveNonSeekableStreamOpen(bool palette, bool runLength, bool indices16Bit)
    {
        const int count = 40000;
        var pixels = CreatePixels(count, runLength);
        if (palette && !indices16Bit)
        {
            for (var i = 0; i < count; ++i)
            {
                pixels[4 * i] %= 255;
                pixels[4 * i + 1] = 0;
                pixels[4 * i + 3] = 255;
            }
        }

        using var stream = new RecordingWriteStream();
        byte[] id = [1, 2, 3];
        var image = new Image(count, 1, pixels);
        if (palette)
            TargaWriter.WriteColorMapped(stream, image, id, runLength, indices16Bit);
        else
            TargaWriter.WriteRgba32(stream, image, id, runLength);

        var encoded = stream.ToArray();
        Assert.True(stream.CanWrite);
        Assert.Equal(1, stream.FlushCount);
        Assert.Equal(id, new TargaReader(encoded).Id.ToArray());
        Assert.Equal(pixels, TargaTestData.Read(encoded));
        Assert.All(stream.WriteSizes, size => Assert.InRange(size, 1, 4096));
        if (!palette && !runLength)
            Assert.Equal(2 + (pixels.Length + 4095) / 4096, stream.WriteSizes.Count);
        if (palette)
        {
            Assert.Equal(runLength ? 9 : 1, encoded[2]);
            Assert.Equal(indices16Bit ? 16 : 8, encoded[16]);
            if (!runLength)
            {
                var paletteBytes = (encoded[5] | encoded[6] << 8) * 4;
                var indexBytes = count * (indices16Bit ? 2 : 1);
                Assert.Equal(2 + (paletteBytes + 4095) / 4096 + (indexBytes + 4095) / 4096, stream.WriteSizes.Count);
            }
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(1025)]
    public void ColorMapChunksPreservePaletteAndLittleEndianIndices(int colors)
    {
        const int count = 8195;
        var pixels = new byte[count * 4];
        for (var i = 0; i < count; ++i)
        {
            pixels[4 * i]     = (byte)(i % colors);
            pixels[4 * i + 1] = (byte)((i % colors) >> 8);
            pixels[4 * i + 2] = 73;
            pixels[4 * i + 3] = 255;
        }

        using var stream = new RecordingWriteStream();
        TargaWriter.WriteColorMapped(stream, new Image(count, 1, pixels), encodeRunLength: false, allow16BitIndices: true);
        var encoded = stream.ToArray();
        Assert.Equal(1, encoded[2]);
        var indexSize = colors <= 256 ? 1 : 2;
        Assert.Equal(indexSize * 8, encoded[16]);
        for (var i = 0; i < colors; ++i)
            Assert.Equal(new byte[] { 73, (byte)(i >> 8), (byte)i, 255 }, encoded[(18 + i * 4)..(22 + i * 4)]);

        var expected = new byte[count * indexSize];
        for (var i = 0; i < count; ++i)
        {
            expected[i * indexSize] = (byte)(i % colors);
            if (indexSize == 2)
                expected[i * indexSize + 1] = (byte)((i % colors) >> 8);
        }

        Assert.Equal(expected, encoded[(18 + colors * 4)..]);
        Assert.Equal(2 + (colors * 4 + 4095) / 4096 + (expected.Length + 4095) / 4096, stream.WriteSizes.Count);
        Assert.Equal(pixels, TargaTestData.Read(encoded));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ColorMapRleWritesWholeRawAndRepeatedPackets(bool indices16Bit)
    {
        var colors = indices16Bit ? 257 : 2;
        // Keep the 16-bit palette worthwhile under the writer's size heuristic.
        var indices = Enumerable.Range(0, colors).Concat(Enumerable.Repeat(colors - 2, indices16Bit ? 384 : 128)).Append(0).ToArray();
        var pixels = new byte[indices.Length * 4];
        for (var i = 0; i < indices.Length; ++i)
        {
            pixels[4 * i]     = (byte)indices[i];
            pixels[4 * i + 1] = (byte)(indices[i] >> 8);
            pixels[4 * i + 2] = 73;
            pixels[4 * i + 3] = 255;
        }

        using var stream = new RecordingWriteStream();
        TargaWriter.WriteColorMapped(stream, new Image(indices.Length, 1, pixels), allow16BitIndices: true);
        var encoded = stream.ToArray();
        Assert.Equal(9, encoded[2]);
        var payload = encoded[(18 + colors * 4)..];
        if (indices16Bit)
        {
            var expected = new List<byte> { 127 };
            for (var i = 0; i < 128; ++i)
                expected.AddRange([(byte)i, 0]);
            expected.Add(127);
            for (var i = 128; i < 256; ++i)
                expected.AddRange([(byte)i, 0]);
            expected.AddRange([0, 0, 1, 255, 255, 0, 255, 255, 0, 255, 255, 0, 0, 0, 0]);
            Assert.Equal(expected.ToArray(), payload);
            Assert.Equal(new[] { 257, 257, 3, 3, 3, 3, 3 }, stream.WriteSizes.Skip(3).ToArray());
        }
        else
        {
            Assert.Equal(new byte[] { 1, 0, 1, 255, 0, 0, 0 }, payload);
            Assert.Equal(new[] { 3, 2, 2 }, stream.WriteSizes.Skip(3).ToArray());
        }

        Assert.Equal(pixels, TargaTestData.Read(encoded));
    }

    [Theory]
    [InlineData(1023)]
    [InlineData(1024)]
    [InlineData(1025)]
    [InlineData(2051)]
    public void UncompressedChunksWriteExactBgraBytes(int count)
    {
        var pixels = CreatePixels(count, false);
        var expected = pixels.ToArray();
        for (var i = 0; i < expected.Length; i += 4)
            (expected[i], expected[i + 2]) = (expected[i + 2], expected[i]);

        using var stream = new RecordingWriteStream();
        TargaWriter.WriteRgba32(stream, new Image(count, 1, pixels), encodeRunLength: false);

        Assert.Equal(expected, stream.ToArray()[18..]);
        var writes = stream.WriteSizes.Where(size => size > 0).Skip(1).ToArray();
        Assert.Equal((pixels.Length + 4095) / 4096, writes.Length);
        Assert.All(writes[..^1], size => Assert.Equal(4096, size));
        Assert.Equal((pixels.Length - 1) % 4096 + 1, writes[^1]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PaletteFallbackLeavesStreamOpen(bool runLength)
    {
        var pixels = CreatePixels(257, false);
        using var stream = new RecordingWriteStream();
        TargaWriter.WriteColorMapped(stream, new Image(257, 1, pixels), encodeRunLength: runLength);

        var encoded = stream.ToArray();
        Assert.True(stream.CanWrite);
        Assert.Equal(runLength ? 10 : 2, encoded[2]);
        Assert.Equal(pixels, TargaTestData.Read(encoded));
    }

    private static byte[] CreatePixels(int count, bool repeatedRuns)
    {
        var pixels = new byte[count * 4];
        for (var i = 0; i < count; ++i)
        {
            var color = repeatedRuns && (i / 257 & 1) != 0 ? 0 : i % 257;
            pixels[4 * i]     = (byte)color;
            pixels[4 * i + 1] = (byte)(color >> 8);
            pixels[4 * i + 2] = 73;
            pixels[4 * i + 3] = (byte)(color % 251);
        }

        return pixels;
    }

    private sealed class RecordingWriteStream : Stream
    {
        private readonly MemoryStream _data = new();
        private bool _disposed;

        public List<int> WriteSizes { get; } = [];
        public int FlushCount { get; private set; }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => !_disposed;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public byte[] ToArray() => _data.ToArray();
        public override void Flush() => ++FlushCount;
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            WriteSizes.Add(buffer.Length);
            _data.Write(buffer);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            _disposed = true;
            if (disposing)
                _data.Dispose();
            base.Dispose(disposing);
        }
    }

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
        TargaWriter.WriteColorMapped(stream, new Image(count, 8, pixels), encodeRunLength: runLength, allow16BitIndices: true);
        var encoded = stream.ToArray();

        Assert.Equal(runLength ? 9 : 1,     encoded[2]);
        Assert.Equal(count <= 256 ? 8 : 16, encoded[16]);
        Assert.Equal(original,              pixels);
        Assert.Equal(original,              TargaTestData.Read(encoded));
    }
}
