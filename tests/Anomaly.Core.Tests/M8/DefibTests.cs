using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M8;

/// <summary>
/// M8 slice E: tg's defibrillator (tools/medical/defib.dm) in a running world, reached by a medic's clicks as tg's: the unit on
/// the back, its paddles taken into the hands and wielded (Z), the click on the patient's chest. Without a cell the paddles only
/// beep; a dead body whose ghost is near is revived after tg's 3 + 2 seconds at tg's HALFWAYCRITDEATH health, its brain decayed
/// while dead, and the ghost is pulled back in; a heart decayed past failing (tg's time limit: about six minutes dead) or a
/// missing heart fails with tg's buzz; a living heart in arrest is restarted; harm mode with the safety off stops a heart; the
/// PENLITE wall mount charges the unit from its area's APC. Plus the dev verbs `heart` and `breath`.
/// </summary>
public class DefibTests
{
    private const int Second = 20;
    private const int ApcX = 3, ApcY = 2;

    private sealed class Feed : IMessageSink
    {
        public List<(object Target, string Html)> Chat { get; } = new();

        void IMessageSink.Chat(object target, DmList message) => Chat.Add((target, Dm.str(message?["html"])));

        public void Output(object target, string text) { }

        public void Balloon(object viewer, object atom, string text) => Chat.Add((viewer, "balloon: " + text));

        public List<string> To(object who) => Chat.Where(c => ReferenceEquals(c.Target, who) || (c.Target is client cl && ReferenceEquals(cl.mob, who))).Select(c => c.Html).ToList();

