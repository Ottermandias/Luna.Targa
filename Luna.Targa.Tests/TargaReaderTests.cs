namespace Luna.Targa.Tests;

public class TargaReaderTests
{
    [Theory]
    [InlineData(0,  false)]
    [InlineData(16, false)]
    [InlineData(32, false)]
    [InlineData(48, false)]
    [InlineData(0,  true)]
    [InlineData(16, true)]
    [InlineData(32, true)]
    [InlineData(48, true)]
    public void DecodeNormalizesOrientation(byte descriptor, bool runLength)
    {
        var data = new List<byte>();
        for (var i = 0; i < 4; ++i)
        {
            var x = (descriptor & 16) is not 0 ? 1 - i % 2 : i % 2;
            var y = (descriptor & 32) is not 0 ? i / 2 : 1 - i / 2;
            var c = TargaTestData.Colors[y * 2 + x];
            data.AddRange([c[2], c[1], c[0]]);
        }

        var payload = runLength ? [3, ..data] : data.ToArray();
        var encoded = TargaTestData.Fixture(descriptor, (byte)(runLength ? 10 : 2), payload);

        Assert.Equal(TargaTestData.Colors.SelectMany(c => c).ToArray(), TargaTestData.Read(encoded));
    }

    [Fact]
    public void DecodeExpandsRepeatedPacket()
    {
        var encoded  = TargaTestData.Fixture(32, 10, 131, 0, 0, 255);
        var expected = Enumerable.Repeat(TargaTestData.Colors[0], 4).SelectMany(c => c).ToArray();

        Assert.Equal(expected, TargaTestData.Read(encoded));
    }

    [Theory]
    [MemberData(nameof(TruncationLengths))]
    public void RejectTruncatedUncompressedImage(int length)
    {
        var truncated = TargaTestData.EncodeRgba(false)[..length];

        Assert.False(TargaReader.TryReadTarga(truncated, out _));
        Assert.Throws<InvalidDataException>(() => TargaTestData.Read(truncated));
    }

    public static TheoryData<int> TruncationLengths
    {
        get
        {
            var lengths = new TheoryData<int>();
            for (var i = 0; i < 26; ++i)
                lengths.Add(i);
            return lengths;
        }
    }

    [Fact]
    public void RejectPacketExceedingImageDimensions()
    {
        var encoded = TargaTestData.Fixture(32, 10, 132, 0, 0, 255);

        Assert.Throws<InvalidDataException>(() => TargaTestData.Read(encoded));
    }

    [Fact]
    public void RejectUnsupportedImageType()
    {
        var encoded = TargaTestData.Fixture(32, 32);

        Assert.False(TargaReader.TryReadTarga(encoded, out _));
        Assert.False(TargaReader.ValidateSize(encoded, out _));
    }

    [Fact]
    public void RejectExcessivePixelCount()
    {
        var encoded               = TargaTestData.Fixture(32, 10, 131, 0, 0, 255);
        encoded[12] = encoded[13] = encoded[14] = encoded[15] = 255;

        Assert.False(TargaReader.TryReadTarga(encoded, out _));
    }

    [Fact]
    public void ValidateRepeatedPacketReportsConsumedBytes()
    {
        var reader = new TargaReader(TargaTestData.Fixture(32, 10, 131, 0, 0, 255));

        Assert.True(TargaReader.ValidateRunLength(reader.Header, reader.ImageData, out var length));
        Assert.Equal(4, length);
    }

    [Theory]
    [InlineData(new byte[] { 131, 0, 0 })]
    [InlineData(new byte[] { 3, 0, 0, 255 })]
    public void ValidateTruncatedPacketReturnsFalse(byte[] payload)
    {
        var reader = new TargaReader(TargaTestData.Fixture(32, 10, 131, 0, 0, 255));

        Assert.False(TargaReader.ValidateRunLength(reader.Header, payload, out _));
    }
}
