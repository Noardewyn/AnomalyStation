using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Gaps K1 (D327): the changeling through the real server on MetaStation. Bob, made a changeling as the roundstart ruleset makes one,
/// clicks his Cellular Emporium button, the CellularEmporium window opens with his ten genetic points and he evolves the mute sting
/// from it; he clicks the sting's button to prepare it and middle-clicks Alice beside him, who is silenced; then he takes Alice by the
/// neck and clicks Absorb DNA: after its three stages Alice is dead and husked, Bob holds her DNA profile and her player is in his
/// hivemind.
/// </summary>
public class ChangelingGameplayTests
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f) { using (w.World.Enter()) return f(); }

    private static ClickTarget Screen(long id) => new(TargetKind.Screen, id, 0, 0, 0, 16, 16);

    private static UiWindowState Window(RealHeadlessWorld.Player p, string @interface) => p.Session.Replica.UiWindows.Values.SingleOrDefault(v => v.Interface == @interface);

    private static long ButtonOf(RealHeadlessWorld w, mob_living_carbon_human me, datum_action action)
        => In(w, () => ((datum)action.viewers[me.hud_used]).dm_id);

    [MetaServerFact]
    public void On_MetaStation_a_changeling_evolves_a_sting_silences_alice_and_absorbs_her()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 871, TimeoutSteps = 60000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player bob = w.Join("Bob");
        RealHeadlessWorld.Player alice = w.Join("Alice");
        w.Run(4 * Second);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        int bootRuntimes = w.World.RuntimeErrorCount;

        // Alice beside Bob on a free floor; Bob a changeling by the roundstart ruleset's call
        datum_antagonist_changeling ling = In(w, () =>
        {
            turf next = Dm.each<object>(GLOB.cardinals).Select(d => get_step(bobMob, Dm.num(d))).OfType<turf_open>()
                .First(t => !Dm.truthy(Waiting.NoSleep(t.is_blocked_turf(exclude_mobs: Dm.TRUE))));
            Waiting.NoSleep(aliceMob.forceMove(next));
            var ruleset = Dm.@new<datum_dynamic_ruleset_roundstart_changeling>(SSdynamic.get_config());
            Waiting.NoSleep(ruleset.assign_role(bobMob.mind));
            return IS_CHANGELING(bobMob) as datum_antagonist_changeling;
        });
        Assert.NotNull(ling);
        w.Run(Second);
        Assert.NotNull(Window(bob, "AntagInfoChangeling"));

        // the emporium's button opens the window with ten points; evolving the mute sting spends two
        Assert.True(bob.Session.Click(Screen(ButtonOf(w, bobMob, ling.emporium_action)), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState emporium = Window(bob, "CellularEmporium");
        Assert.True(emporium != null, "no emporium: " + string.Join(", ", bob.Session.Replica.UiWindows.Values.Select(v => v.Interface)));
        Assert.Equal(10, emporium.Merged.Num("genetic_points_count"));
        Assert.True(bob.Session.UiAct(emporium.Window, "evolve", UiValue.ObjectOf(("path", UiValue.Of("/datum/action/changeling/sting/mute")))));
        w.Run(Second);
        Assert.Equal(8, Window(bob, "CellularEmporium").Merged.Num("genetic_points_count"));
        var mute = In(w, () => ling.purchased_powers[typeof(datum_action_changeling_sting_mute)] as datum_action_changeling_sting_mute);
        Assert.NotNull(mute);

        // the sting's button prepares it; a middle click on Alice stings her silent
        Assert.True(bob.Session.Click(Screen(ButtonOf(w, bobMob, mute)), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        Assert.Same(mute, In(w, () => ling.chosen_sting));
        In(w, () => { ling.chem_charges = 20; return 0; }); // the chemicals regenerated since the start: exactly the sting's cost
        Assert.True(bob.Session.Click(aliceMob.dm_id, ClickButton.Middle, ClickModifiers.None));
        w.Run(Second);
        Assert.True(In(w, () => aliceMob.has_status_effect(typeof(datum_status_effect_silenced)) != null), "Alice is not silenced");
        Assert.True(In(w, () => ling.chem_charges) < 5, "the sting cost no chemicals"); // 20 for the sting; a Life tick may regenerate a little
        In(w, () => { ling.chem_charges = 75; return 0; });

        // the neck grab and Absorb DNA: three stages of fifteen seconds
        In(w, () =>
        {
            Waiting.NoSleep(bobMob.start_pulling(aliceMob));
            Waiting.NoSleep(bobMob.setGrabState(GRAB_KILL));
            return 0;
        });
        var absorb = In(w, () => Dm.each<datum_action_changeling_absorb_dna>(ling.innate_powers).Single());
        Assert.True(bob.Session.Click(Screen(ButtonOf(w, bobMob, absorb)), ClickButton.Left, ClickModifiers.None));
        w.Run(48 * Second);
        Assert.Equal(DEAD, In(w, () => aliceMob.stat));
        Assert.True(In(w, () => HAS_TRAIT(aliceMob, TRAIT_HUSK)), "Alice is not husked");
        Assert.Equal(2, In(w, () => ling.stored_profiles.len));
        Assert.Equal(1f, In(w, () => ling.true_absorbs));
        Assert.Contains(In(w, () => Dm.each<object>(bobMob.imaginary_group).ToList()), m => m is mob_eye_imaginary_friend_hivemind);
        Assert.True(bootRuntimes == w.World.RuntimeErrorCount, w.Err.ToString());
    }
}
