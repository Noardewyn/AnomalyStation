using System.Text;
using Anomaly.Client.Shared.Audio;

namespace Anomaly.Server.Tests.Client.Audio;

/// <summary>The Ogg Vorbis comments mended before Godot reads a sound: a comment with no '=' gets one, the page's checksum is made again.</summary>
public class VorbisCommentsTests
{
    // one Ogg page: the 27-byte header, a segment table of 255-byte lacings and the body, with its checksum
    private static byte[] Page(byte[] body, int sequence)
    {
        var lacing = new List<byte>();
        int left = body.Length;
        while (left >= 255)
        {
            lacing.Add(255);
            left -= 255;
        }
        lacing.Add((byte)left);
        var page = new List<byte>(Encoding.ASCII.GetBytes("OggS")) { 0, (byte)(sequence == 0 ? 2 : 0) };
        page.AddRange(new byte[8]); // granule position
        page.AddRange(BitConverter.GetBytes(1234)); // stream serial
        page.AddRange(BitConverter.GetBytes(sequence));
        page.AddRange(new byte[4]); // checksum
        page.Add((byte)lacing.Count);
        page.AddRange(lacing);
        page.AddRange(body);
        byte[] bytes = page.ToArray();
        BitConverter.GetBytes(VorbisComments.Crc(bytes)).CopyTo(bytes, 22);
        return bytes;
    }

    private static byte[] CommentHeader(params string[] comments)
    {
        var header = new List<byte> { 3 };
        header.AddRange(Encoding.ASCII.GetBytes("vorbis"));
        byte[] vendor = Encoding.ASCII.GetBytes("Xiph.Org libVorbis I 20020717");
        header.AddRange(BitConverter.GetBytes(vendor.Length));
        header.AddRange(vendor);
        header.AddRange(BitConverter.GetBytes(comments.Length));
        foreach (string comment in comments)
        {
            byte[] c = Encoding.ASCII.GetBytes(comment);
            header.AddRange(BitConverter.GetBytes(c.Length));
            header.AddRange(c);
        }
        header.Add(1); // the framing bit
        return header.ToArray();
    }

    private static byte[] File(byte[] comments) => Page(new byte[30], 0).Concat(Page(comments, 1)).ToArray();

    private static bool ChecksumHolds(byte[] ogg, int start)
    {
        int segments = ogg[start + 26], end = start + 27 + segments;
        for (int i = 0; i < segments; i++)
            end += ogg[start + 27 + i];
        byte[] page = ogg[start..end];
        uint stored = BitConverter.ToUInt32(page, 22);
        page[22] = page[23] = page[24] = page[25] = 0;
        return VorbisComments.Crc(page) == stored;
    }

    [Fact]
    public void TheChecksumIsOggs()
    {
        // CRC-32/CKSUM's check value without its final xor
        Assert.Equal(0x765E7680u ^ 0xFFFFFFFFu, VorbisComments.Crc(Encoding.ASCII.GetBytes("123456789")));
    }

    [Fact]
    public void ACommentWithoutAnEqualsSignGetsOneAndThePageItsChecksum()
    {
        byte[] ogg = File(CommentHeader("ARTIST=tg", "Sony Ogg Vorbis 1.0 Final", "nospace"));
        int second = 27 + 1 + 30;
        Assert.True(VorbisComments.Repair(ogg));
        string text = Encoding.ASCII.GetString(ogg);
        Assert.Contains("Sony=Ogg Vorbis 1.0 Final", text);
        Assert.Contains("nospac=", text);
        Assert.Contains("ARTIST=tg", text);
        Assert.True(ChecksumHolds(ogg, 0));
        Assert.True(ChecksumHolds(ogg, second));
    }

    [Fact]
    public void AGoodFileOrOneCutShortIsLeftAsItWas()
    {
        byte[] good = File(CommentHeader("TITLE=beep"));
        byte[] before = good.ToArray();
        Assert.False(VorbisComments.Repair(good));
        Assert.Equal(before, good);

        byte[] cut = File(CommentHeader("Sony Ogg Vorbis 1.0 Final", "x"));
        cut = cut[..^6]; // the last comment's bytes run past the file's end
        byte[] cutBefore = cut.ToArray();
        Assert.False(VorbisComments.Repair(cut));
        Assert.Equal(cutBefore, cut);
        Assert.False(VorbisComments.Repair(Encoding.ASCII.GetBytes("not an ogg file at all, just text")));
    }
}
