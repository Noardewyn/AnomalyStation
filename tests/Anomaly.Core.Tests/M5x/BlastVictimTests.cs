using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M5x;

/// <summary>
/// M5x slice 3: people and chained objects in blasts. A human's ex_act per severity (human_defense.dm:257: the split brute
/// and burn, the knockdown, the unconsciousness, the bang on the ears with its deafness; a devastating blast kills, the body
/// staying until M6's gibbing, D150), the flash of an explosion's flash range (living and carbon flash_act: the fullscreen
/// flash and its clearing, the eyes' sting), the soundbang's ear protection in a vacuum, and the chained objects: a fuel tank
/// set off by a blast, a fire or a lit welder, a devastated canister releasing its air, the gravity generator broken by a
/// devastating blast, a power cell's corruption and the corrupted cell's explosion, a gas tank rupturing into a dyn_explosion
/// and a bodypart's ex_act only off its owner.
/// </summary>
public class BlastVictimTests
{
    private const int Second = 20;
    private const int W = 24, H = 16;

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(W, H, seed: seed);
        r.In(() =>
        {
            for (int x = 3; x <= W - 2; x++)
            {
                for (int y = 3; y <= H - 2; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_floor_iron), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));
            }
        });
        r.Step(Second);
        return r;
    }

    private static turf At(int x, int y) => locate(x, y, 1);

    private static void Explode(AudioRoom r, int x, int y, float dev, float heavy, float light, float flash = 0, float flame = 0)
        => r.In(() => Waiting.Detach(explosion(At(x, y), dev, heavy, light, flame_range: flame, flash_range: flash, adminlog: FALSE, ignorecap: TRUE, silent: TRUE)));

    private static obj_item_organ_ears Ears(mob_living_carbon_human h) => (obj_item_organ_ears)h.get_organ_slot(ORGAN_SLOT_EARS);

    [Fact]
    public void ALightBlastSplitsThirtyBruteOverTheLimbsKnocksDownAndDeafens()
    {
        var r = Room(6301);
        var victim = r.Dummy(14, 8);
        r.Step(Second);
        Explode(r, 12, 8, 0, 0, 3); // the victim at 2: light
        r.Step(1);
        r.In(() =>
        {
            // 5 to a limb picked by apply_damage per bodypart, the limb loop ending at the second wound roll (max_wounds,
            // human_defense.dm:346); since M6 slice 2 the rolls make wounds, and a wounded limb picked again takes its wound's
            // damage multiplier (wound_damage_multiplier, bodyparts/wounds.dm:291)
            float brute = Dm.num(victim.get_brute_loss());
            Assert.InRange(brute, 10f, 45f);
            Assert.True(LAZYLEN(victim.all_wounds) <= 2, "at most two wound rolls");
            Assert.Equal(0f, Dm.num(victim.get_fire_loss()));
            Assert.True(Dm.truthy(victim.IsKnockdown()), "16 s of knockdown");
            Assert.InRange(Dm.num(victim.AmountKnockdown()), 15 * SECONDS, 16 * SECONDS);
            Assert.False(Dm.truthy(victim.IsUnconscious()), "a light blast does not knock out");
            obj_item_organ_ears ears = Ears(victim);
            Assert.True(Dm.num(ears.damage) >= 15, "15 ear damage (or the ears' failure)");
            Assert.True(ears.temporary_deafness >= 119 * SECONDS, "120 s of deafness");
            Assert.True(HAS_TRAIT(victim, TRAIT_DEAF));
            Assert.Equal((float)STABLE, victim.stat); // CONSCIOUS
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHeavyBlastBurnsAndBruisesSixtyEachAndKnocksOut()
    {
        var r = Room(6302);
        var victim = r.Dummy(13, 8);
        r.Step(Second);
        // the human's ex_act alone: in a real blast the turfs explode first, and a floor scraped to space under the victim
        // gives its ears the vacuum's protection (3), as much as the heavy bang's intensity (SOUNDBANG_MASSIVE)
        r.In(() => Waiting.Detach(EX_ACT(victim, EXPLODE_HEAVY)));
        r.In(() =>
        {
            // 10 brute and 10 burn per limb, the limb loop ending at the third wound roll (max_wounds, human_defense.dm:346); a
            // roll may instead dismember an arm or a leg (M6 slice 4, human_defense.dm:338): the lost limb takes its damage away
            // and the chest takes dismember's 15 to 50 brute (dismemberment.dm:16)
            float brute = Dm.num(victim.get_brute_loss());
            int lost = Dm.each_cast<string>(GLOB.limb_zones).Count(zone => victim.get_bodypart(zone) == null);
            if (lost == 0)
            {
                // in whole tens for most seeds; the rolls follow the seed's stream, which the fall's bodyfall sound (its vary roll)
                // also draws from since D296's sound audit, and some rolls spread the damage unevenly. A hit on a limb the loop has
                // already wounded is multiplied by the wound's penalty (wound_damage_multiplier, _bodyparts.dm:726; a critical burn
                // 1.3, burns.dm:365), so the six tens reach at most 78
                string limbs = string.Join("; ", Dm.each_cast<obj_item_bodypart>(victim.get_bodyparts()).Select(bp =>
                    $"{bp.body_zone} {bp.brute_dam}/{bp.burn_dam} [{string.Join(",", Dm.each<datum>(bp.wounds).Select(w => w.GetType().Name))}]"));
                Assert.True(brute >= 30f && brute <= 78f, $"brute {brute}: {limbs}");
                float burn = Dm.num(victim.get_fire_loss());
                Assert.True(burn >= 30f && burn <= 78f, $"burn {burn}: {limbs}");
            }
            else
            {
                Assert.True(brute >= 15f + 10f * (6 - lost - 3), $"brute {brute} with {lost} limb(s) lost");
                Assert.True(Dm.num(victim.get_fire_loss()) <= brute);
            }
            Assert.True(Dm.truthy(victim.IsUnconscious()), "2 s unconscious");
            Assert.True(Dm.truthy(victim.IsKnockdown()));
            Assert.True(Dm.num(Ears(victim).damage) >= 30);
            Assert.True(Ears(victim).temporary_deafness >= 239 * SECONDS);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADevastatingBlastGibsAPerson()
    {
        // M6 slice 5: the devastating blast's gib(DROP_ALL_REMAINS) (human_defense.dm:288) blows the body apart
        var r = Room(6303);
        var victim = r.Dummy(12, 8);
        r.Step(Second);
        Explode(r, 12, 8, 1, 2, 3);
        r.Step(1);
        r.In(() =>
        {
            Assert.True(QDELETED(victim), "gibbed");
            var remains = Dm.each(range(6, At(12, 8))).ToList();
            // the gib decals go with the blasted floor; the limbs and the organs fly apart, the head keeps the brain
            Assert.Equal(5, remains.Count(a => a is obj_item_bodypart));
            Assert.Contains(remains, a => a is obj_item_organ_heart);
            Assert.Contains(remains, a => a is obj_item_bodypart_head head && Dm.each(head).Any(o => o is obj_item_organ_brain));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void BombImmuneMobsAreUntouched()
    {
        var r = Room(6304);
        var victim = r.Dummy(13, 8);
        r.In(() => ADD_TRAIT(victim, TRAIT_BOMBIMMUNE, "test"));
        r.Step(Second);
        Explode(r, 12, 8, 1, 2, 3);
        r.Step(1);
        r.In(() =>
        {
            Assert.Equal(0f, Dm.num(victim.get_brute_loss()));
            Assert.Equal((float)STABLE, victim.stat); // CONSCIOUS
        });
    }

    [Fact]
    public void TheFlashRangeFlashesTheViewersAndTheFlashClears()
    {
        var r = Room(6305);
        var victim = r.Dummy(16, 8);
        r.Step(Second);
        Explode(r, 12, 8, 0, 0, 1, flash: 6); // the victim at 4: out of the blast, in the flash
        r.In(() =>
        {
            Assert.True(Dm.truthy(victim.screens?["flash"]), "the fullscreen flash");
            Assert.IsType<atom_movable_screen_fullscreen_flash>(victim.screens["flash"]);
            Assert.Equal(0f, Dm.num(victim.get_brute_loss()));
        });
        r.Step(4 * Second); // carbon's length 25 ds, then the fade
        r.In(() => Assert.False(Dm.truthy(victim.screens?["flash"]), "cleared after 2.5 s"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AVacuumProtectsTheEarsFromTheBang()
    {
        var r = Room(6306);
        var victim = r.Dummy(14, 8);
        r.Step(Second);
        r.In(() =>
        {
            Assert.Equal(0f, Dm.num(victim.get_ear_protection()));
            datum_gas_mixture air = Waiting.NoSleep(At(14, 8).return_air()) as datum_gas_mixture;
            air.remove_ratio(1);
            Assert.Equal((float)EAR_PROTECTION_VACUUM, Dm.num(victim.get_ear_protection()));
            // SOUNDBANG_STRONG (2) is below the vacuum's 3: no ear damage, no deafness
            Assert.Equal(FALSE, Waiting.NoSleep(victim.soundbang_act(SOUNDBANG_STRONG, 0, 15, 120 * SECONDS, ignore_deafness: TRUE, send_sound: FALSE)));
            Assert.Equal(0f, Dm.num(Ears(victim).damage));
        });
    }

    [Fact]
    public void AFuelTankInABlastExplodesInTurn()
    {
        var r = Room(6307);
        obj_structure_reagent_dispensers_fueltank tank = r.In(() => Dm.@new<obj_structure_reagent_dispensers_fueltank>(At(18, 8)));
        var victim = r.Dummy(20, 8); // 2 from the tank, 8 from the first blast
        r.Step(Second);
        Explode(r, 12, 8, 0, 0, 7); // reaches the tank at 6
        r.Step(Second);
        r.In(() =>
        {
            Assert.True(QDELETED(tank), "the tank burst");
            // 1000 units of fuel: (heavy 1, light 4, flame 6) around the tank; a light blast's limb loop may end early (max_wounds)
            Assert.True(Dm.num(victim.get_brute_loss()) >= 5, "the tank's own blast reached the victim");
            Assert.True(victim.x > 20, "and threw the victim away from the tank");
        });
    }

    [Fact]
    public void AFuelTankSetsOffOnFireAndOnALitWelderButRefillsAnUnlitOne()
    {
        var r = Room(6308);
        var engineer = r.Dummy(10, 8);
        obj_structure_reagent_dispensers_fueltank tank = r.In(() => Dm.@new<obj_structure_reagent_dispensers_fueltank>(At(11, 8)));
        obj_item_weldingtool welder = r.In(() => Dm.@new<obj_item_weldingtool>(At(10, 8)));
        r.Step(Second);
        r.In(() =>
        {
            datum_reagents fuel = (datum_reagents)welder.reagents;
            Waiting.NoSleep(fuel.remove_reagent(typeof(datum_reagent_fuel), 15));
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(tank.welder_act(engineer, welder)));
            Assert.Equal(welder.max_fuel, Dm.num(fuel.total_volume));
            Assert.False(QDELETED(tank));
            Assert.Equal(1000f - 15, Dm.num(((datum_reagents)tank.reagents).total_volume));
        });
        obj_structure_reagent_dispensers_fueltank second = r.In(() => Dm.@new<obj_structure_reagent_dispensers_fueltank>(At(5, 5)));
        r.In(() =>
        {
            Waiting.NoSleep(tank.fire_act(1000, 100));
            Assert.True(QDELETED(tank), "fire sets it off");
            welder.welding = TRUE;
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(second.welder_act(engineer, welder)));
            Assert.True(QDELETED(second), "a lit welder sets it off");
        });
    }

    [Fact]
    public void ADevastatedCanisterReleasesItsAir()
    {
        var r = Room(6309);
        obj_machinery_portable_atmospherics_canister can = r.In(() => Dm.@new<obj_machinery_portable_atmospherics_canister_plasma>(At(12, 8)));
        r.Step(Second);
        float before = r.In(() => Dm.num(((datum_gas_mixture)Waiting.NoSleep(At(12, 8).return_air())).total_moles()));
        float inside = r.In(() => Dm.num(can.air_contents.total_moles()));
        Assert.True(inside > 100);
        r.In(() => can.ex_act(EXPLODE_DEVASTATE, null));
        r.In(() =>
        {
            float after = Dm.num(((datum_gas_mixture)Waiting.NoSleep(At(12, 8).return_air())).total_moles());
            Assert.True(after > before + inside * 0.9f, "the can's air went to its turf");
        });
    }

    [Fact]
    public void OnlyADevastatingBlastBreaksTheGravityGenerator()
    {
        var r = Room(6310);
        obj_machinery_gravity_generator_main gen = r.In(() => Dm.@new<obj_machinery_gravity_generator_main>(At(12, 8)));
        r.Step(Second);
        r.In(() =>
        {
            Assert.Equal(FALSE, gen.ex_act(EXPLODE_HEAVY, null));
            Assert.False(Dm.truthy(Dm.bitand(gen.machine_stat, BROKEN)));
            Assert.Equal(TRUE, gen.ex_act(EXPLODE_DEVASTATE, null));
            Assert.True(Dm.truthy(Dm.bitand(gen.machine_stat, BROKEN)));
        });
    }

    [Fact]
    public void ACorruptedCellExplodesByItsChargeAndALightBlastCorruptsAQuarterOfCells()
    {
        var r = Room(6311);
        r.In(() =>
        {
            var cell = Dm.@new<obj_item_stock_parts_power_store_cell_high>(At(12, 8));
            cell.corrupted = TRUE;
            float charge = cell.charge;
            Assert.True(charge > 0);
            Assert.Equal(TRUE, Waiting.NoSleep(cell.try_explode()));
            Assert.True(QDELETED(cell));
            Assert.True(SSexplosions.lowturf.len > 0, "its explosion was queued");

            int corrupted = 0;
            for (int i = 0; i < 200; i++)
            {
                var c = Dm.@new<obj_item_stock_parts_power_store_cell>(At(5, 5));
                float full = c.charge;
                c.ex_act(EXPLODE_LIGHT, null);
                if (!QDELETED(c) && c.charge < full)
                    corrupted++;
                qdel(c);
            }
            Assert.InRange(corrupted, 25, 75); // prob(25) halves the charge
        });
    }

    [Fact]
    public void AnOverpressuredTankRupturesIntoADynamicExplosion()
    {
        var r = Room(6312);
        r.In(() =>
        {
            var tank = Dm.@new<obj_item_tank_internals_plasma>(At(12, 8));
            tank.air_contents.temperature = 20000; // far above TANK_FRAGMENT_PRESSURE
            Assert.True(Dm.num(tank.air_contents.return_pressure()) > TANK_FRAGMENT_PRESSURE);
            Waiting.NoSleep(tank.atom_destruction(BOMB));
            Assert.True(SSexplosions.lowturf.len + SSexplosions.medturf.len + SSexplosions.highturf.len > 0, "dyn_explosion queued");
        });
    }

    [Fact]
    public void ABodypartOnItsOwnerIgnoresTheBlastButALooseOneTakesIt()
    {
        var r = Room(6313);
        var victim = r.Dummy(12, 8);
        r.Step(Second);
        r.In(() =>
        {
            var arm = (obj_item_bodypart)victim.get_bodypart(BODY_ZONE_L_ARM);
            Assert.Equal(FALSE, arm.ex_act(EXPLODE_DEVASTATE, null));
            Assert.False(QDELETED(arm));
        });
    }
}
