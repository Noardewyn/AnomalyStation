using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M8;

/// <summary>
/// M8 slice C: the reagent containers deliver at tg's amounts, through a player's clicks. The syringe draws its 5 units from a
/// bottle with a right click and injects another after CHEM_INTERACT_DELAY's 3 seconds (syringes.dm:48, :96); the dropper takes and
/// drops its 5 units (dropper.dm:15); a cup's transfer amount steps through its list (reagent_containers.dm:122); the medipen
/// injects at once with the hypospray's sound and is used up (hypospray.dm:54, :166); a donor's blood of the wrong type turns into
/// half as much toxin (blood.dm:370); the IV drip feeds its rate per second and draws blood the other way (iv_drip.dm:220); the pepper
/// spray's puff blinds and knocks down a bare face (food_reagents.dm:469).
/// </summary>
public class DeliveryTests
{
    private const int Second = 20;

    private sealed class Feed : IMessageSink
    {
        public List<(object Target, string Html)> Chat { get; } = new();

        void IMessageSink.Chat(object target, DmList message) => Chat.Add((target, Dm.str(message?["html"])));

        public void Output(object target, string text) { }

        public void Balloon(object viewer, object atom, string text) { }

        public string To(object who) => string.Join("\n", Chat.Where(c => ReferenceEquals(c.Target, who) || (c.Target is client cl && ReferenceEquals(cl.mob, who))).Select(c => c.Html));
    }

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(12, 9, seed: seed);
        r.In(() =>
        {
            for (int x = 2; x <= 11; x++)
            {
                for (int y = 2; y <= 8; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_floor_iron), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));
            }
        });
        r.Step(Second);
        PresentationSinks.Of(r.World).Messages = new Feed();
        return r;
    }

    private static datum_reagents R(atom a) => (datum_reagents)a.reagents;

    private static float Amount(atom a, Type reagent) => Dm.num(R(a).get_reagent_amount(reagent));

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
        => r.In(() =>
        {
            var item = Dm.@new<T>(who.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_active_hand(item))));
            return item;
        });

    private static string ChatOf(AudioRoom r, mob who) => ((Feed)PresentationSinks.Of(r.World).Messages).To(who);

    private static void Click(AudioRoom r, mob_living_carbon_human who, atom target, bool right = false)
    {
        r.In(() =>
        {
            who.next_click = -1;
            who.next_move = -1;
        });
        r.Command(() => ClientSeam.Click(who.client, target, right ? "right=1;button=right" : "left=1;button=left"));
        r.Step();
    }

    [Fact]
    public void TheOxygenDeprivationKitHoldsTgsConvermolSyringesSalbutamolPenMedipenAndIronPills()
    {
        var r = Room(8300);
        r.In(() =>
        {
            var kit = Dm.@new<obj_item_storage_medkit_o2>(locate(5, 5, 1));
            List<Type> inside = Dm.each_cast<atom>(kit.contents).Select(a => a.GetType()).ToList();
            Assert.Equal(3, inside.Count(t => t == typeof(obj_item_reagent_containers_syringe_convermol))); // medkit.dm:228
            Assert.Single(inside, t => t == typeof(obj_item_reagent_containers_hypospray_medipen_salbutamol));
            Assert.Single(inside, t => t == typeof(obj_item_reagent_containers_hypospray_medipen));
            Assert.Single(inside, t => t == typeof(obj_item_storage_pill_bottle_iron));
            Assert.Equal(6, inside.Count);
            Assert.Equal("oxy", kit.get_medbot_skin());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSyringeDrawsItsFiveUnitsFromABottleAndInjectsAnotherAfterThreeSeconds()
    {
        var r = Room(8301);
        mob_living_carbon_human alice = r.Join(5, 5, "Alice");
        mob_living_carbon_human bob = r.Dummy(6, 5);
        var bottle = r.In(() => Dm.@new<obj_item_reagent_containers_cup_bottle_epinephrine>(locate(5, 6, 1)));
        var syringe = Hold<obj_item_reagent_containers_syringe>(r, alice);
        float full = r.In(() => Amount(bottle, typeof(datum_reagent_medicine_epinephrine)));

        Click(r, alice, bottle, right: true);
        r.In(() =>
        {
            Assert.True(5f == Amount(syringe, typeof(datum_reagent_medicine_epinephrine)), ChatOf(r, alice));
            Assert.Equal(5f, Amount(syringe, typeof(datum_reagent_medicine_epinephrine)), 3);
            Assert.Equal(full - 5, Amount(bottle, typeof(datum_reagent_medicine_epinephrine)), 3);
            // round(5 / 15 * 15, 5): the 5-unit state and its fill overlay coloured as epinephrine
            Assert.Equal("syringe_5", syringe.icon_state);
            var fill = Dm.each(syringe.overlays).OfType<IDmAppearance>().Single(o => o.icon_state == "syringe5");
            Assert.Equal(mix_color_from_reagents(R(syringe).reagent_list), fill.color);
        });

        Click(r, alice, bob);
        r.Step(3 * Second - 2);
        r.In(() => Assert.Equal(0f, Amount(bob, typeof(datum_reagent_medicine_epinephrine)), 3));
        r.Step(3);
        r.In(() =>
        {
            // the five units, less what Bob's Life may have metabolized since (epinephrine's 0.1 a tick, the subsystems' order)
            Assert.InRange(Amount(bob, typeof(datum_reagent_medicine_epinephrine)), 4.89f, 5.001f);
            Assert.Equal(0f, Dm.num(R(syringe).total_volume), 3);
            Assert.Equal("syringe_0", syringe.icon_state);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDropperTakesAndDropsItsFiveUnitsAndACupStepsItsTransferAmount()
    {
        var r = Room(8302);
        mob_living_carbon_human alice = r.Join(5, 5, "Alice");
        var source = r.In(() => Dm.@new<obj_item_reagent_containers_cup_beaker>(locate(5, 6, 1)));
        var target = r.In(() => Dm.@new<obj_item_reagent_containers_cup_beaker>(locate(6, 5, 1)));
        r.In(() => Waiting.NoSleep(R(source).add_reagent(typeof(datum_reagent_water), 20)));
        var dropper = Hold<obj_item_reagent_containers_dropper>(r, alice);

        Click(r, alice, source);
        r.In(() => Assert.Equal(5f, Dm.num(R(dropper).total_volume), 3));
        Click(r, alice, target);
        r.In(() =>
        {
            Assert.Equal(0f, Dm.num(R(dropper).total_volume), 3);
            Assert.Equal(5f, Amount(target, typeof(datum_reagent_water)), 3);
            Assert.Equal(15f, Amount(source, typeof(datum_reagent_water)), 3);
        });

        // The beaker's 10 units step forward to 15, backward twice to 5 (reagent_containers.dm:108, :115)
        r.In(() =>
        {
            Assert.Equal(10f, source.amount_per_transfer_from_this);
            source.change_transfer_amount(alice, FORWARD);
            Assert.Equal(15f, source.amount_per_transfer_from_this);
            source.change_transfer_amount(alice, BACKWARD);
            source.change_transfer_amount(alice, BACKWARD);
            Assert.Equal(5f, source.amount_per_transfer_from_this);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMedipenInjectsAtOnceWithTheHyposprayHeardByBothAndIsUsedUp()
    {
        var r = Room(8303);
        mob_living_carbon_human alice = r.Join(5, 5, "Alice");
        mob_living_carbon_human bob = r.Join(6, 5, "Bob");
        var pen = Hold<obj_item_reagent_containers_hypospray_medipen>(r, alice);
        float dose = r.In(() => Amount(pen, typeof(datum_reagent_medicine_epinephrine)));
        Assert.True(dose > 0);
        r.Sounds.Heard.Clear();

        Click(r, alice, bob);
        r.In(() =>
        {
            Assert.Equal(dose, Amount(bob, typeof(datum_reagent_medicine_epinephrine)), 3);
            Assert.Equal(0f, Dm.num(R(pen).total_volume), 3);
            Assert.True(Dm.truthy(pen.used_up));
            Assert.Equal("medipen0", pen.icon_state);
        });
        Assert.Single(r.Sounds.Of(alice, "sound/items/hypospray.ogg"));
        Assert.Single(r.Sounds.Of(bob, "sound/items/hypospray.ogg"));

        // A used pen does nothing more
        r.Sounds.Heard.Clear();
        Click(r, alice, bob);
        Assert.Empty(r.Sounds.Of(alice, "sound/items/hypospray.ogg"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void BloodOfAnIncompatibleDonorBecomesHalfAsMuchToxin()
    {
        var r = Room(8304);
        mob_living_carbon_human donor = r.Dummy(5, 5);
        mob_living_carbon_human universal = r.Dummy(6, 5);
        mob_living_carbon_human patient = r.Dummy(7, 5);
        r.In(() =>
        {
            donor.set_blood_type("B+");
            universal.set_blood_type("O-");
            patient.set_blood_type("A-");
            Assert.False(Dm.truthy(patient.get_blood_compatibility(donor)));
            Assert.True(Dm.truthy(patient.get_blood_compatibility(universal)));
            Assert.True(Dm.truthy(donor.get_blood_compatibility(universal)));

            float blood = Dm.num(patient.get_blood_volume());
            Assert.Equal(10f, Dm.num(Waiting.NoSleep(donor.transfer_blood_to(patient, 10))), 3);
            Assert.Equal(5f, Amount(patient, typeof(datum_reagent_toxin)), 3);
            Assert.Equal(blood, Dm.num(patient.get_blood_volume()), 3);

            Assert.Equal(10f, Dm.num(Waiting.NoSleep(universal.transfer_blood_to(patient, 10))), 3);
            Assert.Equal(blood + 10, Dm.num(patient.get_blood_volume()), 3);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheIvDripFeedsItsRatePerSecondAndDrawsBloodTheOtherWay()
    {
        var r = Room(8305);
        mob_living_carbon_human alice = r.Join(5, 5, "Alice");
        mob_living_carbon_human bob = r.Dummy(6, 5);
        var drip = r.In(() => Dm.@new<obj_machinery_iv_drip>(locate(5, 6, 1)));
        var pack = Hold<obj_item_reagent_containers_blood_o_minus>(r, alice);
        float packed = r.In(() => Dm.num(R(pack).total_volume));

        Click(r, alice, drip);
        r.In(() => Assert.Same(pack, drip.reagent_container));
        r.Command(() => ClientSeam.Drop(alice.client, drip, bob, "icon-x=16;icon-y=16;left=1;button=left", "icon-x=16;icon-y=16;left=1;button=left", 2));
        r.Step(Second + 2); // attaching to a mob takes a second
        r.In(() =>
        {
            Assert.NotNull(drip.attachment);
            Assert.Same(bob, drip.attachment.attached_to);
            Assert.Equal(5f, drip.transfer_rate);
            Assert.Equal("iv_drip_injecting", drip.icon_state);
        });

        // One process of one second moves the 5 units of the rate
        float fed = r.In(() =>
        {
            float before = Dm.num(R(pack).total_volume);
            drip.process(1);
            return before - Dm.num(R(pack).total_volume);
        });
        Assert.Equal(5f, fed, 3);
        // a blood pack's blood is synthetic: Bob's blood now carries its share (carbon/life.dm:493)
        Assert.True(r.In(() => Dm.num(bob.get_blood_synth_content())) > 0);
        Assert.True(r.In(() => Dm.num(R(pack).total_volume)) < packed);

        // Emptied, the pack draws Bob's blood back at the same rate
        // the Toggle Mode verb, as the client's verb call runs it (usr is the caller)
        r.In(() =>
        {
            Waiting.NoSleep(R(pack).clear_reagents());
            usr = alice;
            drip.toggle_mode();
            usr = null;
        });
        r.Step();
        r.In(() =>
        {
            Assert.Equal(IV_TAKING, drip.mode);
            float blood = Dm.num(bob.get_blood_volume());
            drip.process(1);
            Assert.Equal(5f, Dm.num(R(pack).total_volume), 3);
            Assert.Equal(blood - 5, Dm.num(bob.get_blood_volume()), 3);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePepperSprayPuffBlindsAndKnocksDownABareFace()
    {
        var r = Room(8306);
        mob_living_carbon_human alice = r.Join(5, 5, "Alice");
        mob_living_carbon_human bob = r.Dummy(7, 5);
        var spray = Hold<obj_item_reagent_containers_spray_pepper>(r, alice);
        float full = r.In(() => Dm.num(R(spray).total_volume));
        r.Sounds.Heard.Clear();

        Click(r, alice, bob);
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.Equal(full - spray.amount_per_transfer_from_this, Dm.num(R(spray).total_volume), 3);
            Assert.NotNull(bob.has_status_effect(typeof(datum_status_effect_temporary_blindness)));
            Assert.NotNull(bob.has_status_effect(typeof(datum_status_effect_incapacitating_knockdown)));
            Assert.True(Dm.truthy(bob.has_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_pepperspray))));
        });
        Assert.NotEmpty(r.Sounds.Of(alice, "sound/effects/spray2.ogg"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APillsSugarShellHoldsItsReagentsInTheStomachForItsLayersSeconds()
    {
        var r = Room(8307);
        mob_living_carbon_human bob = r.Dummy(6, 5);
        var pill = r.In(() =>
        {
            var p = Dm.@new<obj_item_reagent_containers_applicator_pill_iron>(bob.loc);
            Waiting.NoSleep(p.on_consumption(bob, bob));
            return p;
        });
        // The stomach digests its contents every third mob tick (_stomach.dm:258: every 6 seconds, 2 seconds each): three layers
        // leave one after the first, whose one-second timer finds the stomach in the chest, not in the owner (pill.dm:137, tg's
        // check; organ_movement.dm:133 puts organs in their bodypart), so the next digestion dissolves the pill.
        r.In(() =>
        {
            Assert.Same(bob, pill.loc);
            Assert.False(Dm.truthy(bob.has_reagent(typeof(datum_reagent_iron))));
        });
        r.Step(13 * Second);
        r.In(() =>
        {
            Assert.True(Dm.truthy(pill.gc_destroyed), $"layers {pill.layers_remaining}");
            Assert.True(Dm.truthy(bob.has_reagent(typeof(datum_reagent_iron))));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnInertContainerThrownOrDroppedSoundsAsAnEmptyOneInsteadOfReadingItsMissingHolder()
    {
        // The cult's unholy water flask's type is inert (its Initialize is not translated while the cult is not ported, D333), so
        // it has no reagent holder; the Meta singularity scenarios threw such containers into walls (reagent_containers.dm:355
        // read reagents.total_volume). (The coffee cup this test first used is live since gaps H3, the potion bottle with the
        // necropolis loot.)
        var r = Room(8302);
        var cup = r.In(() => Dm.@new<obj_item_reagent_containers_cup_beaker_unholywater>(locate(5, 5, 1)));
        r.In(() =>
        {
            Assert.Null(cup.reagents);
            cup.throw_drop_sound_chain(YEET_SOUND_VOLUME);
            cup.play_drop_sound();
        });
        r.AssertNoRuntimes();
    }
}
