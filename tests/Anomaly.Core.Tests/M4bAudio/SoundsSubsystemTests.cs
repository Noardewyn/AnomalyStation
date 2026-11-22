using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M4bAudio;

/// <summary>
/// SSsounds (controllers/subsystem/sounds.dm): the channel rotation and reservations, the sound keys (get_sfx over
/// GLOB.sfx_datum_by_key, the weighted /assoc keys) and get_sound_length over the host's length table.
/// </summary>
public class SoundsSubsystemTests
{
    [Fact]
    public void RandomChannelsRotateAndWrapBelowTheReservedOnes()
    {
        var room = new AudioRoom(6, 6, seed: 5201);
        room.In(() =>
        {
            Assert.Equal(1f, SSsounds.initialized);
            Assert.Equal(1011f, length(SSsounds.channel_list));
            float first = Dm.num(SSsounds.random_available_channel());
            Assert.Equal(first + 1, Dm.num(SSsounds.random_available_channel()));
            Assert.Equal(first + 2, Dm.num(SSsounds.random_available_channel()));

            // the top channels are reserved downwards from 1011; random ones never reach them
            Assert.Equal(1011f, Dm.num(SSsounds.reserve_sound_channel()));
            Assert.Equal(1010f, Dm.num(SSsounds.reserve_sound_channel()));
            Assert.Equal(1009f, Dm.num(SSsounds.channel_reserve_high));
            SSsounds.channel_random_low = 1009;
            Assert.Equal(1009f, Dm.num(SSsounds.random_available_channel()));
            Assert.Equal(1f, Dm.num(SSsounds.random_available_channel())); // wrapped

            // freeing 1011 swaps it below the reserve mark, where the rotation reaches it
            SSsounds.free_sound_channel(1011);
            Assert.Equal(1010f, Dm.num(SSsounds.channel_reserve_high));
            SSsounds.channel_random_low = 1010;
            Assert.Equal(1011f, Dm.num(SSsounds.random_available_channel()));
            Assert.Equal(1f, Dm.num(SSsounds.random_available_channel()));
            Assert.True(Dm.truthy(SSsounds.reserved_channels["1010"])); // still reserved, now at index 1011
            Assert.Equal(1011f, Dm.num(SSsounds.reserved_channels["1010"]));
            Assert.Equal(961f, Dm.num(SSsounds.available_channels_left())); // tg's count: the 1011 channels less the 50 kept for random use
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ADatumsChannelsAreFreedWhenItIsDeleted()
    {
        var room = new AudioRoom(6, 6, seed: 5202);
        room.In(() =>
        {
            var owner = Dm.@new<datum>();
            float a = Dm.num(SSsounds.reserve_sound_channel_for_datum(owner));
            float b = Dm.num(SSsounds.reserve_sound_channel_for_datum(owner));
            Assert.Equal(1011f, a);
            Assert.Equal(1010f, b);
            Assert.Equal(2f, length(SSsounds.using_channels_by_datum[owner]));
            Assert.Same(owner, SSsounds.using_channels["1011"]);
            qdel(owner);
            Assert.Null(SSsounds.using_channels_by_datum[owner]);
            Assert.Null(SSsounds.using_channels["1011"]);
            Assert.Equal(1011f, Dm.num(SSsounds.channel_reserve_high));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void SoundKeysPickTheirFiles()
    {
        var room = new AudioRoom(6, 6, seed: 5203);
        room.In(() =>
        {
            var shatter = Assert.IsType<datum_sound_effect_shatter>(GLOB.sfx_datum_by_key[SFX_SHATTER]);
            var files = new HashSet<string>();
            for (int i = 0; i < 60; i++)
                files.Add(Dm.str(get_sfx(SFX_SHATTER)));
            Assert.Equal(new[] { "sound/effects/glass/glassbr1.ogg", "sound/effects/glass/glassbr2.ogg", "sound/effects/glass/glassbr3.ogg" }, files.OrderBy(f => f));
            Assert.IsType<DmResource>(get_sfx(SFX_SHATTER));

            // the weighted subtype picks by weight among its files
            Assert.IsType<datum_sound_effect_assoc_plate_armor_rustle>(GLOB.sfx_datum_by_key[SFX_PLATE_ARMOR_RUSTLE]);
            Assert.StartsWith("sound/items/handling/armor_rustle/plate_armor/plate_armor_rustle", Dm.str(get_sfx(SFX_PLATE_ARMOR_RUSTLE)));

            // a file passes through, and so does text that is no key
            DmResource airlock = Dm.resource("sound/machines/airlock/airlock.ogg");
            Assert.Same(airlock, get_sfx(airlock));
            Assert.Equal("no_such_key", get_sfx("no_such_key"));
            // every key tg defines has its datum (the /assoc base has none)
            Assert.Equal(length(subtypesof(typeof(datum_sound_effect))) - 1, length(GLOB.sfx_datum_by_key));
            Assert.Same(shatter, GLOB.sfx_datum_by_key[SFX_SHATTER]);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void SoundLengthsComeFromTheHostsTable()
    {
        var room = new AudioRoom(6, 6, seed: 5204);
        SoundLengths.Install(room.World, new Dictionary<string, float> { ["sound/machines/airlock/airlock.ogg"] = 12.5f });
        room.In(() =>
        {
            Assert.Equal(12.5f, Dm.num(SSsounds.get_sound_length(Dm.resource("sound/machines/airlock/airlock.ogg"))));
            Assert.Equal(12.5f, Dm.num(SSsounds.get_sound_length("sound/machines/airlock/airlock.ogg"))); // cached
            Assert.Equal(12.5f, Dm.num(SSsounds.sound_lengths["sound/machines/airlock/airlock.ogg"]));
            // a file the table lacks: 0, reported once (tg's CRASH after `. = 0`)
            Assert.Equal(0f, Dm.num(SSsounds.get_sound_length("sound/no/such.ogg")));
            Assert.Equal(0f, Dm.num(SSsounds.get_sound_length("sound/no/such.ogg")));
        });
        Assert.Single(room.Room.Runtimes);
        Assert.Contains("rustg_sound_length error", room.Room.Runtimes[0].Message);
    }
}
