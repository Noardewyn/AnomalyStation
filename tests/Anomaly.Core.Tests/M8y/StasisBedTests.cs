using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.M5.FurnitureTests;

namespace Anomaly.Core.Tests.M8y;

/// <summary>
/// M8y slice B: the stasis bed (game/machinery/stasis.dm), the stasis status effect (debuffs.dm:340), free_operation and the
/// elevation element in a booted toy room. A bed at (5, 4) faces south; most tests set the patient as the buckle would set him (the
/// bed's post_buckle_mob and post_unbuckle_mob called directly); the last three buckle him for real (buckling.dm, slice A).
/// </summary>
public class StasisBedTests
{
    private const int Second = 20;
    private const int BedX = 5, BedY = 4;
    private const string Click = "sound/machines/click.ogg", Yes = "sound/machines/synth/synth_yes.ogg", No = "sound/machines/synth/synth_no.ogg", Spray = "sound/effects/spray.ogg";

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Bob, obj_machinery_stasis Bed);

    private static Rig Build(ulong seed)
    {
        var (r, bob) = Room(seed);
        var bed = r.In(() => Dm.@new<obj_machinery_stasis>(locate(BedX, BedY, 1)));
        r.Step(Second); // LateInitialize: the power relationship, power_change (the first power sound)
        r.AssertNoRuntimes();
        return new Rig(r, bob, bed);
    }

    private static void CutPower(Rig g, bool off)
    {
        g.R.In(() =>
        {
            area room = (area)get_area(g.Bed);
            room.requires_power = TRUE;
            room.power_equip = off ? FALSE : TRUE;
            room.power_change();
        });
    }

    private static int Heard(Rig g, string file) => g.R.Sounds.Heard.Count(h => h.File == file);

    private static bool Stasis(mob_living who) => HAS_TRAIT(who, TRAIT_STASIS);

    private static void Apply(mob_living who, string source = STASIS_MACHINE_EFFECT)
        => Waiting.NoSleep(who.apply_status_effect(typeof(datum_status_effect_grouped_stasis), source));

    [Fact]
    public void TheStatusEffectHoldsItsTraitsFilterAndAlertAndEndsMetabolizing()
    {
        var g = Build(8901);
        mob_living_carbon_human pat = g.R.Dummy(BedX + 1, BedY);
        g.R.In(() =>
        {
            var blood = (datum_reagents)pat.reagents;
            Waiting.NoSleep(blood.add_reagent(typeof(datum_reagent_medicine_cryoxadone), 5));
            var cryox = (datum_reagent)Dm.each(blood.reagent_list).First();
            cryox.metabolizing = TRUE;

            Apply(pat);
            Assert.True(Stasis(pat));
            Assert.True(HAS_TRAIT(pat, TRAIT_IMMOBILIZED));
            Assert.True(HAS_TRAIT(pat, TRAIT_HANDS_BLOCKED));
            Assert.True(HAS_TRAIT_FROM(pat, TRAIT_STASIS, TRAIT_STATUS_EFFECT("stasis")));
            Assert.NotNull(pat.get_filter("stasis_status_ripple"));
            Assert.NotNull(pat.alerts["stasis"]);
            Assert.False(Dm.truthy(cryox.metabolizing)); // end_metabolization (:359): the reagent stops working in him
            Assert.Equal(5f, Dm.num(blood.get_reagent_amount(typeof(datum_reagent_medicine_cryoxadone))), 3);

            // a second source joins the one effect (grouped); the first leaving keeps it
            Apply(pat, "other");
            Assert.Equal(2f, (float)((datum_status_effect_grouped)pat.has_status_effect(typeof(datum_status_effect_grouped_stasis))).sources.len);
            pat.remove_status_effect(typeof(datum_status_effect_grouped_stasis), STASIS_MACHINE_EFFECT);
            Assert.True(Stasis(pat));
            pat.remove_status_effect(typeof(datum_status_effect_grouped_stasis), "other");
            Assert.False(Stasis(pat));
            Assert.False(HAS_TRAIT(pat, TRAIT_IMMOBILIZED));
            Assert.False(HAS_TRAIT(pat, TRAIT_HANDS_BLOCKED));
            Assert.Null(pat.get_filter("stasis_status_ripple"));
            Assert.Null(pat.alerts["stasis"]);
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ADeadBodyKeepsItsTimeOfDeathWhileInStasis()
    {
        var g = Build(8902);
        mob_living_carbon_human pat = g.R.Dummy(BedX + 1, BedY);
        float died = 0, applied = 0;
        g.R.In(() =>
        {
            pat.stat = DEAD;
            died = pat.timeofdeath = Dm.num(world.time);
            applied = Dm.num(world.time);
            Apply(pat);
            Assert.Equal(applied, Dm.num(((datum_status_effect_grouped_stasis)pat.has_status_effect(typeof(datum_status_effect_grouped_stasis))).last_dead_time));
        });
        g.R.Step(10 * Second); // the effect's one-second ticks fold each elapsed second into the time of death (:373)
        g.R.In(() =>
        {
            float now = Dm.num(world.time);
            pat.remove_status_effect(typeof(datum_status_effect_grouped_stasis), STASIS_MACHINE_EFFECT); // on_remove (:376): the last stretch
            // he has been dead for exactly as long as he was in stasis: the time of death moved forward by all of it
            Assert.Equal(died + (Dm.num(world.time) - applied), pat.timeofdeath, 3);
            Assert.True(Dm.num(world.time) - applied >= 10 * 10 - 1);
            Assert.Equal(round_timestamp(wtime: pat.timeofdeath), Dm.str(pat.station_timestamp_timeofdeath));
            Assert.Equal(now, Dm.num(world.time));
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ALivingBodyHasNoTimeOfDeathToMove()
    {
        var g = Build(8903);
        mob_living_carbon_human pat = g.R.Dummy(BedX + 1, BedY);
        g.R.In(() =>
        {
            float before = pat.timeofdeath;
            Apply(pat);
            Assert.Null(((datum_status_effect_grouped_stasis)pat.has_status_effect(typeof(datum_status_effect_grouped_stasis))).last_dead_time);
            pat.remove_status_effect(typeof(datum_status_effect_grouped_stasis), STASIS_MACHINE_EFFECT);
            Assert.Equal(before, pat.timeofdeath);
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AltClickTogglesTheBedAndItsFiveSecondCooldownBlocksTheNextClick()
    {
        var g = Build(8904);
        g.R.In(() =>
        {
            Assert.Equal(1f, g.Bed.stasis_enabled);
            Assert.Contains("Alt-click to turn off the machine.", string.Join("|", Dm.each(Waiting.NoSleep(g.Bed.examine(g.Bob))).Select(Dm.str)), StringComparison.Ordinal);
            Assert.Equal(CLICK_ACTION_SUCCESS, Waiting.NoSleep(g.Bed.click_alt(g.Bob)));
            Assert.Equal(0f, g.Bed.stasis_enabled);
            Assert.Equal(Dm.num(world.time) + 50, g.Bed.stasis_can_toggle);
            Assert.False(Dm.truthy(g.Bed.stasis_running()));
            Assert.Contains("Alt-click to turn on the machine.", string.Join("|", Dm.each(Waiting.NoSleep(g.Bed.examine(g.Bob))).Select(Dm.str)), StringComparison.Ordinal);
            // inside the cooldown (:60)
            Assert.Equal(CLICK_ACTION_BLOCKING, Waiting.NoSleep(g.Bed.click_alt(g.Bob)));
            Assert.Equal(0f, g.Bed.stasis_enabled);
        });
        Assert.Equal(1, Heard(g, Click)); // only the click that worked
        g.R.Step(4 * Second); // 4 s: still inside
        g.R.In(() => Assert.Equal(CLICK_ACTION_BLOCKING, Waiting.NoSleep(g.Bed.click_alt(g.Bob))));
        g.R.Step(2 * Second); // 6 s
        g.R.In(() =>
        {
            Assert.Equal(CLICK_ACTION_SUCCESS, Waiting.NoSleep(g.Bed.click_alt(g.Bob)));
            Assert.Equal(1f, g.Bed.stasis_enabled);
            Assert.True(Dm.truthy(g.Bed.stasis_running()));
        });
        Assert.Equal(2, Heard(g, Click));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void StasisRunsOnlyEnabledAndPoweredAndSaysSoWithItsSynthSound()
    {
        var g = Build(8905);
        // power_change at the late init: the bed is running, so it said so once (:49)
        Assert.Equal(1, Heard(g, Yes));
        g.R.In(() => Assert.True(Dm.truthy(g.Bed.stasis_running())));

        CutPower(g, true);
        g.R.In(() =>
        {
            Assert.True(Dm.truthy(Dm.bitand(g.Bed.machine_stat, NOPOWER)));
            Assert.False(Dm.truthy(g.Bed.stasis_running()));
            Assert.Equal(0f, g.Bed.last_stasis_sound);
        });
        Assert.Equal(1, Heard(g, No));
        var no = g.R.Sounds.Heard.Single(h => h.File == No);
        Assert.InRange(no.Frequency, 5120, 8800);

        CutPower(g, false); // power back: it says yes again
        g.R.In(() =>
        {
            Assert.True(Dm.truthy(g.Bed.stasis_running()));
            Assert.Equal(1f, g.Bed.last_stasis_sound);
        });
        Assert.Equal(2, Heard(g, Yes));

        // switched off by hand with power on: not running, and the alt-click says it
        g.R.In(() => Waiting.NoSleep(g.Bed.click_alt(g.Bob)));
        g.R.In(() => Assert.False(Dm.truthy(g.Bed.stasis_running())));
        Assert.Equal(2, Heard(g, No));

        // power cut while off: running stays false, nothing is said again
        CutPower(g, true);
        Assert.Equal(2, Heard(g, No));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ProcessChillsAnOccupantAndThawsHimWhenStasisStops()
    {
        var g = Build(8906);
        mob_living_carbon_human pat = g.R.Dummy(BedX + 1, BedY);
        g.R.In(() =>
        {
            Assert.Equal(IDLE_POWER_USE, g.Bed.use_power);
            g.Bed.process(2f); // nobody on the bed: idle (:164)
            Assert.Equal(IDLE_POWER_USE, g.Bed.use_power);

            g.Bed.set_occupant(pat);
            Waiting.NoSleep(pat.adjust_fire_stacks(3));
            pat.ignite_mob();
            Assert.True(Dm.truthy(pat.on_fire));
            g.Bed.process(2f);
            Assert.True(Stasis(pat));
            Assert.True(HAS_TRAIT_FROM(pat, TRAIT_STASIS, TRAIT_STATUS_EFFECT("stasis")));
            Assert.False(Dm.truthy(pat.on_fire)); // chill_out (:136): extinguished
            Assert.Equal(ACTIVE_POWER_USE, g.Bed.use_power);
            Assert.NotNull(pat.has_status_effect(typeof(datum_status_effect_grouped_stasis)));
        });
        Assert.Single(g.R.Sounds.Heard, h => h.File == Spray);
        Assert.InRange(g.R.Sounds.Heard.Single(h => h.File == Spray).Frequency, 24750, 26550);

        g.R.In(() => g.Bed.process(2f)); // already chilled: no second spray, no second effect
        Assert.Single(g.R.Sounds.Heard, h => h.File == Spray);

        CutPower(g, true);
        g.R.In(() =>
        {
            g.Bed.process(2f);
            Assert.False(Stasis(pat)); // thaw_them (:141)
            Assert.Equal(IDLE_POWER_USE, g.Bed.use_power);
        });
        CutPower(g, false);
        g.R.In(() =>
        {
            g.Bed.process(2f);
            Assert.True(Stasis(pat));
            Assert.Equal(ACTIVE_POWER_USE, g.Bed.use_power);
            g.Bed.stasis_enabled = FALSE;
            g.Bed.process(2f);
            Assert.False(Stasis(pat));
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ABedThatIsOffDoesNotChillTheOneBuckledToIt()
    {
        var g = Build(8907);
        mob_living_carbon_human pat = g.R.Dummy(BedX + 1, BedY);
        g.R.In(() =>
        {
            g.Bed.stasis_enabled = FALSE;
            Waiting.NoSleep(g.Bed.post_buckle_mob(pat));
            Assert.Same(pat, g.Bed.occupant);
            Assert.False(Stasis(pat));
            Assert.Equal(IDLE_POWER_USE, g.Bed.use_power);
            g.Bed.stasis_enabled = TRUE;
            g.Bed.process(2f);
            Assert.True(Stasis(pat));
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void BucklingPutsThePatientInStasisAndReadyToOperateAndUnbucklingTakesBoth()
    {
        var g = Build(8908);
        mob_living_carbon_human pat = g.R.Dummy(BedX + 1, BedY);
        g.R.In(() =>
        {
            Waiting.NoSleep(g.Bed.post_buckle_mob(pat)); // :147
            Assert.Same(pat, g.Bed.occupant);
            Assert.True(Stasis(pat));
            Assert.Equal(ACTIVE_POWER_USE, g.Bed.use_power);
            // free_operation: the mob and each of his limbs are ready to be operated on (free_operation.dm:5)
            Assert.NotNull(pat.GetComponent(typeof(datum_component_free_operation)));
            Assert.True(HAS_TRAIT(pat, TRAIT_READY_TO_OPERATE));
            var limbs = Dm.each_cast<obj_item_bodypart>(pat.get_bodyparts(TRUE)).ToList();
            Assert.NotEmpty(limbs);
            Assert.All(limbs, l => Assert.True(HAS_TRAIT(l, TRAIT_READY_TO_OPERATE)));

            Waiting.NoSleep(g.Bed.post_unbuckle_mob(pat)); // :156
            Assert.Null(g.Bed.occupant);
            Assert.False(Stasis(pat));
            Assert.Equal(IDLE_POWER_USE, g.Bed.use_power);
            Assert.Null(pat.GetComponent(typeof(datum_component_free_operation)));
            Assert.False(HAS_TRAIT(pat, TRAIT_READY_TO_OPERATE));
            Assert.All(limbs, l => Assert.False(HAS_TRAIT(l, TRAIT_READY_TO_OPERATE)));
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AnOccupantLeavingTheBedIsThawed()
    {
        var g = Build(8909);
        mob_living_carbon_human pat = g.R.Dummy(BedX + 1, BedY);
        g.R.In(() =>
        {
            Waiting.NoSleep(g.Bed.post_buckle_mob(pat));
            Assert.True(Stasis(pat));
            Waiting.NoSleep(g.Bed.Exited(pat, null)); // :72
            Assert.False(Stasis(pat));
            Assert.Equal(IDLE_POWER_USE, g.Bed.use_power);
            // a mob that is not the occupant leaving does nothing
            Waiting.NoSleep(g.Bed.Exited(g.Bob, null));
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ADeadPatientOnTheBedKeepsHisTimeOfDeath()
    {
        var g = Build(8910);
        mob_living_carbon_human pat = g.R.Dummy(BedX + 1, BedY);
        float died = 0, onBed = 0;
        g.R.In(() =>
        {
            pat.stat = DEAD;
            died = pat.timeofdeath = Dm.num(world.time);
            onBed = Dm.num(world.time);
            Waiting.NoSleep(g.Bed.post_buckle_mob(pat));
            Assert.True(Stasis(pat));
        });
        g.R.Step(5 * Second);
        g.R.In(() =>
        {
            Waiting.NoSleep(g.Bed.post_unbuckle_mob(pat));
            Assert.Equal(died + (Dm.num(world.time) - onBed), pat.timeofdeath, 3);
            Assert.True(pat.timeofdeath > died);
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheBedShowsItsStatesAndLiesTheSleeperByItsFacing()
    {
        var g = Build(8911);
        g.R.In(() =>
        {
            Assert.Equal("stasis", g.Bed.icon_state);
            // the mattress vis overlay: made at alpha 0 by the first update_overlays, fading in as the bed runs (:99)
            Assert.NotNull(g.Bed.mattress_on);
            Assert.Equal("stasis_on", g.Bed.mattress_on.icon_state);
            Assert.True(Dm.@in(g.Bed.mattress_on, g.Bed.vis_contents));
            // buckle_lying: 90 facing south, 270 facing north or east (:81)
            Assert.Equal(90f, g.Bed.buckle_lying);
            Waiting.NoSleep(g.Bed.setDir(EAST));
            Assert.Equal(270f, g.Bed.buckle_lying);
            Waiting.NoSleep(g.Bed.setDir(NORTH));
            Assert.Equal(270f, g.Bed.buckle_lying);
            Waiting.NoSleep(g.Bed.setDir(WEST));
            Assert.Equal(90f, g.Bed.buckle_lying);
            Waiting.NoSleep(g.Bed.setDir(SOUTH));
            Assert.Equal(90f, g.Bed.buckle_lying);

            g.Bed.machine_stat = Dm.bitor(g.Bed.machine_stat, MAINT);
            Waiting.NoSleep(g.Bed.update_icon_state());
            Assert.Equal("stasis_maintenance", g.Bed.icon_state);
            g.Bed.machine_stat = Dm.bitand(g.Bed.machine_stat, Dm.bitnot(MAINT));
            g.Bed.panel_open = TRUE;
            Waiting.NoSleep(g.Bed.update_icon_state());
            Assert.Equal("stasis_maintenance", g.Bed.icon_state);
            g.Bed.panel_open = FALSE;
            Waiting.NoSleep(g.Bed.update_icon_state());
            Assert.Equal("stasis", g.Bed.icon_state);

            Waiting.NoSleep(g.Bed.atom_break(MELEE));
            Assert.Equal("stasis_broken", g.Bed.icon_state);
            Assert.False(Dm.truthy(g.Bed.stasis_running()));
        });
        Assert.Equal(1, Heard(g, No)); // atom_break (:124) says it stopped
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AScrewdriverOpensItsPanelAndACrowbarTakesItApartAndItIsBuiltAgainFromItsBoard()
    {
        var g = Build(8912);
        var r = g.R;
        var bob = g.Bob;
        Assert.Contains(r.In(() => Dm.each(g.Bed.component_parts).ToList()), p => p is datum_stock_part_servo);
        Assert.Contains(r.In(() => Dm.each(g.Bed.component_parts).ToList()), p => p is datum_stock_part_capacitor);
        r.In(() => Assert.Equal(300f, Dm.num(g.Bed.idle_power_usage))); // 300 / ((1 + 1) / 2): tier-1 parts (:31)
        r.In(() => Assert.Equal(3000f, Dm.num(g.Bed.active_power_usage)));
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, g.Bed, Left, 1);
        r.In(() =>
        {
            Assert.True(Dm.truthy(g.Bed.panel_open));
            Assert.Equal("stasis_maintenance", g.Bed.icon_state);
        });
        Hold<obj_item_crowbar>(r, bob);
        Click(r, bob, g.Bed, Left, 1);
        Assert.True(r.In(() => QDELETED(g.Bed)));
        var frame = Assert.Single(On<obj_structure_frame_machine>(r, BedX, BedY));
        Assert.Single(On<obj_item_circuitboard_machine_stasis>(r, BedX, BedY));
        Assert.Single(On<obj_item_stock_parts_servo>(r, BedX, BedY));
        Assert.Single(On<obj_item_stock_parts_capacitor>(r, BedX, BedY));
        Assert.Equal(3f, Amount<obj_item_stack_cable_coil>(r, BedX, BedY));

        foreach (Type part in new[] { typeof(obj_item_circuitboard_machine_stasis), typeof(obj_item_stack_cable_coil),
            typeof(obj_item_stock_parts_servo), typeof(obj_item_stock_parts_capacitor) })
        {
            r.In(() =>
            {
                foreach (object held in Dm.each(bob.held_items.Copy()))
                    if (held is obj_item i)
                        qdel(i);
                Waiting.NoSleep(bob.put_in_active_hand(On<obj_item>(r, BedX, BedY).First(i => i.GetType() == part)));
            });
            Click(r, bob, frame, Left, 1);
        }
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, frame, Left, 1);
        Assert.True(r.In(() => QDELETED(frame)));
        var built = Assert.Single(On<obj_machinery_stasis>(r, BedX, BedY));
        r.In(() =>
        {
            Assert.IsType<obj_item_circuitboard_machine_stasis>(built.circuit_value);
            Assert.Equal(300f, Dm.num(built.idle_power_usage));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void BetterPartsLowerThePowerItDraws()
    {
        var g = Build(8913);
        g.R.In(() =>
        {
            // energy_rating 3 + 3 = 6: 300 / (6 / 2) and 3000 / (6 / 2) (:38-42)
            g.Bed.component_parts = Dm.list(Dm.@new<obj_item_stock_parts_capacitor_adv>(), Dm.@new<obj_item_stock_parts_servo_nano>());
            Waiting.NoSleep(g.Bed.RefreshParts());
            Assert.Equal(100f, Dm.num(g.Bed.idle_power_usage));
            Assert.Equal(1000f, Dm.num(g.Bed.active_power_usage));
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void WhoStandsOnTheBedsTileIsLiftedBySixPixels()
    {
        var g = Build(8914);
        mob_living_carbon_human pat = g.R.Dummy(BedX + 1, BedY);
        g.R.In(() =>
        {
            turf tile = locate(BedX, BedY, 1);
            Assert.True(HAS_TRAIT(tile, TRAIT_ELEVATED_TURF)); // the bed's element (stasis.dm:27) put the core on its turf
            Assert.False(HAS_TRAIT(pat, TRAIT_MOB_ELEVATED));
            Waiting.NoSleep(pat.forceMove(tile));
            Assert.True(HAS_TRAIT(pat, TRAIT_MOB_ELEVATED));
            var z = (DmList)pat.offsets[PIXEL_Z_OFFSET];
            Assert.Equal(6f, Dm.each(z).Sum(k => Dm.num(z[k])));
            Waiting.NoSleep(pat.forceMove(locate(BedX + 1, BedY, 1)));
            Assert.False(HAS_TRAIT(pat, TRAIT_MOB_ELEVATED));
            Assert.Null(pat.offsets);
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheTileIsLevelAgainWhenTheBedIsGone()
    {
        var g = Build(8915);
        g.R.In(() =>
        {
            turf tile = locate(BedX, BedY, 1);
            Assert.True(HAS_TRAIT(tile, TRAIT_ELEVATED_TURF));
            qdel(g.Bed);
        });
        g.R.Step(2);
        g.R.In(() => Assert.False(HAS_TRAIT(locate(BedX, BedY, 1), TRAIT_ELEVATED_TURF)));
        g.R.AssertNoRuntimes();
    }

    // The tests below use the real buckle (buckling.dm, slice A, D218).

    // the sum of a mob's pixel_z offsets (living_update_icons.dm add_offsets): the elevation's and the buckle's
    private static float Lift(mob_living who) => Dm.each(who.offsets?[PIXEL_Z_OFFSET] as DmList).Sum(k => Dm.num(((DmList)who.offsets[PIXEL_Z_OFFSET])[k]));

    [Fact]
    public void ADownedPatientBuckledToThePoweredBedIsChilledAndFreedWhenUnbuckled()
    {
        var g = Build(8916);
        mob_living_carbon_human pat = g.R.Dummy(BedX, BedY);
        g.R.In(() =>
        {
            Waiting.NoSleep(pat.Knockdown(10 * SECONDS));
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Bed.buckle_mob(pat))));
            Assert.Same(g.Bed, pat.buckled);
            Assert.Same(pat, g.Bed.occupant); // post_buckle_mob (stasis.dm:147)
            Assert.True(Stasis(pat));
            Assert.True(Dm.truthy(pat.GetComponent(typeof(datum_component_free_operation))));
        });
        g.R.Step(Second);
        g.R.In(() =>
        {
            Assert.True(Stasis(pat));
            Waiting.NoSleep(g.Bed.unbuckle_mob(pat));
            Assert.Null(pat.buckled);
            Assert.Null(g.Bed.occupant); // post_unbuckle_mob (:156)
            Assert.False(Stasis(pat));
            Assert.False(Dm.truthy(pat.GetComponent(typeof(datum_component_free_operation))));
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ThePatientLiesOnTheBedAt90DegreesFacingSouthAnd270FacingEast()
    {
        var g = Build(8917);
        obj_machinery_stasis east = g.R.In(() =>
        {
            var bed = Dm.@new<obj_machinery_stasis>(locate(BedX + 2, BedY, 1));
            Waiting.NoSleep(bed.setDir(EAST));
            return bed;
        });
        g.R.Step(Second);
        mob_living_carbon_human south = g.R.Dummy(BedX, BedY);
        mob_living_carbon_human other = g.R.Dummy(BedX + 2, BedY);
        g.R.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Bed.buckle_mob(south))));
            Assert.True(Dm.truthy(Waiting.NoSleep(east.buckle_mob(other))));
            Assert.Equal(LYING_DOWN, Dm.num(south.body_position));
            Assert.Equal(90f, south.lying_angle);
            Assert.Equal(SOUTH, Dm.num(south.dir)); // the bed's buckle_dir
            Assert.Equal(270f, other.lying_angle);
            Assert.Equal(SOUTH, Dm.num(other.dir)); // buckle_dir SOUTH whatever the bed faces
            Assert.True(HAS_TRAIT(south, TRAIT_FLOORED));
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void APatientBuckledToABedInStasisIsNotLiftedByTheElevationButOneBuckledToAMobIs()
    {
        var g = Build(8918);
        mob_living_carbon_human pat = g.R.Dummy(BedX + 1, BedY);
        g.R.In(() =>
        {
            // stepped onto the bed's tile he stands 6 pixels up (the elevation element, stasis.dm:28)
            Waiting.NoSleep(pat.Move(locate(BedX, BedY, 1), WEST));
            Assert.Equal(6f, Lift(pat));
            // buckled to the bed (an object) his elevation is cleared (elevation.dm on_set_buckled)
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Bed.buckle_mob(pat))));
            Assert.Equal(0f, Lift(pat));
            Waiting.NoSleep(g.Bed.unbuckle_mob(pat));
            Assert.Equal(6f, Lift(pat));
        });
        mob_living_carbon_human rider = g.R.Dummy(BedX + 1, BedY);
        g.R.In(() =>
        {
            // a mob buckled to a mob on the elevated tile is lifted with it (forced: a human cannot buckle)
            Waiting.NoSleep(rider.Move(locate(BedX, BedY, 1), WEST));
            Assert.Equal(6f, Lift(rider));
            Assert.True(Dm.truthy(Waiting.NoSleep(pat.buckle_mob(rider, force: TRUE, check_loc: FALSE))));
            Assert.Equal(6f, Lift(rider));
        });
        g.R.AssertNoRuntimes();
    }
}
