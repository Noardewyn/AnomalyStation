using System.IO;

namespace Anomaly.Tools.Sounds;

/// <summary>What the tool needs from an Ogg Vorbis file: sample rate, channels and length.</summary>
/// <param name="SampleRate">The Vorbis identification header's sample rate (Hz).</param>
/// <param name="Channels">The channel count.</param>
/// <param name="Granule">The last page's granule position: the number of PCM frames in the stream.</param>
public sealed record OggInfo(int SampleRate, int Channels, long Granule, string Format = "ogg")
{
    /// <summary>The length in deciseconds as rust-g's <c>sound_len</c> reports it (a fraction, not rounded to whole tenths); 0 for an empty stream.</summary>
    public double Deciseconds => SampleRate <= 0 ? 0 : Math.Round(Granule * 10.0 / SampleRate, 4);

    /// <summary>
    /// Reads the headers by hand (no library): the first page carries the Vorbis identification packet (type 1, "vorbis", version,
    /// channels, sample rate); the length is the granule position of the last page that has one (the file's final page, normally).
    /// </summary>
    /// <exception cref="InvalidDataException">The bytes are no Ogg Vorbis stream.</exception>
    public static OggInfo Read(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 12 && data.Slice(0, 4).SequenceEqual("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WAVE"u8))
            return ReadWave(data);
        if (data.Length >= 4 && (data.Slice(0, 3).SequenceEqual("ID3"u8) || (data[0] == 0xFF && (data[1] & 0xE0) == 0xE0)))
            return ReadMp3(data);
        if (!TryPage(data, 0, out int headerSize, out int bodySize, out _))
            throw new InvalidDataException("not an Ogg file (no OggS page at the start)");
        ReadOnlySpan<byte> body = data.Slice(headerSize, bodySize); // the page starts at 0
        // type 1, "vorbis", u32 version, u8 channels, u32 sample rate.
        if (body.Length < 16 || body[0] != 1 || !body.Slice(1, 6).SequenceEqual("vorbis"u8))
            throw new InvalidDataException("the first Ogg packet is no Vorbis identification header");
        if (BitConverter.ToUInt32(body.Slice(7, 4)) != 0)
            throw new InvalidDataException("unknown Vorbis version");
        int channels = body[11];
        int rate = (int)BitConverter.ToUInt32(body.Slice(12, 4));
        if (channels < 1 || rate < 1)
            throw new InvalidDataException("the Vorbis identification header holds no channels or sample rate");

