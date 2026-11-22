using System.IO;
using System.IO.Compression;

namespace Anomaly.Client.Shared.Icons;

/// <summary>
/// A PNG decoder for tg's icon sheets (a .dmi is a PNG with an extra text chunk): every colour type and bit depth, palettes
/// with tRNS, plain and Adam7 interlacing, to 8-bit RGBA rasters. Engine-free so that the compositor and its tests need no
/// Godot; the client uploads the finished pictures as textures (Godot never imports the sheets, godot/assets/tg is .gdignore'd).
/// </summary>
public static class PngDecoder
{
    private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <summary>Decodes <paramref name="png"/>; throws <see cref="InvalidDataException"/> for anything it cannot read.</summary>
    public static Raster Decode(byte[] png)
    {
        if (png == null || png.Length < 8)
            throw new InvalidDataException("too short for a PNG");
        for (int i = 0; i < Signature.Length; i++)
        {
            if (png[i] != Signature[i])
                throw new InvalidDataException("not a PNG (bad signature)");
        }

        int width = 0, height = 0, depth = 0, colorType = 0, interlace = 0;
        byte[] palette = null, transparency = null;
        var data = new MemoryStream();
        int position = 8;
        bool header = false, end = false;
        while (!end && position + 12 <= png.Length)
        {
            long length = ((long)png[position] << 24) | ((long)png[position + 1] << 16) | ((long)png[position + 2] << 8) | png[position + 3];
            if (length < 0 || position + 12L + length > png.Length)
                throw new InvalidDataException("a PNG chunk runs past the end of the file");
            string type = new string(new[] { (char)png[position + 4], (char)png[position + 5], (char)png[position + 6], (char)png[position + 7] });
            int body = position + 8;
            switch (type)
            {
                case "IHDR":
                    if (length < 13)
                        throw new InvalidDataException("a short IHDR");
                    width = ReadInt(png, body);
                    height = ReadInt(png, body + 4);
                    depth = png[body + 8];
                    colorType = png[body + 9];
                    if (png[body + 10] != 0 || png[body + 11] != 0)
                        throw new InvalidDataException("an unknown compression or filter method");
                    interlace = png[body + 12];
                    header = true;
                    break;
                case "PLTE":
                    palette = new byte[length];
                    Buffer.BlockCopy(png, body, palette, 0, (int)length);
                    break;
                case "tRNS":
                    transparency = new byte[length];
                    Buffer.BlockCopy(png, body, transparency, 0, (int)length);
                    break;
                case "IDAT":
                    data.Write(png, body, (int)length);
                    break;
                case "IEND":
                    end = true;
                    break;
            }
            position = body + (int)length + 4;
        }

        if (!header)
            throw new InvalidDataException("no IHDR chunk");
        if (width < 1 || height < 1 || (long)width * height > 1 << 28)
            throw new InvalidDataException("a picture of " + width + " x " + height);
        int channels = colorType switch
        {
            0 => 1,
            2 => 3,
            3 => 1,
            4 => 2,
            6 => 4,
            _ => throw new InvalidDataException("unknown colour type " + colorType),
        };
        if (depth != 1 && depth != 2 && depth != 4 && depth != 8 && depth != 16)
            throw new InvalidDataException("bit depth " + depth);
        if (colorType == 3 && palette == null)
            throw new InvalidDataException("a palette picture without a palette");

        data.Position = 0;
        var result = new Raster(width, height);
        int bitsPerPixel = channels * depth;
        int bytesPerPixel = Math.Max(1, bitsPerPixel / 8);
        byte[] raw;
        using (var inflate = new ZLibStream(data, CompressionMode.Decompress))
        {
            if (interlace == 0)
            {
                // The size of a plain picture's data is known: inflated straight into one array (a 4800 x 4320 sheet is 83 MB, and
                // growing a stream and copying it out cost as much again).
                raw = new byte[(long)height * (1 + ((long)width * bitsPerPixel + 7) / 8)];
                int read = inflate.ReadAtLeast(raw, raw.Length, throwOnEndOfStream: false);
                if (read < raw.Length)
                    Array.Resize(ref raw, read); // short: DecodePass reports the truncation
            }
            else
            {
                using var output = new MemoryStream();
                inflate.CopyTo(output);
                raw = output.ToArray();
            }
        }

        int at = 0;
        if (interlace == 0)
        {
            DecodePass(raw, ref at, width, height, 0, 0, 1, 1, bitsPerPixel, bytesPerPixel, depth, colorType, palette, transparency, result);
        }
        else if (interlace == 1)
        {
            int[] x0 = { 0, 4, 0, 2, 0, 1, 0 }, y0 = { 0, 0, 4, 0, 2, 0, 1 }, dx = { 8, 8, 4, 4, 2, 2, 1 }, dy = { 8, 8, 8, 4, 4, 2, 2 };
            for (int pass = 0; pass < 7; pass++)
            {
                int passWidth = (width - x0[pass] + dx[pass] - 1) / dx[pass];
                int passHeight = (height - y0[pass] + dy[pass] - 1) / dy[pass];
                if (passWidth <= 0 || passHeight <= 0)
                    continue;
                DecodePass(raw, ref at, passWidth, passHeight, x0[pass], y0[pass], dx[pass], dy[pass], bitsPerPixel, bytesPerPixel, depth, colorType, palette, transparency, result);
            }
        }
        else
        {
            throw new InvalidDataException("interlace method " + interlace);
        }
        return result;
    }

