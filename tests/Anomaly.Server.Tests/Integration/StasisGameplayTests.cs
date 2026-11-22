using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M8y slice C: the stasis bed on MetaStation through the real server (D217). A dying patient (a severe cut on his chest, oxygen damage
/// and epinephrine in his blood, by dev commands) is dragged by a medic onto the Treatment Center's bed (79,105): buckled to it he is in
/// tg's stasis (its traits and the Stasis alert on his screen), and for a minute his health, his blood and the reagent stay as they were;
/// a watcher sees the mattress fade in (the bed's unique vis overlay, alpha 0 to 255 over 5 s, CUBIC) and the ripple on the patient, and
/// hears the spray. The medic's alt-click turns the bed off (the click, synth_no at tg's random frequency): he is thawed and bleeds again;
/// on again, the room's APC breaker off stops it as well; dead on the bed, his time of death moves with the clock (the defib's limit
/// waits); stood up from the bed, he is thawed.
/// </summary>
public class StasisGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const int BedX = 79, BedY = 105;
    private const string Reagent = "/datum/reagent/medicine/epinephrine";

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Put(RealHeadlessWorld w, mob m, int x, int y)
    {
        using (w.World.Enter())
            Waiting.NoSleep(m.forceMove(locate(x, y, m.z)));
        w.Run(Second);
    }

    private static ClickTarget Entity(long id) => new(TargetKind.Entity, id, 0, 0, 0, 16, 16);

    [MetaServerFact]
    public void A_dying_patient_on_the_stasis_bed_stays_as_he_was_until_the_bed_or_its_power_stops_or_he_is_taken_off()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 63, TimeoutSteps = 12000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player medic = w.Join("Medic");
        RealHeadlessWorld.Player patient = w.Join("Bob");
        RealHeadlessWorld.Player watcher = w.Join("Watcher");
        var me = (mob_living_carbon_human)w.MobOf(medic);
        var him = (mob_living_carbon_human)w.MobOf(patient);
        var eye = (mob_living_carbon_human)w.MobOf(watcher);
        int z = In(w, () => (int)me.z);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        var told = new List<string>();
        patient.Client.ServerTextReceived += t => told.Add(t.Html);
        var heard = new List<SoundEvent>();
        watcher.Session.Replica.SoundsReceived += events => heard.AddRange(events);
        ReplicaWorld seen = watcher.Session.Replica;
        w.Run(Second);

        var bed = In(w, () => Dm.each_cast<atom>(((turf)locate(BedX, BedY, z)).contents).OfType<obj_machinery_stasis>().Single());
        Assert.True(In(w, () => Dm.truthy(bed.stasis_running())), "the bed is on and powered at the round's start");
        Put(w, eye, BedX + 2, BedY - 1);
        Put(w, me, BedX + 1, BedY);
        Put(w, him, BedX + 1, BedY - 1);

        // the bed as tg draws it: its picture, and the mattress (a unique vis overlay) drawn on it at full alpha while it runs
        ReplicaEntity Mattress() => seen.Entities.SingleOrDefault(e => e.IsVisual && e.VisHost == bed.dm_id && e.TypePath == "/obj/effect/overlay/vis");
        AppearanceEntry Look(ReplicaEntity e) => seen.TryGetAppearance(e.State.Appearance, out AppearanceEntry a) ? a : null;
        ReplicaEntity bedSeen = seen.Entities.Single(e => e.Id == bed.dm_id);
        Assert.Equal("stasis", seen.ResolveString(Look(bedSeen).IconState));
        Assert.DoesNotContain(Look(bedSeen).Overlays, o => seen.TryGetAppearance(o, out AppearanceEntry a) && seen.ResolveString(a.IconState) == "stasis_on");
        ReplicaEntity mattress = Mattress();
        Assert.NotNull(mattress);
        Assert.Equal("stasis_on", seen.ResolveString(Look(mattress).IconState));
        Assert.Equal(255, Look(mattress).Alpha);

        // ---- 1. The emergency, by dev commands (as D193): a severe cut, oxygen damage, a reagent in his blood.
        foreach (string line in new[] { "wound Bob chest slash severe", "damage Bob oxy 60", $"reagent Bob {Reagent} 15" })
        {
            medic.Session.DevCommand(line);
            w.Run(1);
        }
        w.Run(Second);
        float Blood() => In(w, () => Dm.num(him.blood_volume));
        float Health() => In(w, () => Dm.num(him.health));
        float Oxy() => In(w, () => Dm.num(him.get_oxy_loss()));
        float Dose() => In(w, () => Dm.num(((datum_reagents)him.reagents).get_reagent_amount(typeof(datum_reagent_medicine_epinephrine))));
        float blood0 = Blood();
        w.Run(3 * Second);
        Assert.True(Blood() < blood0, "he bleeds off the bed");
        Assert.True(Dose() > 0, "the reagent is in his blood");
        output.WriteLine($"the emergency: health {Health()}, oxy {Oxy()}, blood {Blood()}, dose {Dose()}");

        // ---- 2. Dragged onto the bed: buckled lying (tg's 2 s), chilled out: stasis's traits, the alert, the ripple, the spray.
        Assert.True(medic.Session.Drop(new DragRelease(Entity(him.dm_id), Entity(bed.dm_id), ClickButton.Left, ClickModifiers.None, 4)));
        w.Run(2 * Second + 10);
        Assert.Same(bed, In(w, () => him.buckled));
        Assert.Same(him, In(w, () => bed.occupant));
        Assert.Equal(LYING_DOWN, In(w, () => Dm.num(him.body_position)));
        Assert.Equal(In(w, () => bed.buckle_lying), In(w, () => him.lying_angle)); // tg's update_buckle_vars: 270 facing north or east, else 90
        output.WriteLine($"the bed faces {In(w, () => Dm.num(bed.dir))}: he lies at {In(w, () => him.lying_angle)}");
        bool Stasis() => In(w, () => HAS_TRAIT(him, TRAIT_STASIS));
        Assert.True(Stasis());
        Assert.True(In(w, () => HAS_TRAIT(him, TRAIT_IMMOBILIZED) && HAS_TRAIT(him, TRAIT_HANDS_BLOCKED)));
        long alert = In(w, () => ((atom_movable_screen_alert_status_effect_stasis)him.alerts["stasis"]).dm_id);
        Assert.Contains(patient.Session.Replica.Screen.Values, e => e.Id == alert);
        Assert.Contains(heard, h => h.File == "sound/effects/spray.ogg" && h.Frequency >= 24750 && h.Frequency <= 26550);
        ReplicaEntity body = seen.Entities.Single(e => e.Id == him.dm_id);
        Assert.Contains(Look(body).Filters ?? new List<FilterEntry>(), f => f.Kind == FilterKind.Ripple);
        AnimationTrack ripple = Assert.Single(body.Animation?.Tracks ?? Array.Empty<AnimationTrack>(), t => t.Filter != 0);
        Assert.True(ripple.Loop <= 0, "tg's loop = -1: for ever");
        Assert.True(ripple.Parallel);
        Assert.Equal(2, ripple.Steps.Length); // radius 0, size 2 at once; then radius 32, size 0 over 1.5 s
        output.WriteLine("in stasis ok");

        // ---- 3. A minute on the bed: his health, his blood and the reagent stay as they were.
        (float health, float blood, float dose, float oxy) = (Health(), Blood(), Dose(), Oxy());
        w.Run(60 * Second);
        Assert.Equal(health, Health());
        Assert.Equal(blood, Blood());
        Assert.Equal(dose, Dose());
        Assert.Equal(oxy, Oxy());
        Assert.True(Stasis());
        output.WriteLine($"a minute later: health {Health()}, blood {Blood()}, dose {Dose()}");

        // ---- 4. The medic's alt-click turns it off: the click, synth_no at tg's random pitch, the mattress fades out (5 s, CUBIC, EASE_IN); thawed, he bleeds.
        heard.Clear();
        Assert.True(medic.Session.Click(bed.dm_id, ClickButton.Left, ClickModifiers.Alt));
        w.Run(Second);
        Assert.False(In(w, () => Dm.truthy(bed.stasis_enabled)));
        Assert.Contains(heard, h => h.File == "sound/machines/click.ogg");
        Assert.Contains(heard, h => h.File == "sound/machines/synth/synth_no.ogg" && h.Frequency >= 5120 && h.Frequency <= 8800);
        mattress = Mattress();
        Assert.NotNull(mattress);
        Assert.Equal(0, Look(mattress).Alpha); // the chain's end, which the server applied at once
        AnimationTrack fade = Assert.Single(mattress.Animation.Tracks);
        Assert.Equal(50, fade.Steps[^1].Time);
        Assert.Equal(AnimationEasing.Cubic | AnimationEasing.EaseIn, fade.Steps[^1].Easing);
        Assert.Equal(255, fade.Start.Alpha);
        w.Run(2 * Second); // the bed's next process (SSmachines, every 2 s) thaws him
        Assert.False(Stasis());
        Assert.DoesNotContain(patient.Session.Replica.Screen.Values, e => e.Id == alert);
        Assert.Null(Look(seen.Entities.Single(e => e.Id == him.dm_id)).Filters?.FirstOrDefault(f => f.Kind == FilterKind.Ripple));
        blood = Blood();
        w.Run(5 * Second);
        Assert.True(Blood() < blood, "thawed, he bleeds again");
        Assert.Null(Mattress().Animation); // the fade ended
        output.WriteLine("off ok");

        // on again (tg's 5 s cooldown has passed): the mattress fades in (EASE_OUT), synth_yes, stasis again
        heard.Clear();
        Assert.True(medic.Session.Click(bed.dm_id, ClickButton.Left, ClickModifiers.Alt));
        w.Run(Second);
        Assert.Contains(heard, h => h.File == "sound/machines/synth/synth_yes.ogg" && h.Frequency >= 5120 && h.Frequency <= 8800);
        fade = Assert.Single(Mattress().Animation.Tracks);
        Assert.Equal(AnimationEasing.Cubic | AnimationEasing.EaseOut, fade.Steps[^1].Easing);
        Assert.Equal(255, Look(Mattress()).Alpha);
        w.Run(2 * Second);
        Assert.True(Stasis());

        // ---- 5. The room's APC: its main breaker off stops the bed (synth_no) and thaws him; on again, it runs.
        var apc = In(w, () => (obj_machinery_power_apc)((area)get_area(bed)).apc);
        Assert.NotNull(apc);
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => medic.Session.UiAct(window, action, parameters),
            SendClose = window => medic.Session.UiClose(window),
        };
        UiNode Breaker()
        {
            windows.Sync(medic.Session.Replica.UiWindows, 1280, 720, 1);
            return windows.Get(medic.Session.Replica.UiWindows.Tgui().Single().Window)?.Content.Walk().First(n => n.Kind == UiKind.LabeledItem && n.Label == "Main Breaker").Buttons.Walk().First(n => n.Key == "breaker");
        }
        In(w, () => Waiting.NoSleep(me.forceMove(apc.loc)));
        var card = In(w, () => Dm.@new<obj_item_card_id_advanced_debug>(me.loc));
        w.Run(Second);
        Assert.True(medic.Session.Click(apc.dm_id, ClickButton.Left, ClickModifiers.None)); // the window, locked
        w.Run(Second);
        In(w, () => Waiting.NoSleep(me.put_in_hands(card)));
        Assert.True(medic.Session.Click(apc.dm_id, ClickButton.Left, ClickModifiers.None)); // the debug ID unlocks it
        w.Run(Second);
        heard.Clear();
        Breaker().OnClick();
        w.Run(6 * Second);
        Assert.False(In(w, () => Dm.truthy(apc.operating)));
        Assert.False(In(w, () => Dm.truthy(bed.stasis_running())));
        Assert.Contains(heard, h => h.File == "sound/machines/synth/synth_no.ogg" && h.Frequency >= 5120 && h.Frequency <= 8800);
        Assert.False(Stasis());
        Assert.Equal(0, Look(Mattress()).Alpha);
        output.WriteLine("breaker off ok");
        Breaker().OnClick();
        w.Run(6 * Second);
        Assert.True(In(w, () => Dm.truthy(bed.stasis_running())));
        Assert.True(Stasis());

        // ---- 6. Dead on the bed: his time of death moves with the clock (update_time_of_death), so a defibrillator's limit waits.
        medic.Session.DevCommand("kill Bob");
        w.Run(Second);
        Assert.Equal(DEAD, In(w, () => him.stat));
        float Dead() => In(w, () => Dm.num(world.time) - him.timeofdeath);
        w.Run(30 * Second);
        Assert.True(Dead() <= 2 * SECONDS, $"dead for {Dead()} ds on the bed");
        output.WriteLine($"dead on the bed: {Dead()} ds after 30 s");

        // ---- 7. Stood up from the bed by the medic's hand: thawed, and the clock of his death runs again.
        In(w, () => { qdel(card); return 0; });
        Put(w, me, BedX + 1, BedY);
        Assert.True(medic.Session.Click(bed.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Null(In(w, () => him.buckled));
        Assert.Null(In(w, () => bed.occupant));
        Assert.False(Stasis());
        float off = Dead();
        w.Run(10 * Second);
        Assert.True(Dead() >= off + 9 * SECONDS, "off the bed, his time of death stays where it was");

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