        // The last page with a valid granule: scan backwards for "OggS" that starts a page ending inside the data.
        long granule = 0;
        for (int at = data.Length - 27; at >= 0; at--)
        {
            if (data[at] != (byte)'O' || !TryPage(data, at, out _, out _, out long g))
                continue;
            if (g >= 0)
            {
                granule = g;
                break;
            }
        }
        return new OggInfo(rate, channels, granule);
    }

    /// <summary>
    /// A RIFF/WAVE file under an .ogg name (tg has one, vehicles/mecha/justice_shield_broken.ogg): the format chunk gives rate and channels, the
    /// data chunk's size over the block size the frames. Godot's Ogg loader cannot play it; the index says <c>"wav"</c> so that the client can tell.
    /// </summary>
    private static OggInfo ReadWave(ReadOnlySpan<byte> data)
    {
        int rate = 0, channels = 0, block = 0;
        long frames = -1;
        int at = 12;
        while (at + 8 <= data.Length)
        {
            uint size = BitConverter.ToUInt32(data.Slice(at + 4, 4));
            ReadOnlySpan<byte> id = data.Slice(at, 4);
            if (id.SequenceEqual("fmt "u8) && size >= 16 && at + 8 + 16 <= data.Length)
            {
                channels = BitConverter.ToUInt16(data.Slice(at + 10, 2));
                rate = (int)BitConverter.ToUInt32(data.Slice(at + 12, 4));
                block = BitConverter.ToUInt16(data.Slice(at + 20, 2));
            }
            else if (id.SequenceEqual("data"u8))
            {
                long bytes = Math.Min(size, (uint)(data.Length - at - 8));
                frames = block > 0 ? bytes / block : -1;
                break;
            }
            at += 8 + (int)Math.Min(size + (size & 1), int.MaxValue - 16);
        }
        if (channels < 1 || rate < 1 || frames < 0)
            throw new InvalidDataException("a WAVE file without a usable fmt or data chunk");
        return new OggInfo(rate, channels, frames, "wav");
    }

    private static readonly int[] Mpeg1Layer3Kbps = { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320 };
    private static readonly int[] Mpeg2Layer3Kbps = { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160 };

    /// <summary>
    /// An MP3 (tg has one, items/weapons/earthcracker_bang.mp3): an ID3v2 tag is skipped, then the MPEG audio Layer III frames are walked
    /// to the first non-frame (an ID3v1 trailer); the first frame's header gives rate and channels, the frame count times the samples a
    /// frame holds the length. The index says <c>"mp3"</c> so that the client loads it as Godot's MP3 stream.
    /// </summary>
    private static OggInfo ReadMp3(ReadOnlySpan<byte> data)
    {
        int at = 0;
        if (data.Slice(0, 3).SequenceEqual("ID3"u8) && data.Length >= 10)
            at = 10 + (data[6] << 21 | data[7] << 14 | data[8] << 7 | data[9]) + ((data[5] & 0x10) != 0 ? 10 : 0);
        int rate = 0, channels = 0;
        long samples = 0;
        while (at + 4 <= data.Length)
        {
            int version = (data[at + 1] >> 3) & 3; // 3 MPEG-1, 2 MPEG-2, 0 MPEG-2.5
            int layer = (data[at + 1] >> 1) & 3;   // 1 Layer III
            int bitrate = data[at + 2] >> 4, rateIndex = (data[at + 2] >> 2) & 3;
            if (data[at] != 0xFF || (data[at + 1] & 0xE0) != 0xE0 || version == 1 || layer != 1 || bitrate is 0 or 15 || rateIndex == 3)
                break;
            int frameRate = new[] { 44100, 48000, 32000 }[rateIndex] >> (version == 3 ? 0 : version == 2 ? 1 : 2);
            int kbps = (version == 3 ? Mpeg1Layer3Kbps : Mpeg2Layer3Kbps)[bitrate];
            int frameSamples = version == 3 ? 1152 : 576;
            if (rate == 0)
            {
                rate = frameRate;
                channels = (data[at + 3] >> 6) == 3 ? 1 : 2;
            }
            samples += frameSamples;
            at += frameSamples / 8 * kbps * 1000 / frameRate + ((data[at + 2] >> 1) & 1);
        }
        if (rate == 0)
            throw new InvalidDataException("an MP3 without a Layer III frame header");
        return new OggInfo(rate, channels, samples, "mp3");
    }

    /// <summary>Parses the page header at <paramref name="at"/>; false when there is no complete page there. The sizes are the header's and the body's byte counts (the body follows the header).</summary>
    private static bool TryPage(ReadOnlySpan<byte> data, int at, out int headerSize, out int bodySize, out long granule)
    {
        headerSize = bodySize = 0;
        granule = -1;
        if (at < 0 || at + 27 > data.Length || !data.Slice(at, 4).SequenceEqual("OggS"u8) || data[at + 4] != 0)
            return false;
        int segments = data[at + 26];
        headerSize = 27 + segments;
        if (at + headerSize > data.Length)
            return false;
        for (int i = 0; i < segments; i++)
            bodySize += data[at + 27 + i];
        if (at + headerSize + bodySize > data.Length)
            return false;
        granule = BitConverter.ToInt64(data.Slice(at + 6, 8));
        return true;
    }
}
