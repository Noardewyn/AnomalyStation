using System.IO;
using System.Text;

namespace Anomaly.Core.Tests.Tools.Sounds;

/// <summary>Builds tiny Ogg Vorbis-shaped files for the tests: a first page with the identification header, a last page with a granule position.</summary>
internal static class OggBuilder
{
    /// <summary>An Ogg stream of two pages: the Vorbis identification packet (page 0, granule 0) and one data page whose granule position is <paramref name="frames"/>.</summary>
    public static byte[] Make(int sampleRate, int channels, long frames, int dataBytes = 40, long lastGranule = long.MinValue)
    {
        var ms = new MemoryStream();
        var id = new MemoryStream();
        id.WriteByte(1);
        id.Write(Encoding.ASCII.GetBytes("vorbis"));
        id.Write(BitConverter.GetBytes(0u));            // version
        id.WriteByte((byte)channels);
        id.Write(BitConverter.GetBytes((uint)sampleRate));
        id.Write(BitConverter.GetBytes(0)); id.Write(BitConverter.GetBytes(64000)); id.Write(BitConverter.GetBytes(0));   // bitrates
        id.WriteByte(0xB8);                            // blocksizes
        id.WriteByte(1);                               // framing
        Page(ms, 2, 0, id.ToArray());
        Page(ms, 4, lastGranule == long.MinValue ? frames : lastGranule, new byte[dataBytes]);
        return ms.ToArray();
    }

    /// <summary>One Ogg page: header, a lacing table for <paramref name="body"/> (at most 255 bytes here), the body. The CRC is left zero (the reader does not check it).</summary>
    public static void Page(Stream to, byte headerType, long granule, byte[] body)
    {
        to.Write(Encoding.ASCII.GetBytes("OggS"));
        to.WriteByte(0);
        to.WriteByte(headerType);
        to.Write(BitConverter.GetBytes(granule));
        to.Write(BitConverter.GetBytes(1u));            // serial
        to.Write(BitConverter.GetBytes(0u));            // page sequence
        to.Write(BitConverter.GetBytes(0u));            // CRC
        to.WriteByte(1);                                // one segment
        to.WriteByte((byte)body.Length);
        to.Write(body);
    }

    /// <summary>A RIFF/WAVE file of 16-bit PCM silence.</summary>
    public static byte[] Wave(int sampleRate, int channels, int frames)
    {
        var ms = new MemoryStream();
        int block = channels * 2, dataSize = frames * block;
        ms.Write(Encoding.ASCII.GetBytes("RIFF")); ms.Write(BitConverter.GetBytes(36 + dataSize)); ms.Write(Encoding.ASCII.GetBytes("WAVE"));
        ms.Write(Encoding.ASCII.GetBytes("fmt ")); ms.Write(BitConverter.GetBytes(16));
        ms.Write(BitConverter.GetBytes((short)1)); ms.Write(BitConverter.GetBytes((short)channels));
        ms.Write(BitConverter.GetBytes(sampleRate)); ms.Write(BitConverter.GetBytes(sampleRate * block));
        ms.Write(BitConverter.GetBytes((short)block)); ms.Write(BitConverter.GetBytes((short)16));
        ms.Write(Encoding.ASCII.GetBytes("data")); ms.Write(BitConverter.GetBytes(dataSize));
        ms.Write(new byte[dataSize]);
        return ms.ToArray();
    }
}
