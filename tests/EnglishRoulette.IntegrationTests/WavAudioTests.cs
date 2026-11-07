using EnglishRoulette.Infrastructure.Media;

namespace EnglishRoulette.IntegrationTests;

public sealed class WavAudioTests
{
    [Theory]
    [InlineData(22050, 22050, 1000)]
    [InlineData(16000, 8000, 500)]
    [InlineData(22050, 1, 1)]
    public void Duration_UsesRealFrameCountAndRoundsUp(int sampleRate, int frames, int expectedMilliseconds)
    {
        using var wave = Create(sampleRate, frames);
        Assert.Equal(expectedMilliseconds, WavAudio.DurationMilliseconds(wave));
    }

    [Fact]
    public void Duration_RejectsTruncatedAndNonAudioFiles()
    {
        using var truncated = Create(16000, 16000);
        truncated.SetLength(truncated.Length - 1);
        Assert.Throws<InvalidDataException>(() => WavAudio.DurationMilliseconds(truncated));
        using var invalid = new MemoryStream(new byte[80]);
        Assert.Throws<InvalidDataException>(() => WavAudio.DurationMilliseconds(invalid));
    }

    private static MemoryStream Create(int sampleRate, int frames)
    {
        var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        writer.Write("RIFF"u8);
        writer.Write(36 + frames * 2);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((ushort)1);
        writer.Write((ushort)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((ushort)2);
        writer.Write((ushort)16);
        writer.Write("data"u8);
        writer.Write(frames * 2);
        writer.Write(new byte[frames * 2]);
        stream.Position = 0;
        return stream;
    }
}
