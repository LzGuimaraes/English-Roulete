using EnglishRoulette.Infrastructure.Media;

namespace EnglishRoulette.IntegrationTests;

public sealed class ImageInspectionTests
{
    public static byte[] Png() => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4Xyz2HwAGhQKI5KS77gAAAABJRU5ErkJggg==");
    [Fact]
    public void LegacyAppearanceWithoutJson_UsesDefaultPalette()
    {
        var theme = EnglishRoulette.Infrastructure.Admin.CatalogService.AppearanceFromJson("");
        Assert.True(theme.IsValid());
        Assert.Equal("#6D28D9", theme.Primary);
    }
    [Fact]
    public void Png_RecognizesRealContainerAndDimensions()
    {
        var image = ImageInspection.Inspect(Png());
        Assert.Equal("image/png", image.ContentType);
        Assert.Equal(1, image.Width); Assert.Equal(1, image.Height); Assert.False(image.Animated);
    }
    [Fact]
    public void Gif_RecognizesAnimationWithoutExecutingUploadedContent()
    {
        var gif = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
        var still = ImageInspection.Inspect(gif);
        Assert.False(still.Animated);
        var frameStart = Array.IndexOf(gif, (byte)0x21, 19);
        var animation = gif[..^1].Concat(gif[frameStart..^1]).Append((byte)0x3b).ToArray();
        Assert.True(ImageInspection.Inspect(animation).Animated);
    }
    [Fact]
    public void CorruptTruncatedOrExecutableContent_IsRejected()
    {
        var corrupt = Png(); corrupt[45] ^= 1;
        Assert.Throws<InvalidDataException>(() => ImageInspection.Inspect(corrupt));
        Assert.Throws<InvalidDataException>(() => ImageInspection.Inspect(Png()[..20]));
        Assert.Throws<InvalidDataException>(() => ImageInspection.Inspect("<svg onload='alert(1)'></svg>"u8.ToArray()));
        Assert.Throws<InvalidDataException>(() => ImageInspection.Inspect([]));
    }
}
