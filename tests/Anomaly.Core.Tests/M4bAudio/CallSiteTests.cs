using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M4bAudio;

/// <summary>
/// The call sites restored in M4b audio slice 6, one per family: an item's pickup and drop sounds (items.dm:672 sound_chain),
/// the toolbox's storage opening (storage.dm:1006), the welder lighting and going out (weldingtool.dm:256, :266), a slip
/// (_open.dm:575), the canister valve (canister.dm:694), a locker opening and closing (closets.dm:546, :618), the space wind
/// pull (living.dm:1112) and the heartbeat channel stopped on a full heal and on a human's death (living.dm:1013,
/// human/death.dm:20). Each is heard through tg's playsound by a player in the toy room's air (one atmosphere, so the
/// pressure factor is 1 and a sound on the listener's own tile keeps its volume).
/// </summary>
public class CallSiteTests
{
    private const int Second = 20;

    private static (AudioRoom Room, mob_living_carbon_human Me) Room(ulong seed)
    {
        var room = new AudioRoom(12, 8, seed: seed);
        mob_living_carbon_human me = room.Join(5, 4, "Listener");
        room.Step(Second);
        room.Sounds.Heard.Clear();
        return (room, me);
    }

    private static T Hold<T>(AudioRoom room, mob_living_carbon_human who) where T : obj_item => room.In(() =>
    {
        T item = Dm.@new<T>(get_turf(who));
        Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(item))), "the item reaches a hand");
        return item;
    });

    [Fact]
    public void PickingUpAndDroppingAToolboxPlaysItsHandlingSounds()
    {
        var (room, me) = Room(6601);
        obj_item_storage_toolbox_mechanical box = Hold<obj_item_storage_toolbox_mechanical>(room, me);
        HeardSound pickup = Assert.Single(room.Sounds.Of(me, "sound/items/handling/toolbox/toolbox_pickup.ogg"));
        Assert.Equal(PICKUP_SOUND_VOLUME, pickup.Volume, 3); // on the listener's own tile: no falloff

        room.Sounds.Heard.Clear();
        room.In(() => Waiting.NoSleep(me.dropItemToGround(box)));
        HeardSound drop = Assert.Single(room.Sounds.Of(me, "sound/items/handling/toolbox/toolbox_drop.ogg"));
        Assert.Equal(DROP_SOUND_VOLUME, drop.Volume, 3);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void OpeningAToolboxPlaysItsOpenSound()
    {
        var (room, me) = Room(6602);
        obj_item_storage_toolbox_mechanical box = Hold<obj_item_storage_toolbox_mechanical>(room, me);
        room.Sounds.Heard.Clear();
        Assert.True(Dm.truthy(room.In(() => Waiting.NoSleep(box.atom_storage.open_storage(me)))), "the toolbox opened");
        HeardSound open = Assert.Single(room.Sounds.Of(me, "sound/items/handling/toolbox/toolbox_open.ogg"));
        Assert.Equal(50, open.Volume, 3);
        Assert.InRange(open.Frequency, 32000, 55000); // open_sound_vary
        room.AssertNoRuntimes();
    }

    [Fact]
    public void LightingAWelderAndPuttingItOutPlayTheActivationSounds()
    {
        var (room, me) = Room(6603);
        obj_item_weldingtool welder = Hold<obj_item_weldingtool>(room, me);
        room.Sounds.Heard.Clear();
        room.In(() => Waiting.NoSleep(welder.attack_self(me)));
        Assert.True(room.In(() => Dm.truthy(welder.welding)), "the welder is lit");
        HeardSound on = Assert.Single(room.Sounds.Of(me, "sound/items/tools/welderactivate.ogg"));
        Assert.Equal(50, on.Volume, 3);

        room.In(() => Waiting.NoSleep(welder.attack_self(me)));
        Assert.False(room.In(() => Dm.truthy(welder.welding)), "the welder is out");
        Assert.Single(room.Sounds.Of(me, "sound/items/tools/welderdeactivate.ogg"));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ASlipPlaysTheSlipSound()
    {
        var (room, me) = Room(6604);
        var floor = room.In(() => (turf_open)get_turf(me));
        room.In(() => Waiting.NoSleep(floor.handle_slip(me, 2 * SECONDS, null, NONE)));
        HeardSound slip = Assert.Single(room.Sounds.Of(me, "sound/misc/slip.ogg"));
        Assert.Equal(50, slip.Volume, 3);
        Assert.Equal(LYING_DOWN, room.In(() => me.body_position));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TurningACanisterValvePlaysTheValveSound()
    {
        var (room, me) = Room(6605);
        var can = room.In(() => Dm.@new<obj_machinery_portable_atmospherics_canister_air>(locate(8, 4, 1)));
        room.Step(Second);
        room.Sounds.Heard.Clear();
        room.In(() => can.toggle_valve(null));
        HeardSound valve = Assert.Single(room.Sounds.Of(me, "sound/effects/valve_opening.ogg"));
        double expected = 50 - Math.Pow(3.0 / 15, 1 / 2.5) * 45; // vol 50, 3 tiles of 15, exponent 2.5, min_volume 5
        Assert.Equal(expected, valve.Volume, 3);
        Assert.Equal(3, valve.X);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ALockerPlaysItsOpenAndCloseSounds()
    {
        var (room, me) = Room(6606);
        var closet = room.In(() => Dm.@new<obj_structure_closet>(locate(5, 5, 1)));
        room.Step(Second);
        room.Sounds.Heard.Clear();
        Assert.True(Dm.truthy(room.In(() => Waiting.NoSleep(closet.open(me)))), "the locker opened");
        HeardSound open = Assert.Single(room.Sounds.Of(me, "sound/machines/closet/closet_open.ogg"));
        double atOne = Math.Pow(1.0 / 12, 1 / 2.5); // extrarange -3: maxdistance 12
        Assert.Equal(35 - atOne * 30, open.Volume, 3); // open_sound_volume 35

        room.Sounds.Heard.Clear();
        Assert.True(Dm.truthy(room.In(() => Waiting.NoSleep(closet.close(me)))), "the locker closed");
        HeardSound close = Assert.Single(room.Sounds.Of(me, "sound/machines/closet/closet_close.ogg"));
        Assert.Equal(50 - atOne * 45, close.Volume, 3); // close_sound_volume 50
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheSpaceWindPullPlaysTheWindSound()
    {
        var (room, me) = Room(6607);
        room.In(() => me.experience_pressure_difference(80, NORTH));
        HeardSound wind = Assert.Single(room.Sounds.Of(me, "sound/effects/space_wind.ogg"));
        Assert.Equal(50, wind.Volume, 3);
        Assert.InRange(wind.Frequency, 32000, 55000);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AFullHealAndADeathStopTheHeartbeatChannel()
    {
        var (room, me) = Room(6608);
        room.In(() => Waiting.NoSleep(me.fully_heal(HEAL_ALL)));
        HeardSound stop = Assert.Single(room.Sounds.Heard, h => ReferenceEquals(h.Target, me) && h.File == null && h.Channel == CHANNEL_HEARTBEAT);
        Assert.Equal(100, stop.Volume, 3); // BYOND's /sound default: a stop is a fileless sound on the channel

        room.Sounds.Heard.Clear();
        room.In(() => Waiting.NoSleep(me.death()));
        Assert.Equal(DEAD, room.In(() => me.stat));
        // human/death.dm:20, then the heart's Stop (_heart.dm:82) on the same channel
        Assert.True(room.Sounds.Heard.Count(h => ReferenceEquals(h.Target, me) && h.File == null && h.Channel == CHANNEL_HEARTBEAT) >= 1, room.Sounds.Dump());
        room.AssertNoRuntimes();
    }
}
