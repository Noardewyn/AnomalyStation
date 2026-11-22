using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.Support;

/// <summary>Determinism (translation-rules.md 15): the same support scenario twice gives the same world state hash.</summary>
public class SupportDeterminismTests
{
    // Every proc of the package once: modifiers, mob lists, string lists, connect_loc and a movement detector moved about.
    private static ulong Scenario(bool extraModifier)
    {
        using var w = new SupportWorld(seed: 9);
        turf a = locate(1, 1, 1), b = locate(2, 1, 1);
        var mob = Dm.@new<mob_test_support_mob>(a);
        mob.add_to_mob_list();
        mob.add_to_alive_mob_list();
        Waiting.NoSleep(mob.initialize_actionspeed());
        Waiting.NoSleep(mob.update_config_movespeed());
        Waiting.NoSleep(mob.add_movespeed_modifier(typeof(datum_movespeed_modifier_test_support_slow)));
        if (extraModifier)
            Waiting.NoSleep(mob.add_movespeed_modifier(typeof(datum_movespeed_modifier_test_support_fast)));
        string_list(Dm.list("neutral"));

        var box = Dm.@new<obj_test_support_box>(a);
        var listener = Dm.@new<obj_test_support_listener>(box);
        Waiting.NoSleep(listener.AddElement(typeof(datum_element_connect_loc), Dm.list(Dm.kv("test_support_mock", nameof(obj_test_support_listener.on_mock)))));
        var recorder = Dm.@new<datum_test_support_recorder>();
        Dm.@new<datum_movement_detector>(listener, Dm.@new<datum_callback>(recorder, nameof(datum_test_support_recorder.on_tracked_moved)));
        SupportMoves.Move(box, b, EAST);
        SupportMoves.ForceMove(listener, b);
        Assert.Empty(w.Runtimes);
        return w.World.ComputeStateHash();
    }

    [Fact]
    public void TheSameScenarioTwiceGivesTheSameStateHash()
    {
        Assert.Equal(Scenario(false), Scenario(false));
        Assert.NotEqual(Scenario(false), Scenario(true)); // the hash sees the modifier state
    }
}