        public bool Any(string text) => Chat.Any(c => c.Html != null && c.Html.Contains(text, StringComparison.Ordinal));
    }

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Medic, Feed Feed);

    private static Rig Build(ulong seed)
    {
        var r = new AudioRoom(13, 10, seed: seed, mapExtras: w =>
        {
            ((area)GLOB.areas_by_type[typeof(area_misc_testroom)]).requires_power = TRUE;
            for (int x = 2; x <= 5; x++)
                Dm.@new<obj_structure_cable>(locate(x, ApcY, 1));
            Dm.@new<obj_machinery_power_apc_auto_name_directional_south>(locate(ApcX, ApcY, 1));
        });
        r.In(() =>
        {
            for (int x = 2; x <= 11; x++)
            {
                for (int y = 3; y <= 8; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_floor_iron), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));
            }
        });
        mob_living_carbon_human medic = r.Join(5, 5, "Medic");
        var feed = new Feed();
        PresentationSinks.Of(r.World).Messages = feed;
        r.Step(Second);
        return new Rig(r, medic, feed);
    }

    // The unit on the medic's back, its paddles in the hands (the unit's own click, attack_hand), wielded with Z.
    private static obj_item_defibrillator Equip<T>(Rig g, bool wield = true) where T : obj_item_defibrillator
    {
        AudioRoom r = g.R;
        obj_item_defibrillator defib = r.In(() =>
        {
            var d = Dm.@new<T>(g.Medic.loc);
            Waiting.NoSleep(g.Medic.equip_to_slot(d, ITEM_SLOT_BACK));
            Assert.Same(d, g.Medic.back);
            return (obj_item_defibrillator)d;
        });
        r.Command(() => ClientSeam.Click(g.Medic.client, defib, "icon-x=16;icon-y=16;left=1;button=left"));
        r.Step(2);
        r.In(() =>
        {
            Assert.Same(g.Medic, defib.paddles.loc);
            Assert.Equal(TRUE, defib.on);
            Assert.Contains("defibunit-powered", Overlays(defib)); // the unit's lights
            Assert.DoesNotContain("defibunit-paddles", Overlays(defib)); // the paddles are out
        });
        if (wield)
        {
            r.Command(() => ClientSeam.Keybinding(g.Medic.client, 4, true));
            r.Command(() => ClientSeam.Keybinding(g.Medic.client, 4, false));
            r.Step(2);
            r.In(() =>
            {
                Assert.True(HAS_TRAIT(defib.paddles, TRAIT_WIELDED), "Z wields the paddles in both hands");
                Assert.Equal("defibpaddles1", defib.paddles.icon_state);
            });
        }
        r.In(() => g.Medic.zone_selected = BODY_ZONE_CHEST);
        return defib;
    }

    private static List<string> Overlays(atom a) => States(a.managed_overlays);

    private static List<string> States(object overlays)
        => Dm.each(overlays is DmList l ? l : Dm.list(overlays)).Select(o => o switch
        {
            string s => s,
            mutable_appearance m => m.icon_state,
            DmAppearance d => d.icon_state,
            _ => null,
        }).Where(s => s != null).ToList();

    private static void Click(Rig g, atom target, bool right = false)
        => g.R.Command(() => ClientSeam.Click(g.Medic.client, target, right ? "icon-x=16;icon-y=16;right=1;button=right" : "icon-x=16;icon-y=16;left=1;button=left"));

    // A dead player whose ghost flies off (client/Move ghostizes him), as Bob in the gameplay test.
    private static (mob_living_carbon_human Body, client Player) DeadWithGhost(Rig g, int x, int y)
    {
        mob_living_carbon_human bob = g.R.Join(x, y, "Bob");
        client player = bob.client;
        g.R.In(() => Waiting.NoSleep(bob.death()));
        g.R.Command(() => ClientSeam.SetHeldDirections(player, (int)NORTH));
        g.R.Step(2);
        g.R.Command(() => ClientSeam.SetHeldDirections(player, 0));
        g.R.Step();
        Assert.IsType<mob_dead_observer>(player.mob);
        return (bob, player);
    }

    [Fact]
    public void WithoutACellThePaddlesOnlyBeep()
    {
        Rig g = Build(8201);
        mob_living_carbon_human patient = g.R.Dummy(6, 5);
        g.R.In(() => Waiting.NoSleep(patient.death()));
        obj_item_defibrillator defib = g.R.In(() =>
        {
            var d = Dm.@new<obj_item_defibrillator>(g.Medic.loc);
            Waiting.NoSleep(g.Medic.equip_to_slot(d, ITEM_SLOT_BACK));
            Assert.Same(d, g.Medic.back);
            Waiting.NoSleep(d.toggle_paddles(g.Medic));
            Assert.Null(d.cell);
            Assert.Equal(FALSE, d.powered);
            Assert.Contains("defibunit-nocell", Overlays(d));
            g.Medic.zone_selected = BODY_ZONE_CHEST;
            return d;
        });
        g.R.Sounds.Heard.Clear();
        Click(g, patient);
        g.R.Step(6 * Second);
        Assert.True(g.Feed.Any("beeps: Not enough charge!"));
        Assert.Contains(g.R.Sounds.Heard, h => h.File == "sound/machines/defib/defib_failed.ogg");
        Assert.DoesNotContain(g.R.Sounds.Heard, h => h.File == "sound/machines/defib/defib_zap.ogg");
        g.R.In(() =>
        {
            Assert.Equal((float)DEAD, patient.stat);
            Assert.Equal(FALSE, defib.paddles.busy ?? 0);
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ADeadBodyWithItsGhostNearIsRevivedAtTgsHealthWithItsBrainDecayed()
    {
        Rig g = Build(8202);
        (mob_living_carbon_human bob, client player) = DeadWithGhost(g, 6, 5);
        g.R.Step(30 * Second); // dead for 30 s: tg's brain decays (STANDARD_ORGAN_DECAY * 0.5 of 200 a second at body heat)
        float brainBefore = g.R.In(() => Dm.num(((obj_item_organ)bob.get_organ_by_type(typeof(obj_item_organ_brain))).damage));
        Assert.True(brainBefore > 1f, $"the brain decayed while dead: {brainBefore}");
        obj_item_defibrillator defib = Equip<obj_item_defibrillator_loaded>(g);
        float chargeBefore = g.R.In(() => defib.cell.charge);
        g.R.Sounds.Heard.Clear();

        Click(g, bob);
        g.R.Step(3 * Second - 2); // do_help: 3 s to place the paddles ...
        g.R.In(() => Assert.Equal((float)DEAD, bob.stat));
        Assert.DoesNotContain(g.R.Sounds.Heard, h => h.File == "sound/machines/defib/defib_charge.ogg");
        g.R.Step(4);
        Assert.Contains(g.R.Sounds.Heard, h => h.File == "sound/machines/defib/defib_charge.ogg"); // the paddles are placed
        g.R.Step(2 * Second); // ... and 2 s more to shock
        g.R.In(() =>
        {
            Assert.NotEqual((float)DEAD, bob.stat);
            Assert.Same(bob, player.mob); // grab_ghost: the player is back in the body
            Assert.Equal(obj_item_defibrillator.HALFWAYCRITDEATH, bob.health, 1); // tg's oxyloss puts him back halfway into crit
            float brain = Dm.num(((obj_item_organ)bob.get_organ_by_type(typeof(obj_item_organ_brain))).damage);
            Assert.True(brain >= brainBefore, "the brain damage of the time dead stays");
            Assert.Equal(chargeBefore - defib.paddles.revivecost, defib.cell.charge, 1); // on_defib_success: tg's revivecost
            Assert.Equal(TRUE, defib.paddles.cooldown);
            Assert.Equal("defibpaddles1", defib.paddles.icon_state); // tg sets the cooldown after do_success's update_appearance
            Assert.NotNull(bob.has_status_effect(typeof(datum_status_effect_jitter))); // set_jitter_if_lower(200 SECONDS)
        });
        Assert.True(g.Feed.Any("pings: Resuscitation successful."));
        Assert.True(g.Feed.Any("Your heart is being defibrillated!")); // notify_revival to the ghost
        foreach (string file in new[] { "sound/machines/defib/defib_zap.ogg", "sound/machines/defib/defib_success.ogg" })
            Assert.Contains(g.R.Sounds.Heard, h => h.File == file);
        Assert.True(g.R.Sounds.Heard.Any(h => h.File != null && h.File.Contains("bodyfall", StringComparison.Ordinal)), g.R.Sounds.Dump());

        // the unit recharges for cooldown_duration (5 s) and says so
        g.R.Step(5 * Second + 2);
        g.R.In(() => Assert.Equal(FALSE, defib.paddles.cooldown));
        Assert.True(g.Feed.Any("beeps: Unit ready."));
        Assert.Contains(g.R.Sounds.Heard, h => h.File == "sound/machines/defib/defib_ready.ogg");
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AHeartDecayedPastFailingOrMissingFailsWithTgsBuzz()
    {
        Rig g = Build(8203);
        (mob_living_carbon_human bob, client _) = DeadWithGhost(g, 6, 5);
        // tg's time limit: the heart decays at 2.5 * STANDARD_ORGAN_DECAY of its 100 a second, failing ~6 minutes after death
        g.R.Step(370 * Second);
        g.R.In(() =>
        {
            var heart = (obj_item_organ)bob.get_organ_by_type(typeof(obj_item_organ_heart));
            Assert.True(Dm.truthy(Dm.bitand(heart.organ_flags, ORGAN_FAILING)), $"the heart failed: {Dm.num(heart.damage)}");
            Assert.Equal(DEFIB_FAIL_FAILING_HEART, Dm.num(bob.can_defib()));
        });
        // six minutes beside a fresh death bring the medic's sanity down to unstable (mood.dm process, the see-death moodlet, gaps
        // MS1), which slows the defib's two do_afters past this window: the defib, not the medic's mood, is what this checks
        g.R.In(() => Waiting.NoSleep(g.Medic.mob_mood.set_sanity(110)));
        Equip<obj_item_defibrillator_loaded>(g);
        Click(g, bob);
        g.R.Step(5 * Second + 4);
        Assert.True(g.Feed.Any("buzzes: Resuscitation failed - Patient's heart too damaged, replace or repair and try again."));
        g.R.In(() => Assert.Equal((float)DEAD, bob.stat));

        // without a heart at all
        g.R.In(() =>
        {
            var heart = (obj_item_organ)bob.get_organ_by_type(typeof(obj_item_organ_heart));
            Waiting.NoSleep(heart.Remove(bob));
            qdel(heart);
        });
        g.R.Step(6 * Second); // the unit's cooldown
        Click(g, bob);
        g.R.Step(5 * Second + 4);
        Assert.True(g.Feed.Any("buzzes: Resuscitation failed - Patient's heart is missing."));
        g.R.In(() => Assert.Equal((float)DEAD, bob.stat));
        Assert.Contains(g.R.Sounds.Heard, h => h.File == "sound/machines/defib/defib_failed.ogg");
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ALivingHeartInArrestIsRestarted()
    {
        Rig g = Build(8204);
        mob_living_carbon_human patient = g.R.Dummy(6, 5);
        string reply = g.R.In(() => DevCommands.Heart(patient, true)); // the dev verb heart WHO stop
        Assert.Contains("cardiac arrest yes", reply);
        g.R.Step(2 * Second);
        g.R.In(() => Assert.True(Dm.truthy(patient.undergoing_cardiac_arrest())));
        Equip<obj_item_defibrillator_loaded>(g);
        g.R.Sounds.Heard.Clear();
        Click(g, patient);
        g.R.Step(5 * Second + 4);
        g.R.In(() => Assert.False(Dm.truthy(patient.undergoing_cardiac_arrest())));
        Assert.True(g.Feed.Any("pings: Patient's heart is now beating again."));
        Assert.Contains(g.R.Sounds.Heard, h => h.File == "sound/machines/defib/defib_zap.ogg");

        // and the dev verbs' other halves
        Assert.Contains("cardiac arrest no", g.R.In(() => DevCommands.Heart(patient, false)));
        Assert.Contains("losebreath 20", g.R.In(() => DevCommands.Breath(patient, 20)));
        g.R.In(() => Assert.Equal(20f, patient.losebreath));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void HarmModeWithTheSafetyOffStopsAHeart()
    {
        Rig g = Build(8205);
        mob_living_carbon_human patient = g.R.Dummy(6, 5);
        obj_item_defibrillator defib = Equip<obj_item_defibrillator_loaded>(g);

        // with the safety on, harm mode does nothing
        g.R.In(() => g.Medic.set_combat_mode(TRUE));
        Click(g, patient);
        g.R.Step(4 * Second);
        g.R.In(() => Assert.False(Dm.truthy(patient.undergoing_cardiac_arrest())));

        // an emag takes the safety off
        g.R.In(() =>
        {
            Waiting.NoSleep(defib.emag_act(g.Medic));
            Assert.Equal(FALSE, defib.safety);
        });
        g.R.Sounds.Heard.Clear();
        Click(g, patient);
        g.R.Step(3 * Second + 4); // do_harm: 1.5 s + 1.5 s
        g.R.In(() =>
        {
            Assert.True(Dm.truthy(patient.undergoing_cardiac_arrest()));
            Assert.Equal(50f, Dm.num(((obj_item_bodypart)patient.get_bodypart(BODY_ZONE_CHEST)).burn_dam), 1);
            Assert.True(Dm.truthy(patient.IsParalyzed()));
            Assert.Contains("defibunit-emagged", Overlays(defib)); // tg's emagged light, drawn at the unit's next update_power
        });
        foreach (string file in new[] { "sound/machines/defib/defib_charge.ogg", "sound/machines/defib/defib_zap.ogg", "sound/items/weapons/egloves.ogg" })
            Assert.Contains(g.R.Sounds.Heard, h => h.File == file);
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ThePenliteMountChargesItsUnitFromTheAreasPower()
    {
        Rig g = Build(8206);
        obj_machinery_defibrillator_mount_charging mount = g.R.In(() => Dm.@new<obj_machinery_defibrillator_mount_charging>(locate(6, 5, 1)));
        obj_item_defibrillator defib = g.R.In(() =>
        {
            var d = Dm.@new<obj_item_defibrillator_loaded>(g.Medic.loc);
            d.cell.charge = 0;
            d.update_power();
            Assert.Equal(FALSE, d.powered);
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Medic.put_in_active_hand(d))));
            return d;
        });
        Click(g, mount); // the unit hooked up
        g.R.Step(2);
        g.R.In(() =>
        {
            Assert.Same(mount, defib.loc);
            Assert.Same(defib, mount.defib);
            Assert.Contains("defib", Overlays(mount));
        });
        g.R.Step(4 * Second);
        g.R.In(() =>
        {
            Assert.True(defib.cell.charge > 0, $"the mount charged the cell: {defib.cell.charge}");
            Assert.Equal(TRUE, defib.powered);
            object top = Dm.each(mount.managed_overlays is DmList l ? l : Dm.list(mount.managed_overlays))
                .Single(o => (o as DmAppearance)?.icon_state == "defib" || (o as mutable_appearance)?.icon_state == "defib");
            List<string> nested = States(top is DmAppearance d ? d.overlays : ((mutable_appearance)top).overlays);
            Assert.Contains("online", nested); // the safety light
            Assert.Contains(nested, s => s != null && s.StartsWith("charge", StringComparison.Ordinal));
        });

        // the paddles come off the wall with an empty hand and snap back when the medic walks away
        Click(g, mount);
        g.R.Step(2);
        g.R.In(() => Assert.Same(g.Medic, defib.paddles.loc));
        g.R.In(() => Waiting.NoSleep(g.Medic.forceMove(locate(9, 5, 1))));
        g.R.Step(2);
        g.R.In(() =>
        {
            Assert.Same(defib, defib.paddles.loc);
            Assert.Equal(FALSE, defib.on);
        });
        Assert.True(g.Feed.Any("paddles overextend and come out of your hands!"));
        g.R.AssertNoRuntimes();
    }
}
