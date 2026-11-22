using System.Buffers.Binary;
using System.Linq;
using System.IO;
using Godot;

namespace Anomaly.Client.Hud;

// M10 slice F: tg's lobby buttons flick an animated PNG when pressed or enabled (tgui-lobby LobbyMenu.tsx useFlick: an <img> of
// "<state>_pressed.png", shown for the APNG's duration). The engine loads a PNG's first frame only, so this reads the APNG's frames:
// each frame's data (IDAT for the first, fdAT after it) becomes a standalone PNG of the frame's size, decoded by the engine and drawn
// onto the canvas at its offset with its blend and dispose operations (the APNG specification's acTL, fcTL and fdAT chunks).

/// <summary>The frames of an animated PNG (one frame for a plain PNG), each with its delay in seconds.</summary>
public sealed class ApngFrames
{
    public IReadOnlyList<(Image Frame, double Seconds)> Frames { get; }

    public double Duration { get; }

    private ApngFrames(List<(Image, double)> frames)
    {
        Frames = frames;
        Duration = frames.Sum(f => f.Item2);
    }

    /// <summary>The frames of <paramref name="png"/>, or null when it is not a PNG the engine decodes.</summary>
    public static ApngFrames Read(byte[] png)
    {
        if (png == null || png.Length < 8 || !png.AsSpan(0, 8).SequenceEqual(Signature))
            return null;
        byte[] ihdr = null;
        var shared = new List<byte[]>(); // the chunks every frame's PNG needs (PLTE, tRNS, gAMA and the like), whole
        var frames = new List<Frame>();
        Frame current = null;
        bool animated = false;
        for (int at = 8; at + 12 <= png.Length;)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(at));
            string type = System.Text.Encoding.ASCII.GetString(png, at + 4, 4);
            if (length < 0 || at + 12 + length > png.Length)
                return null;
            ReadOnlySpan<byte> data = png.AsSpan(at + 8, length);
            switch (type)
            {
                case "IHDR":
                    ihdr = data.ToArray();
                    break;
                case "acTL":
                    animated = true;
                    break;
                case "fcTL":
                    current = new Frame
                    {
                        Width = BinaryPrimitives.ReadInt32BigEndian(data[4..]),
                        Height = BinaryPrimitives.ReadInt32BigEndian(data[8..]),
                        X = BinaryPrimitives.ReadInt32BigEndian(data[12..]),
                        Y = BinaryPrimitives.ReadInt32BigEndian(data[16..]),
                        DelayNum = BinaryPrimitives.ReadUInt16BigEndian(data[20..]),
                        DelayDen = BinaryPrimitives.ReadUInt16BigEndian(data[22..]),
                        Dispose = data[24],
                        Blend = data[25],
                    };
                    frames.Add(current);
                    break;
                case "IDAT":
                    if (current != null) // the default image is the first frame only when an fcTL comes before the IDAT
                        current.Data.Add(data.ToArray());
                    else if (!animated) // a plain PNG: its one frame
                        (current = AddPlain(frames, ihdr)).Data.Add(data.ToArray());
                    break;
                case "fdAT":
                    current?.Data.Add(data[4..].ToArray()); // the sequence number, then the image data
                    break;
                case "IEND":
                    break;
                default:
                    if (type is "PLTE" or "tRNS" or "gAMA" or "cHRM" or "sRGB" or "iCCP" or "sBIT")
                        shared.Add(png.AsSpan(at, 12 + length).ToArray());
                    break;
            }
            at += 12 + length;
        }
        if (ihdr == null || frames.Count == 0)
            return null;

        int width = BinaryPrimitives.ReadInt32BigEndian(ihdr), height = BinaryPrimitives.ReadInt32BigEndian(ihdr.AsSpan(4));
        Image canvas = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        var result = new List<(Image, double)>();
        foreach (Frame frame in frames)
        {
            Image piece = Decode(ihdr, shared, frame);
            if (piece == null)
                return null;
            piece.Convert(Image.Format.Rgba8);
            Image before = frame.Dispose == 2 ? (Image)canvas.Duplicate() : null; // APNG_DISPOSE_OP_PREVIOUS
            var rect = new Rect2I(0, 0, piece.GetWidth(), piece.GetHeight());
            if (frame.Blend == 0) // APNG_BLEND_OP_SOURCE: the frame replaces the region
                canvas.BlitRect(piece, rect, new Vector2I(frame.X, frame.Y));
            else // APNG_BLEND_OP_OVER
                canvas.BlendRect(piece, rect, new Vector2I(frame.X, frame.Y));
            double seconds = (frame.DelayDen == 0 ? frame.DelayNum / 100.0 : frame.DelayNum / (double)frame.DelayDen);
            result.Add(((Image)canvas.Duplicate(), seconds));
            if (frame.Dispose == 1) // APNG_DISPOSE_OP_BACKGROUND: the region is cleared for the next frame
                canvas.FillRect(new Rect2I(frame.X, frame.Y, frame.Width, frame.Height), new Color(0, 0, 0, 0));
            else if (before != null)
                canvas = before;
        }
        return new ApngFrames(result);
    }

    private static Frame AddPlain(List<Frame> frames, byte[] ihdr)
    {
        var frame = new Frame { Width = BinaryPrimitives.ReadInt32BigEndian(ihdr), Height = BinaryPrimitives.ReadInt32BigEndian(ihdr.AsSpan(4)), DelayNum = 0, DelayDen = 1 };
        frames.Add(frame);
        return frame;
    }

    // A standalone PNG of one frame: the image header with the frame's size, the shared chunks, the frame's data as IDAT.
    private static Image Decode(byte[] ihdr, List<byte[]> shared, Frame frame)
    {
        using var stream = new MemoryStream();
        stream.Write(Signature);
        byte[] header = (byte[])ihdr.Clone();
        BinaryPrimitives.WriteInt32BigEndian(header, frame.Width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), frame.Height);
        WriteChunk(stream, "IHDR", header);
        foreach (byte[] chunk in shared)
            stream.Write(chunk);
        foreach (byte[] part in frame.Data)
            WriteChunk(stream, "IDAT", part);
        WriteChunk(stream, "IEND", Array.Empty<byte>());
        var image = new Image();
        return image.LoadPngFromBuffer(stream.ToArray()) == Error.Ok ? image : null;
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        Span<byte> four = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(four, data.Length);
        stream.Write(four);
        byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);
        uint crc = Crc32(typeBytes, data);
        BinaryPrimitives.WriteUInt32BigEndian(four, crc);
        stream.Write(four);
    }

    private static uint Crc32(byte[] type, byte[] data)
    {
        uint c = 0xFFFFFFFF;
        foreach (byte b in type) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        foreach (byte b in data) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFF;
    }

    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        uint c = (uint)n;
        for (int k = 0; k < 8; k++)
            c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    private sealed class Frame
    {
        public int Width, Height, X, Y;
        public ushort DelayNum, DelayDen;
        public byte Dispose, Blend;
        public readonly List<byte[]> Data = new();
    }
}
