using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package RG2 (D339): the medicines, toxins, impure reagents and addictions at tg's numbers. One metabolism of a body's blood
/// (a Life tick, seconds_per_tick 2) gives a reagent the metabolization_ratio of its rate in REAGENTS_METABOLISM (0.5 for the
/// stimulants, 1 for a toxin at the base rate). Strange reagent revives a body with enough units and not with too few; rezadone
/// unhusks a burnt husk; the stimulants speed the body and restore its stamina; plasma splashes fire stacks and poisons; lexorin
/// takes the breath and blocks breathing; chloral hydrate puts the body to sleep; fluorosulfuric acid burns a mouth and an item;
/// ammoniated mercury purges a toxin; libital made too impure turns into libitoil in the blood; helbital's overdose brings the
/// necropolis curse; an addiction takes hold from a drug's points, goes through its three withdrawal stages and wears off, and a
/// full heal cures it.
/// </summary>
public class ChemistryRG2Tests
{
    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(9, 9, seed: seed);
        r.In(() =>
        {
            for (int x = 2; x <= 8; x++)
            {
                for (int y = 2; y <= 8; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_floor_iron), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));
            }
        });
        r.Step(Second);
        return r;
    }

    private static datum_reagents Blood(mob_living_carbon who) => (datum_reagents)who.reagents;

    private static void Add(mob_living_carbon who, Type reagent, float units) => Waiting.NoSleep(Blood(who).add_reagent(reagent, units));

    private static float Amount(mob_living_carbon who, Type reagent) => Dm.num(Blood(who).get_reagent_amount(reagent));

    private static void Metabolize(mob_living_carbon who) => Waiting.NoSleep(Blood(who).metabolize(who, SSMOBS_DT(), can_overdose: TRUE));

    private static float Num(object x) => Dm.num(x);

    private static string Said(Feed feed) => string.Join("\n", feed.Chat.Select(c => c.Html));

    private static obj_item_bodypart Part(mob_living_carbon who, string zone) => (obj_item_bodypart)who.get_bodypart(zone);

    private static void Hurt(mob_living_carbon who, string zone, float brute, float burn)
    {
        Waiting.NoSleep(Part(who, zone).receive_damage(brute, burn, wound_bonus: CANT_WOUND));
        Waiting.NoSleep(who.updatehealth());
    }

    private static void Expose<T>(mob_living who, float methods, float units) where T : datum_reagent
        => Waiting.NoSleep(Dm.@new<T>().expose_mob(who, methods, units));

    [Fact]
    public void StrangeReagentRevivesWithEnoughUnitsAndHealsTheExcess()
    {
        var r = Room(10001);
        Feed feed = Listen(r);
        mob_living_carbon_human d = r.Dummy(4, 4);
        mob_living_carbon_human e = r.Dummy(6, 4);
        r.Join(5, 5, "witness"); // a player beside both bodies sees the visible messages: a dummy has no client, and the dead are blind
        r.In(() =>
        {
            foreach (mob_living_carbon_human body in new[] { d, e })
            {
                Hurt(body, BODY_ZONE_CHEST, 100, 100);
                if (body.stat != DEAD)
                    Waiting.NoSleep(body.death());
                Assert.Equal(DEAD, body.stat);
            }

            // 100 - 200 = -100 health: round(100 / 5, 0.1) = 20 units to revive (medicine_reagents.dm:1103); 10 are too few
            Expose<datum_reagent_medicine_strange_reagent_instant>(e, INGEST, 10);
            Assert.Equal(DEAD, e.stat);
            Assert.True(feed.Any("convulses a bit, and then falls still once more"), Said(feed));

            // 30 units: 20 * 5 + (30 - 20) * 5 * 0.8 = 140 healing; half to brute (70), the rest to burn (70), then revive
            Expose<datum_reagent_medicine_strange_reagent_instant>(d, INGEST, 30);
            Assert.True(feed.Any("starts convulsing!"), Said(feed));
            Assert.NotEqual(DEAD, d.stat);
            Assert.Equal(30f, Num(d.get_brute_loss()), 2);
            Assert.Equal(30f, Num(d.get_fire_loss()), 2);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void RezadoneUnhusksABurntHusk()
    {
        var r = Room(10002);
        mob_living_carbon_human d = r.Dummy(4, 4);
        r.In(() =>
        {
            d.become_husk(BURN);
            Hurt(d, BODY_ZONE_CHEST, 0, 30);
            Assert.True(HAS_TRAIT_FROM(d, TRAIT_HUSK, BURN));
            // 5 or more units touching, a husk from burns under UNHUSK_DAMAGE_THRESHOLD (50) burn: cure_husk(BURN) (medicine_reagents.dm:252)
            Expose<datum_reagent_medicine_rezadone>(d, TOUCH, 10);
            Assert.False(HAS_TRAIT(d, TRAIT_HUSK));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void StimulantsSpeedTheBodyAndRestoreItsStamina()
    {
        var r = Room(10003);
        mob_living_carbon_human d = r.Dummy(4, 4);
        r.In(() =>
        {
            Waiting.NoSleep(d.adjust_stamina_loss(50));
            Waiting.NoSleep(d.Paralyze(10 * SECONDS));
            float paralyzed = Num(d.AmountParalyzed());
            Add(d, typeof(datum_reagent_medicine_stimulants), 10);
            Metabolize(d);
            // on_mob_metabolize: the stimulants' movespeed modifier; -12 * 0.5 * 2 stamina, AdjustAllImmobility(-60 * 0.5 * 2)
            Assert.True(Dm.truthy(d.has_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_stimulants))));
            Assert.Equal(38f, Num(d.get_stamina_loss()), 2);
            Assert.True(Num(d.AmountParalyzed()) <= paralyzed - 60, $"paralyzed {paralyzed} -> {Num(d.AmountParalyzed())}");
            Waiting.NoSleep(Blood(d).remove_reagent(typeof(datum_reagent_medicine_stimulants), 100));
            Assert.False(Dm.truthy(d.has_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_stimulants))));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void PlasmaSplashesFireStacksAndPoisonsTheBlood()
    {
        var r = Room(10004);
        mob_living_carbon_human d = r.Dummy(4, 4);
        r.In(() =>
        {
            // a splash: adjust_fire_stacks(reac_volume / 5) (toxin_reagents.dm:152)
            Expose<datum_reagent_toxin_plasma>(d, TOUCH, 10);
            var stacks = (datum_status_effect_fire_handler)d.has_status_effect(typeof(datum_status_effect_fire_handler_fire_stacks));
            Assert.NotNull(stacks);
            Assert.Equal(2f, Num(stacks.stacks), 2);

            Add(d, typeof(datum_reagent_toxin_plasma), 10);
            Add(d, typeof(datum_reagent_medicine_epinephrine), 5);
            Metabolize(d);
            Assert.True(Num(d.get_tox_loss()) > 0, "plasma did no toxin damage");
            // the plasma purges 1 * ratio * 2 epinephrine beside its own metabolism
            Assert.True(Amount(d, typeof(datum_reagent_medicine_epinephrine)) < 5 - 0.1f - 0.5f);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void LexorinTakesTheBreathAndBlocksBreathing()
    {
        var r = Room(10005);
        mob_living_carbon_human d = r.Dummy(4, 4);
        r.In(() =>
        {
            Add(d, typeof(datum_reagent_toxin_lexorin), 10);
            Metabolize(d);
            Assert.True(d.losebreath > 0, $"losebreath {d.losebreath}");
            Assert.True(Num(d.get_oxy_loss()) > 0);
            // block_breath answers the attempt to breathe with COMSIG_CARBON_BLOCK_BREATH (toxin_reagents.dm:250)
            Assert.Equal(COMSIG_CARBON_BLOCK_BREATH, (int)Num(d.SEND_SIGNAL(COMSIG_CARBON_ATTEMPT_BREATHE, Dm.list(d))));
            Waiting.NoSleep(Blood(d).remove_reagent(typeof(datum_reagent_toxin_lexorin), 100));
            Assert.False(Dm.truthy(d.SEND_SIGNAL(COMSIG_CARBON_ATTEMPT_BREATHE, Dm.list(d))));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ChloralHydratePutsTheBodyToSleep()
    {
        var r = Room(10006);
        mob_living_carbon_human d = r.Dummy(4, 4);
        r.In(() =>
        {
            Add(d, typeof(datum_reagent_toxin_chloralhydrate), 30);
            for (int i = 0; i < 6; i++)
                Metabolize(d);
            // cycles 2 to 11: confusion and drowsiness, no sleep yet
            Assert.False(Dm.truthy(d.IsSleeping()));
            Assert.NotNull(d.has_status_effect(typeof(datum_status_effect_drowsiness)));
            for (int i = 0; i < 8; i++)
                Metabolize(d);
            Assert.True(Dm.truthy(d.IsSleeping()));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void FluorosulfuricAcidBurnsAMouthAndCorrodesAnItem()
    {
        var r = Room(10007);
        mob_living_carbon_human d = r.Dummy(4, 4);
        r.In(() =>
        {
            // INGEST: adjust_brute_loss(min(6 * toxpwr, reac_volume * toxpwr)) = min(12, 20) (toxin_reagents.dm:1288)
            Expose<datum_reagent_toxin_acid_fluacid>(d, INGEST, 10);
            Assert.Equal(12f, Num(d.get_brute_loss()), 2);

            obj_item_screwdriver tool = Dm.@new<obj_item_screwdriver>(locate(5, 5, 1));
            Waiting.NoSleep(Dm.@new<datum_reagent_toxin_acid_fluacid>().expose_obj(tool, 10, TOUCH));
            Assert.NotNull(tool.GetComponent(typeof(datum_component_acid)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AmmoniatedMercuryPurgesAToxin()
    {
        var r = Room(10008);
        mob_living_carbon_human d = r.Dummy(4, 4);
        r.In(() =>
        {
            Add(d, typeof(datum_reagent_toxin), 10);
            Add(d, typeof(datum_reagent_medicine_ammoniated_mercury), 10);
            Metabolize(d);
            // 25 * purge_multiplier 1 * ratio 0.1 * 2 = 5 units purged, beside the toxin's own metabolism (medicine_reagents.dm:498)
            Assert.True(Amount(d, typeof(datum_reagent_toxin)) <= 5f, $"toxin left {Amount(d, typeof(datum_reagent_toxin))}");
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void LibitalTooImpureTurnsIntoLibitoil()
    {
        var r = Room(10009);
        mob_living_carbon_human d = r.Dummy(4, 4);
        r.In(() =>
        {
            var liver = (obj_item_organ_liver)d.get_organ_slot(ORGAN_SLOT_LIVER);
            float tolerance = liver.alcohol_tolerance;
            // purity 0.2 under libital's inverse_chem_val 0.3: all of it goes in as libitoil (holder/mob_life.dm process_mob_reagent_purity)
            Waiting.NoSleep(Blood(d).add_reagent(typeof(datum_reagent_medicine_c2_libital), 10, added_purity: 0.2f));
            Assert.Equal(0f, Amount(d, typeof(datum_reagent_medicine_c2_libital)), 3);
            Assert.Equal(10f, Amount(d, typeof(datum_reagent_inverse_libitoil)), 3);
            // libitoil's on_mob_add doubles the liver's alcohol tolerance; it halves back when the libitoil leaves
            Assert.Equal(tolerance * 2, liver.alcohol_tolerance, 5);
            Waiting.NoSleep(Blood(d).remove_reagent(typeof(datum_reagent_inverse_libitoil), 100));
            Assert.Equal(tolerance, liver.alcohol_tolerance, 5);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void HelbitalOverdoseBringsTheNecropolisCurse()
    {
        var r = Room(10010);
        mob_living_carbon_human d = r.Dummy(4, 4);
        r.In(() =>
        {
            Add(d, typeof(datum_reagent_medicine_c2_helbital), 40);
            Metabolize(d);
            // overdose_threshold 35: apply_necropolis_curse(CURSE_WASTING | CURSE_BLINDING) (cat2_medicine_reagents.dm:85)
            var curse = (datum_status_effect_necropolis_curse)d.has_status_effect(typeof(datum_status_effect_necropolis_curse));
            Assert.NotNull(curse);
            Assert.True(Dm.truthy(Dm.bitand(curse.curse_flags, CURSE_BLINDING)));
            // the blinding curse is the "curse" fullscreen over the view (debuffs.dm:626), not the blind status
            Assert.IsType<atom_movable_screen_fullscreen_curse>(d.screens["curse"]);
            // on_mob_delete takes the curse away, and its fullscreen with it
            Waiting.NoSleep(Blood(d).remove_reagent(typeof(datum_reagent_medicine_c2_helbital), 100));
            Assert.Null(d.has_status_effect(typeof(datum_status_effect_necropolis_curse)));
            Assert.Null(d.screens["curse"]);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnAddictionTakesHoldGoesThroughWithdrawalAndWearsOff()
    {
        var r = Room(10011);
        Feed feed = Listen(r);
        mob_living_carbon_human d = r.Dummy(4, 4);
        r.In(() =>
        {
            Waiting.NoSleep(d.mind_initialize());
            Type stimulants = typeof(datum_addiction_stimulants);
            Add(d, typeof(datum_reagent_medicine_stimulants), 10);
            Metabolize(d);
            // each metabolism adds 600 / (150 / the metabolized volume) points (holder/mob_life.dm:120)
            float points = Num(d.mind.addiction_points[stimulants]);
            Assert.True(points > 0, "no addiction points");
            Assert.Null(LAZYACCESS(d.mind.active_addictions, stimulants));

            // over addiction_gain_threshold (600): addicted at the first cycle
            d.mind.add_addiction_points(stimulants, 600);
            Assert.Equal(1f, Num(LAZYACCESS(d.mind.active_addictions, stimulants)), 3);
            Waiting.NoSleep(Blood(d).remove_reagent(typeof(datum_reagent_medicine_stimulants), 100));

            var addiction = (datum_addiction)GLOB.addictions[stimulants];
            bool stage1 = false, stage2 = false, stage3 = false;
            for (int i = 0; i < 200 && LAZYACCESS(d.mind.active_addictions, stimulants) != null; i++)
            {
                Waiting.NoSleep(addiction.process_addiction(d, 2));
                stage1 |= Dm.truthy(d.has_actionspeed_modifier(typeof(datum_actionspeed_modifier_stimulants)));
                stage2 |= d.has_status_effect(typeof(datum_status_effect_woozy)) != null;
                stage3 |= Dm.truthy(d.has_movespeed_modifier(typeof(datum_movespeed_modifier_stimulants)));
            }
            // cycles 61, 121 and 181 enter the stages (the actionspeed modifier, woozy, the movespeed modifier); the points fall by
            // 0.5, 0.5, 1 and 1.5 a second per stage until under addiction_loss_threshold (400), when the addiction is lost
            Assert.True(stage1 && stage2 && stage3, $"stages {stage1} {stage2} {stage3}");
            Assert.Null(LAZYACCESS(d.mind.active_addictions, stimulants));
            Assert.True(feed.Any("You feel like you've gotten over your need for drugs."));
            Assert.False(Dm.truthy(d.has_movespeed_modifier(typeof(datum_movespeed_modifier_stimulants))));
            Assert.Null(d.has_status_effect(typeof(datum_status_effect_woozy)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AFullHealCuresAnAddiction()
    {
        var r = Room(10012);
        mob_living_carbon_human d = r.Dummy(4, 4);
        r.In(() =>
        {
            Waiting.NoSleep(d.mind_initialize());
            Type opioids = typeof(datum_addiction_opioids);
            d.mind.add_addiction_points(opioids, 700);
            Assert.NotNull(LAZYACCESS(d.mind.active_addictions, opioids));
            // HEAL_TRAUMAS: "Addictions are like traumas" (carbon.dm fully_heal)
            Waiting.NoSleep(d.fully_heal(HEAL_ALL));
            Assert.Null(LAZYACCESS(d.mind.active_addictions, opioids));
            Assert.Null(LAZYACCESS(d.mind.addiction_points, opioids)); // removed at 0 (_mind.dm:486)
        });
        r.AssertNoRuntimes();
    }
}
