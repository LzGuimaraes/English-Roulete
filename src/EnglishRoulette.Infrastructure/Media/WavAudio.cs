using System.Text;

namespace EnglishRoulette.Infrastructure.Media;

public static class WavAudio
{
    public static int DurationMilliseconds(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        if (stream.Length < 44 || new string(reader.ReadChars(4)) != "RIFF") { throw Invalid(); }
        var riffSize = reader.ReadUInt32();
        if (riffSize + 8L != stream.Length || new string(reader.ReadChars(4)) != "WAVE") { throw Invalid(); }
        uint bytesPerSecond = 0;
        uint dataLength = 0;
        ushort blockAlign = 0;
        while (stream.Position + 8 <= stream.Length)
        {
            var kind = new string(reader.ReadChars(4));
            var length = reader.ReadUInt32();
            var next = stream.Position + length + (length % 2);
            if (next > stream.Length) { throw Invalid(); }
            if (kind == "fmt ")
            {
                if (length < 16 || reader.ReadUInt16() != 1) { throw Invalid(); }
                var channels = reader.ReadUInt16();
                var sampleRate = reader.ReadUInt32();
                bytesPerSecond = reader.ReadUInt32();
                blockAlign = reader.ReadUInt16();
                var bits = reader.ReadUInt16();
                if (channels == 0 || sampleRate == 0 || bits != 16 || blockAlign != channels * 2 ||
                    bytesPerSecond != (long)sampleRate * blockAlign) { throw Invalid(); }
            }
            else if (kind == "data") { dataLength = length; }
            stream.Position = next;
        }
        if (bytesPerSecond == 0 || dataLength == 0 || blockAlign == 0 || dataLength % blockAlign != 0) { throw Invalid(); }
        return checked((int)Math.Ceiling(dataLength * 1000d / bytesPerSecond));
    }

    private static InvalidDataException Invalid() => new("The asset is not a valid 16-bit PCM WAV file.");
}
