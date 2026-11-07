using System.Buffers.Binary;

namespace EnglishRoulette.Infrastructure.Media;

public sealed record ImageInfo(string ContentType, int Width, int Height, bool Animated);

// Bounded container inspection; no scripts, SVG, external URLs or server-side image execution.
public static class ImageInspection
{
    public static ImageInfo Inspect(byte[] bytes)
    {
        try
        {
            ImageInfo info = bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? Png(bytes)
                : bytes.AsSpan().StartsWith("GIF87a"u8) || bytes.AsSpan().StartsWith("GIF89a"u8) ? Gif(bytes)
                : bytes.AsSpan().StartsWith(new byte[] { 255, 216 }) ? Jpeg(bytes) : throw Invalid();
            if (info.Width is < 1 or > 4096 || info.Height is < 1 or > 4096 || (long)info.Width * info.Height > 8000000)
            { throw Invalid(); }
            return info;
        }
        catch (Exception error) when (error is IndexOutOfRangeException or ArgumentOutOfRangeException or OverflowException)
        { throw Invalid(); }
    }
    private static ImageInfo Png(byte[] bytes)
    {
        var position = 8; var width = 0; var height = 0; var frames = 1; var data = false; var end = false;
        while (position <= bytes.Length - 12)
        {
            var length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(position)));
            if (length > bytes.Length - position - 12) { throw Invalid(); }
            var type = bytes.AsSpan(position + 4, 4);
            var body = bytes.AsSpan(position + 8, length);
            uint crc = uint.MaxValue;
            foreach (var value in bytes.AsSpan(position + 4, length + 4))
            {
                crc ^= value;
                for (var bit = 0; bit < 8; bit++) { crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u); }
            }
            if (~crc != BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(position + length + 8))) { throw Invalid(); }
            if (position == 8 && !type.SequenceEqual("IHDR"u8)) { throw Invalid(); }
            if (type.SequenceEqual("IHDR"u8))
            {
                if (width != 0 || length != 13) { throw Invalid(); }
                width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(body));
                height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(body[4..]));
            }
            if (type.SequenceEqual("acTL"u8))
            {
                if (length != 8) { throw Invalid(); }
                frames = checked((int)BinaryPrimitives.ReadUInt32BigEndian(body));
            }
            data |= type.SequenceEqual("IDAT"u8) && length > 0;
            position += length + 12;
            if (type.SequenceEqual("IEND"u8)) { end = length == 0 && position == bytes.Length; break; }
        }
        if (!data || !end || frames is < 1 or > 120 || (long)width * height * frames > 50000000) { throw Invalid(); }
        return new("image/png", width, height, frames > 1);
    }
    private static ImageInfo Gif(byte[] bytes)
    {
        var width = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(6));
        var height = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8));
        var packed = bytes[10];
        var position = 13 + ((packed & 128) == 0 ? 0 : 3 * (1 << ((packed & 7) + 1)));
        var frames = 0;
        void SkipBlocks()
        {
            while (true)
            {
                var length = bytes[position++];
                if (length == 0) { return; }
                if (length > bytes.Length - position) { throw Invalid(); }
                position += length;
            }
        }
        while (position < bytes.Length)
        {
            var marker = bytes[position++];
            if (marker == 0x3b && position == bytes.Length && frames > 0) { return new("image/gif", width, height, frames > 1); }
            if (marker == 0x21) { position++; SkipBlocks(); continue; }
            if (marker != 0x2c || position + 9 > bytes.Length) { throw Invalid(); }
            var frameWidth = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(position + 4));
            var frameHeight = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(position + 6));
            if (frameWidth == 0 || frameHeight == 0 || frameWidth > width || frameHeight > height) { throw Invalid(); }
            packed = bytes[position + 8]; position += 9;
            if ((packed & 128) != 0) { position += 3 * (1 << ((packed & 7) + 1)); }
            var codeSize = bytes[position++];
            if (codeSize is < 2 or > 8) { throw Invalid(); }
            frames++;
            if (frames > 120 || (long)width * height * frames > 50000000) { throw Invalid(); }
            SkipBlocks();
        }
        throw Invalid();
    }
    private static ImageInfo Jpeg(byte[] bytes)
    {
        var position = 2; var width = 0; var height = 0; var scan = false;
        if (bytes.Length < 4 || bytes[^2] != 255 || bytes[^1] != 217) { throw Invalid(); }
        while (position < bytes.Length - 2)
        {
            if (bytes[position++] != 255) { throw Invalid(); }
            while (bytes[position] == 255) { position++; }
            var marker = bytes[position++];
            if (marker == 0xda)
            {
                var headerLength = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(position));
                if (headerLength < 6 || headerLength >= bytes.Length - position - 2) { throw Invalid(); }
                scan = true; break;
            }
            if (marker is 0xd8 or 0xd9 or 0x00) { throw Invalid(); }
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(position));
            if (length < 2 || length > bytes.Length - position) { throw Invalid(); }
            if (marker is 0xc0 or 0xc1 or 0xc2)
            {
                if (length < 8) { throw Invalid(); }
                height = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(position + 3));
                width = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(position + 5));
            }
            position += length;
        }
        if (!scan || width == 0 || height == 0) { throw Invalid(); }
        return new("image/jpeg", width, height, false);
    }
    private static InvalidDataException Invalid() => new("Use PNG/APNG, JPEG ou GIF válidos, até 4096 px, 8 megapixels e 120 quadros (50 megapixels acumulados).");
}