    private static int ReadInt(byte[] bytes, int offset)
        => (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];

    // Unfilters the scanlines of one pass and stores its pixels at (x0 + i * dx, y0 + j * dy) of the result.
    private static void DecodePass(byte[] raw, ref int at, int passWidth, int passHeight, int x0, int y0, int dx, int dy,
        int bitsPerPixel, int bytesPerPixel, int depth, int colorType, byte[] palette, byte[] transparency, Raster result)
    {
        int stride = (passWidth * bitsPerPixel + 7) / 8;
        byte[] previous = new byte[stride];
        byte[] line = new byte[stride];
        for (int row = 0; row < passHeight; row++)
        {
            if (at + 1 + stride > raw.Length)
                throw new InvalidDataException("the image data is truncated");
            int filter = raw[at++];
            Buffer.BlockCopy(raw, at, line, 0, stride);
            at += stride;
            Unfilter(filter, line, previous, bytesPerPixel);
            int y = y0 + row * dy;
            if (colorType == 6 && depth == 8 && dx == 1)
            {
                // RGBA8, the converted sheets' format: the unfiltered line is the row's pixels.
                Buffer.BlockCopy(line, 0, result.Pixels, y * result.Width * 4, stride);
                (previous, line) = (line, previous);
                continue;
            }
            for (int col = 0; col < passWidth; col++)
            {
                int x = x0 + col * dx;
                WritePixel(line, col, depth, colorType, palette, transparency, result.Pixels, (y * result.Width + x) * 4);
            }
            (previous, line) = (line, previous);
        }
    }

    private static void Unfilter(int filter, byte[] line, byte[] previous, int bpp)
    {
        switch (filter)
        {
            case 0:
                break;
            case 1:
                for (int i = bpp; i < line.Length; i++)
                    line[i] = (byte)(line[i] + line[i - bpp]);
                break;
            case 2:
                for (int i = 0; i < line.Length; i++)
                    line[i] = (byte)(line[i] + previous[i]);
                break;
            case 3:
                for (int i = 0; i < line.Length; i++)
                {
                    int left = i >= bpp ? line[i - bpp] : 0;
                    line[i] = (byte)(line[i] + ((left + previous[i]) >> 1));
                }
                break;
            case 4:
                // Paeth; the first pixel has no left neighbours (a = c = 0, so the predictor is the byte above).
                for (int i = 0; i < bpp && i < line.Length; i++)
                    line[i] = (byte)(line[i] + previous[i]);
                for (int i = bpp; i < line.Length; i++)
                {
                    int a = line[i - bpp], b = previous[i], c = previous[i - bpp];
                    int pa = Math.Abs(b - c), pb = Math.Abs(a - c), pc = Math.Abs(a + b - c - c);
                    int predictor = pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
                    line[i] = (byte)(line[i] + predictor);
                }
                break;
            default:
                throw new InvalidDataException("filter type " + filter);
        }
    }

