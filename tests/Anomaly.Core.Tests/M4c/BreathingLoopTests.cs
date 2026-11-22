using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M4c;

/// <summary>
/// M4c slice 8, D146: tg's breathing loop on internals (carbon/life.dm:117-120). After a successful breath from internals a mob
/// with a client that is not deaf starts its `breathing_loop` (looping_sounds/breathing.dm: one of the eight
/// internals_breathing sounds every 7 s, sent to the mob only); the `sound_breathing` preference is read as its default, on
/// (DV41). A mob without a client never starts it, and closing the valve stops it (carbon/inventory.dm close_internals).
/// </summary>
public class BreathingLoopTests
{
    private const int Second = 20;
    private const string Breathing = "sound/mobs/humanoids/breathing/internals_breathing";

    private static void Internals(AudioRoom r, mob_living_carbon_human who, out obj_item_tank_internals_oxygen tank)
    {
        obj_item_tank_internals_oxygen t = null;
        r.In(() =>
        {
            var mask = Dm.@new<obj_item_clothing_mask_breath>(who.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(who.equip_to_slot_or_del(mask, ITEM_SLOT_MASK))));
            t = Dm.@new<obj_item_tank_internals_oxygen>(who.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(t))));
            Waiting.NoSleep(who.toggle_internals(t));
            Assert.Same(t, who.@internal);
        });
        tank = t;
    }

    [Fact]
    public void A_player_on_internals_hears_the_breathing_loop_and_it_stops_with_the_valve()
    {
        var r = new AudioRoom(9, 9, seed: 8102);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        mob_living_carbon_human dummy = r.Dummy(6, 6);
        r.Step(Second);
        Internals(r, bob, out obj_item_tank_internals_oxygen tank);
        Internals(r, dummy, out _);
        r.Sounds.Heard.Clear();
        r.Step(20 * Second);

        List<HeardSound> breaths = r.Sounds.Heard.Where(h => h.File.StartsWith(Breathing, StringComparison.Ordinal)).ToList();
        Assert.True(breaths.Count >= 2, "the loop plays every 7 s: " + r.Sounds.Dump());
        Assert.All(breaths, h => Assert.Same(bob, h.Target)); // _direct: to the breather only; the dummy has no client
        Assert.True(r.In(() => Dm.truthy(bob.breathing_loop.is_active())));
        Assert.False(r.In(() => Dm.truthy(dummy.breathing_loop.is_active())));

        // The valve closed: close_internals stops the loop (carbon/inventory.dm:275).
        r.In(() => Waiting.NoSleep(bob.toggle_internals(tank)));
        Assert.Null(r.In(() => bob.@internal));
        Assert.False(r.In(() => Dm.truthy(bob.breathing_loop.is_active())));
        r.Sounds.Heard.Clear();
        r.Step(10 * Second);
        Assert.DoesNotContain(r.Sounds.Heard, h => h.File.StartsWith(Breathing, StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }
}
