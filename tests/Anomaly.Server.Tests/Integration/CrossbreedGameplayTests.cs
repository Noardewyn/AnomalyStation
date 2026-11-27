using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Xenobiology's crossbreeds through the real server with headless clients: a joined scientist crushes a regenerative extract
/// over a wounded crewmate with a click (regenerative.dm:18), who is healed in full and sees the milky goo's message; then she
/// carries a stabilized bluespace extract, whose effect takes hold and puts its alert on her HUD, and leaves when she drops it
/// (stabilized.dm:42, _status_effects.dm:463).
/// </summary>
public class CrossbreedGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static object Obj(RealHeadlessWorld w, long id) => In(w, () => w.World.Objects.TryGet(id, out object o) ? o : null);

    private static ClickTarget On(atom a) => new(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static void Hold(mob_living_carbon_human who, obj_item item)
    {
        foreach (obj_item held in Dm.each<obj_item>(who.held_items).ToList())
            Waiting.NoSleep(who.dropItemToGround(held));
        Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_active_hand(item))));
    }

    [Fact]
    public void A_scientist_heals_a_wounded_crewmate_with_a_regenerative_extract_and_carries_a_stabilized_one()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 13990, TimeoutSteps = 6000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var scientist = (mob_living_carbon_human)w.MobOf(alice);
        var patient = (mob_living_carbon_human)w.MobOf(bob);
        var bobHeard = new List<string>();
        bob.Client.ServerTextReceived += t => bobHeard.Add(t.Html ?? "");
        w.Run(Second);

        // Bob stands beside her, badly hurt; she holds a regenerative grey extract and clicks him
        var core = In(w, () =>
        {
            turf beside = new[] { EAST, WEST, NORTH, SOUTH }.Select(d => get_step(scientist, d) as turf)
                .First(t => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density)));
            Waiting.NoSleep(patient.forceMove(beside));
            Waiting.NoSleep(patient.adjust_brute_loss(60));
            Waiting.NoSleep(patient.adjust_fire_loss(20));
            var made = Dm.@new<obj_item_slimecross_regenerative_grey>(scientist.loc);
            Hold(scientist, made);
            return made;
        });
        w.Run(Second / 2);
        Assert.True(In(w, () => Dm.num(patient.get_brute_loss()) + Dm.num(patient.get_fire_loss())) >= 80);
        Assert.True(alice.Session.Click(On(patient), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.True(In(w, () => QDELETED(core)), "the extract was crushed");
        Assert.Equal(0, In(w, () => Dm.num(patient.get_brute_loss()) + Dm.num(patient.get_fire_loss())));
        Assert.Contains(bobHeard, h => h.Contains("the milky goo quickly regenerating all of", StringComparison.Ordinal));

        // she carries a stabilized bluespace extract: its effect holds while she has it, with its alert on her HUD
        var stabilized = In(w, () =>
        {
            var made = Dm.@new<obj_item_slimecross_stabilized_bluespace>(scientist.loc);
            Hold(scientist, made);
            return made;
        });
        w.Run(2 * Second);
        Assert.True(In(w, () => Dm.truthy(scientist.has_status_effect(typeof(datum_status_effect_stabilized_bluespace)))));
        long alertId = alice.Session.Replica.Screen.Values.Select(e => e.Id).FirstOrDefault(id => Obj(w, id) is atom_movable_screen_alert_status_effect_bluespaceslime);
        Assert.True(alertId != 0, "no stabilized bluespace alert on her HUD");
        Assert.Equal("slime_bluespace_on", In(w, () => Dm.str(((atom)Obj(w, alertId)).icon_state)));

        // dropped, it fades
        Do(w, () => Waiting.NoSleep(scientist.dropItemToGround(stabilized)));
        w.Run(2 * Second);
        Assert.False(In(w, () => Dm.truthy(scientist.has_status_effect(typeof(datum_status_effect_stabilized_bluespace)))));
        output.WriteLine("server errors:\n" + string.Join("\n", w.Err.ToString().Split('\n').TakeLast(20)));
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }
}
