using Anomaly.Client.Shared.Icons;

namespace Anomaly.Server.Tests.Client.Icons;

/// <summary>The join's sheets decoded ahead: a sheet's decoded size from its PNG header, and no giant but the combat list's.</summary>
public class IconPrefetchTests
{
    private static byte[] Png(uint width, uint height)
    {
        var png = new byte[33];
        new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 13, 10, 26, 10 }.CopyTo(png, 0);
        png[16] = (byte)(width >> 24);
        png[17] = (byte)(width >> 16);
        png[18] = (byte)(width >> 8);
        png[19] = (byte)width;
        png[20] = (byte)(height >> 24);
        png[21] = (byte)(height >> 16);
        png[22] = (byte)(height >> 8);
        png[23] = (byte)height;
        return png;
    }

    [Fact]
    public void ASheetIsDecodedAheadUnlessItIsAGiant()
    {
        Assert.Equal(4800L * 4320 * 4, IconPrefetch.DecodedBytes(Png(4800, 4320)));
        Assert.Equal(-1, IconPrefetch.DecodedBytes(new byte[10]));
        Assert.True(IconPrefetch.Ahead("icons/obj/doors/airlock.dmi", Png(1024, 1024)));
        Assert.False(IconPrefetch.Ahead("icons/effects/station_explosion.dmi", Png(6144, 6144)));
        Assert.True(IconPrefetch.Ahead("icons/hud/screen_full.dmi", Png(4800, 4320))); // the combat list's, prefetched whatever its size
        Assert.False(IconPrefetch.Ahead("icons/obj/missing.dmi", null));
    }
}