    private static void WritePixel(byte[] line, int col, int depth, int colorType, byte[] palette, byte[] transparency, byte[] dest, int d)
    {
        switch (colorType)
        {
            case 6:
                if (depth == 8)
                {
                    int s = col * 4;
                    dest[d] = line[s];
                    dest[d + 1] = line[s + 1];
                    dest[d + 2] = line[s + 2];
                    dest[d + 3] = line[s + 3];
                }
                else
                {
                    int s = col * 8;
                    dest[d] = line[s];
                    dest[d + 1] = line[s + 2];
                    dest[d + 2] = line[s + 4];
                    dest[d + 3] = line[s + 6];
                }
                break;
            case 2:
            {
                int step = depth == 8 ? 1 : 2;
                int s = col * 3 * step;
                byte r = line[s], g = line[s + step], b = line[s + 2 * step];
                dest[d] = r;
                dest[d + 1] = g;
                dest[d + 2] = b;
                dest[d + 3] = 255;
                if (transparency != null && transparency.Length >= 6)
                {
                    // tRNS names one colour (16-bit samples) that is fully transparent.
                    int tr = (transparency[0] << 8) | transparency[1], tg = (transparency[2] << 8) | transparency[3], tb = (transparency[4] << 8) | transparency[5];
                    int pr = depth == 8 ? r : (line[s] << 8) | line[s + 1];
                    int pg = depth == 8 ? g : (line[s + 2] << 8) | line[s + 3];
                    int pb = depth == 8 ? b : (line[s + 4] << 8) | line[s + 5];
                    if (pr == tr && pg == tg && pb == tb)
                        dest[d + 3] = 0;
                }
                break;
            }
            case 4:
                if (depth == 8)
                {
                    byte g = line[col * 2];
                    dest[d] = dest[d + 1] = dest[d + 2] = g;
                    dest[d + 3] = line[col * 2 + 1];
                }
                else
                {
                    byte g = line[col * 4];
                    dest[d] = dest[d + 1] = dest[d + 2] = g;
                    dest[d + 3] = line[col * 4 + 2];
                }
                break;
            case 0:
            {
                int sample = ReadSample(line, col, depth);
                byte g = depth == 16 ? (byte)(sample >> 8) : (byte)(sample * 255 / ((1 << depth) - 1));
                dest[d] = dest[d + 1] = dest[d + 2] = g;
                dest[d + 3] = 255;
                if (transparency != null && transparency.Length >= 2 && sample == ((transparency[0] << 8) | transparency[1]))
                    dest[d + 3] = 0;
                break;
            }
            case 3:
            {
                int index = ReadSample(line, col, depth);
                if (index * 3 + 2 >= palette.Length)
                    throw new InvalidDataException("a palette index outside the palette");
                dest[d] = palette[index * 3];
                dest[d + 1] = palette[index * 3 + 1];
                dest[d + 2] = palette[index * 3 + 2];
                dest[d + 3] = transparency != null && index < transparency.Length ? transparency[index] : (byte)255;
                break;
            }
        }
    }

    // One sample of a grey or palette scanline (depths 1, 2, 4, 8, 16), high bits first.
    private static int ReadSample(byte[] line, int col, int depth)
    {
        if (depth == 8)
            return line[col];
        if (depth == 16)
            return (line[col * 2] << 8) | line[col * 2 + 1];
        int perByte = 8 / depth;
        int shift = (perByte - 1 - col % perByte) * depth;
        return (line[col / perByte] >> shift) & ((1 << depth) - 1);
    }
}
