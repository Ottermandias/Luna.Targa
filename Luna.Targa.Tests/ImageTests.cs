using Luna.Targa.Structs;

namespace Luna.Targa.Tests;

public class ImageTests
{
    [Fact]
    public void CloneEmptyImagePreservesEmptyDimensionsAndPixels()
    {
        var image = Image.Empty.Clone();

        Assert.Equal(0, image.Width);
        Assert.Equal(0, image.Height);
        Assert.Empty(image.Pixels);
    }
}
