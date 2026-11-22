using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M4b;

/// <summary>
/// M4b (the owner, 2026-10-15): tg's fire extinguisher (items/tools/extinguisher.dm), its water (effects_water.dm, the
/// water reagent's exposure, the reagent holder's trans_to and expose), the extinguisher cabinet (structures/extinguisher.dm)
/// and the water tank (reagent_dispenser.dm), clicked by a player in a booted toy room with SSair: the safety blocks the
/// spray; a spray costs five units (five puffs of one unit each) and plays extinguish.ogg; the puffs fly to the target's row
/// and put out the hotspots they reach, cooling the air; a burning human in the puffs' way is put out and wetted; in zero
/// gravity the spray pushes the user the other way; an empty extinguisher refills from a water tank with refill.ogg; the
/// cabinet stores and gives back the extinguisher, its door opens and closes with click.ogg and its icon follows; taking the
/// full extinguisher in hand sloshes.
/// </summary>
public class ExtinguisherTests
{
    private const int Second = 20;
    private const string Spray = "sound/effects/extinguish.ogg";
    private const string Refill = "sound/effects/refill.ogg";
    private const string Click = "sound/machines/click.ogg";
    private const string Slosh = "sound/items/handling/reagent_containers/default/default_liquid_slosh";

    // 20 moles of plasma per tile in normal air (FireTests' lean mix): a fire that burns for a while
    private const string PlasmaAir = "o2=22;n2=82;plasma=20;TEMP=293.15";

    private static void LeftClick(AudioRoom r, mob who, atom target, string extra = "")
    {
        r.Command(() => ClientSeam.Click(who.client, target, "left=1;icon-x=16;icon-y=16" + extra));
        r.Step();
    }

    private static (AudioRoom r, mob_living_carbon_human bob, T ext) Holding<T>(ulong seed, int x = 3, int y = 8) where T : obj_item_extinguisher
    {
        var r = new AudioRoom(16, 16, seed: seed);
        mob_living_carbon_human bob = r.Join(x, y, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_GODMODE, "test"));
        r.Step(Second);
        T ext = r.In(() =>
        {
            T e = Dm.@new<T>(bob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(e))), "the extinguisher is in a hand");
            return e;
        });
        r.Step(Second);
        return (r, bob, ext);
    }

    private static float Water(AudioRoom r, atom a) => r.In(() => ((datum_reagents)a.reagents).total_volume);

    private static List<obj_effect_particle_effect_water_extinguisher> Puffs(AudioRoom r) => r.In(() =>
    {
        var found = new List<obj_effect_particle_effect_water_extinguisher>();
        for (int x = 1; x <= 16; x++)
            for (int y = 1; y <= 16; y++)
                found.AddRange(Dm.each_cast<atom>(locate(x, y, 1).contents).OfType<obj_effect_particle_effect_water_extinguisher>());
        return found;
    });

    private static void SafetyOff(AudioRoom r, mob_living_carbon_human bob, obj_item_extinguisher ext)
    {
        r.Command(() => Waiting.NoSleep(ext.attack_self(bob)));
        r.Step();
        Assert.Equal(0f, r.In(() => ext.safety));
    }

    private static void SetAir(AudioRoom r, int x, int y, string mix) => r.In(() =>
    {
        var t = (turf_open)locate(x, y, 1);
        t.air = SSair.parse_gas_string(mix) as datum_gas_mixture;
        SSair.add_to_active(t);
    });

    [Fact]
    public void TheSafetyBlocksTheSprayAndASprayCostsFiveUnitsWithItsSound()
    {
        var (r, bob, ext) = Holding<obj_item_extinguisher>(8101);
        Assert.Equal(100f, Water(r, ext)); // refill(): max_water of /datum/reagent/water
        Assert.Equal("fire_extinguisher0", r.In(() => ext.icon_state));
        Assert.Equal(1f, r.In(() => ext.safety));
        turf target = r.In(() => locate(8, 8, 1));
        r.Sounds.Heard.Clear();

        // safety on: ranged_interact_with_atom returns NONE, nothing is sprayed
        LeftClick(r, bob, target);
        r.Step(Second);
        Assert.Equal(100f, Water(r, ext));
        Assert.Empty(Puffs(r));
        Assert.Empty(r.Sounds.Of(bob, Spray));

        SafetyOff(r, bob, ext);
        Assert.Equal("fire_extinguisher1", r.In(() => ext.icon_state)); // icon_state = "[sprite_name][!safety]"

        long sprayed = r.In(() => (long)r.World.Tick);
        LeftClick(r, bob, target);
        // five puffs from the user's tile, each with one unit of the extinguisher's water in a holder of five
        List<obj_effect_particle_effect_water_extinguisher> puffs = Puffs(r);
        Assert.Equal(5, puffs.Count);
        Assert.All(puffs, p => Assert.Equal(1f, r.In(() => ((datum_reagents)p.reagents).total_volume)));
        Assert.All(puffs, p => Assert.Equal(5f, r.In(() => ((datum_reagents)p.reagents).maximum_volume)));
        Assert.All(puffs, p => Assert.IsType<datum_reagent_water>(r.In(() => ((datum_reagents)p.reagents).reagent_list[1])));
        Assert.Equal(95f, Water(r, ext));
        HeardSound sound = Assert.Single(r.Sounds.Of(bob, Spray)); // playsound(src.loc, 'sound/effects/extinguish.ogg', 75, TRUE, -3)
        Assert.True(sound.Volume > 0);

        // the cooldown: world.time < last_use + 12 (1.2 s)
        LeftClick(r, bob, target);
        Assert.Equal(95f, Water(r, ext));
        r.Step(2 * Second);
        LeftClick(r, bob, target);
        Assert.Equal(90f, Water(r, ext));
        Assert.Equal(2, r.Sounds.Of(bob, Spray).Count);

        // the puffs fly to the target's row and fade (end_life: QDEL_IN the loop's delay); none outlives 7 seconds
        r.Step(8 * Second);
        Assert.Empty(Puffs(r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePuffsPutOutTheHotspotsTheyReachAndCoolTheAir()
    {
        var (r, bob, ext) = Holding<obj_item_extinguisher>(8102);
        for (int y = 7; y <= 9; y++)
            SetAir(r, 7, y, PlasmaAir);
        r.Step();
        for (int y = 7; y <= 9; y++)
            Assert.Contains("is burning", r.In(() => DevCommands.Ignite(locate(7, y, 1))));
        r.Step(Second);
        Assert.True(r.In(() => (int)SSair.hotspots.len) >= 3, "the plasma burns");
        SafetyOff(r, bob, ext);

        // spray the middle of the fire until the puffs have reached each burning tile of the row; a puff on a burning tile
        // removes its hotspot at once and cools the air (other_reagents.dm:149: T / cooling_temperature, at least T0C)
        var putOut = new HashSet<int>();
        var cooled = new Dictionary<int, (float Before, float After)>();
        for (int spray = 0; spray < 12 && putOut.Count < 3; spray++)
        {
            LeftClick(r, bob, r.In(() => locate(7, 8, 1)));
            for (int t = 0; t < 2 * Second; t++)
            {
                var hot = new Dictionary<int, float>();
                for (int y = 7; y <= 9; y++)
                {
                    var turf = r.In(() => (turf_open)locate(7, y, 1));
                    if (r.In(() => turf.active_hotspot) != null)
                        hot[y] = r.In(() => turf.air.temperature);
                }
                r.Step();
                foreach ((int y, float before) in hot)
                {
                    var turf = r.In(() => (turf_open)locate(7, y, 1));
                    bool puffHere = r.In(() => Dm.each_cast<atom>(turf.contents).OfType<obj_effect_particle_effect_water_extinguisher>().Any());
                    if (puffHere && r.In(() => turf.active_hotspot) == null)
                    {
                        putOut.Add(y);
                        cooled.TryAdd(y, (before, r.In(() => turf.air.temperature)));
                    }
                }
            }
        }
        Assert.True(putOut.Count >= 2, "the puffs put out the burning tiles they reached: " + string.Join(",", putOut));
        foreach ((int y, (float before, float after)) in cooled)
            Assert.True(after < before, $"the air at 7,{y} was cooled: {before} -> {after}");

        // keep spraying: the fire is put out for good
        for (int spray = 0; spray < 30 && r.In(() => (int)SSair.hotspots.len) > 0; spray++)
        {
            LeftClick(r, bob, r.In(() => locate(7, 8 + (spray % 3) - 1, 1)));
            r.Step(Second + 5);
        }
        Assert.Equal(0, r.In(() => (int)SSair.hotspots.len));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABurningHumanInTheSprayIsPutOutAndWetted()
    {
        var (r, bob, ext) = Holding<obj_item_extinguisher>(8103);
        mob_living_carbon_human alice = r.Dummy(5, 8);
        r.In(() =>
        {
            ADD_TRAIT(alice, TRAIT_GODMODE, "test");
            Waiting.NoSleep(alice.adjust_fire_stacks(10));
            Assert.True(Dm.truthy(alice.ignite_mob()), "alice catches fire");
        });
        r.Step();
        Assert.True(r.In(() => Dm.truthy(alice.on_fire)));
        SafetyOff(r, bob, ext);

        // the puffs that fly to alice's row cross her tile: water's expose_mob (TOUCH) calls extinguish_mob and adds wet stacks
        for (int spray = 0; spray < 8 && r.In(() => Dm.truthy(alice.on_fire)); spray++)
        {
            LeftClick(r, bob, r.In(() => locate(7, 8, 1)));
            r.Step(Second + 5);
        }
        Assert.False(r.In(() => Dm.truthy(alice.on_fire)), "the spray puts alice out");
        Assert.NotNull(r.In(() => alice.has_status_effect(typeof(datum_status_effect_fire_handler_wet_stacks)))); // and wet: adjust_wet_stacks(reac_volume * 0.5)
        r.AssertNoRuntimes();
    }

    [Fact]
    public void InZeroGravityTheSprayPushesTheUserTheOtherWay()
    {
        var r = new AudioRoom(16, 16, seed: 8104);
        r.In(() =>
        {
            for (int x = 2; x < 16; x++)
                for (int y = 2; y < 16; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_space)));
        });
        mob_living_carbon_human bob = r.Join(8, 8, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_GODMODE, "test"));
        var ext = r.In(() =>
        {
            var e = Dm.@new<obj_item_extinguisher>(bob.loc);
            Waiting.NoSleep(bob.put_in_hands(e));
            return e;
        });
        r.Step(Second);
        Assert.False(r.In(() => Dm.truthy(bob.has_gravity())));
        Assert.Null(r.In(() => bob.drift_handler));
        SafetyOff(r, bob, ext);

        // spraying east: user.newtonian_move(dir2angle(REVERSE_DIR(EAST))), a drift west
        LeftClick(r, bob, r.In(() => locate(12, 8, 1)));
        Assert.NotNull(r.In(() => bob.drift_handler));
        Assert.Equal(270f, r.In(() => Dm.num(bob.drift_handler.drifting_loop.angle)));
        r.Step(3 * Second);
        Assert.True(r.In(() => (int)bob.x) < 8, "bob drifts west: " + r.In(() => bob.x));
        Assert.Equal(8, r.In(() => (int)bob.y));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnEmptyExtinguisherRefillsFromAWaterTank()
    {
        var (r, bob, ext) = Holding<obj_item_extinguisher_empty>(8105, 4, 5);
        Assert.Equal(0f, Water(r, ext)); // starting_water = FALSE: an empty holder of max_water
        Assert.Equal(100f, r.In(() => ((datum_reagents)ext.reagents).maximum_volume));
        obj_structure_reagent_dispensers_watertank tank = r.In(() => Dm.@new<obj_structure_reagent_dispensers_watertank>(locate(5, 5, 1)));
        r.Step();
        Assert.Equal(1000f, Water(r, tank)); // create_reagents(tank_volume, reagent_flags); add_reagent(reagent_id, tank_volume)
        r.Sounds.Heard.Clear();

        // spraying an empty extinguisher: "it's empty!"
        SafetyOff(r, bob, ext);
        LeftClick(r, bob, r.In(() => locate(4, 10, 1)));
        Assert.Empty(Puffs(r));
        Assert.Empty(r.Sounds.Of(bob, Spray));

        // a click on the adjacent tank: attack_atom's AttemptRefill (with the safety off the nozzle sprays the tank first,
        // ranged_interact_with_atom then refilling = TRUE skips the next spray, as in tg)
        r.Command(() => Waiting.NoSleep(ext.attack_self(bob)));
        r.Step();
        Assert.Equal(1f, r.In(() => ext.safety));
        r.Step(Second); // past the click cooldown
        LeftClick(r, bob, tank);
        Assert.Equal(100f, Water(r, ext));
        Assert.Equal(900f, Water(r, tank));
        Assert.True(r.In(() => Dm.truthy(ext.refilling)));
        Assert.Single(r.Sounds.Of(bob, Refill)); // playsound(src.loc, 'sound/effects/refill.ogg', 50, TRUE, -6)
        datum_reagent_water water = r.In(() => (datum_reagent_water)((datum_reagents)ext.reagents).reagent_list[1]);
        Assert.Equal(2f, r.In(() => water.cooling_temperature)); // cooling_power

        // full: "already full!", nothing moves
        r.Step(Second);
        LeftClick(r, bob, tank);
        Assert.Equal(900f, Water(r, tank));
        Assert.Single(r.Sounds.Of(bob, Refill));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCabinetStoresAndGivesBackTheExtinguisherAndItsDoorClicks()
    {
        var (r, bob, ext) = Holding<obj_item_extinguisher>(8106, 4, 5);
        obj_structure_extinguisher_cabinet cabinet = r.In(() => Dm.@new<obj_structure_extinguisher_cabinet>(locate(5, 5, 1)));
        r.Step();
        List<string> Overlays() => r.In(() => Dm.each_cast<object>(cabinet.overlays).Select(o => ((IDmAppearance)o).icon_state).ToList());
        // built (not mapped): open and empty
        Assert.Equal(1f, r.In(() => cabinet.opened));
        Assert.Null(r.In(() => cabinet.stored_extinguisher));
        Assert.Equal("extinguisher", r.In(() => cabinet.icon_state));
        Assert.Contains("door_open", Overlays());
        r.Sounds.Heard.Clear();

        // the extinguisher in hand goes in (item_interaction: transferItemToLoc)
        LeftClick(r, bob, cabinet);
        Assert.Same(cabinet, r.In(() => ext.loc));
        Assert.Same(ext, r.In(() => cabinet.stored_extinguisher));
        Assert.Equal("extinguisher_default", r.In(() => cabinet.icon_state));
        Assert.Null(r.In(() => bob.get_active_held_item()));

        // a right click closes the door with its click
        r.Step(Second); // past the click cooldown
        r.Command(() => ClientSeam.Click(bob.client, cabinet, "right=1;icon-x=16;icon-y=16"));
        r.Step(Second);
        Assert.Equal(0f, r.In(() => cabinet.opened));
        Assert.Contains("door", Overlays());
        Assert.DoesNotContain("door_open", Overlays());
        Assert.Single(r.Sounds.Of(bob, Click));

        // an empty hand takes the extinguisher out of the closed cabinet, which opens with its click (attack_hand)
        r.Step(Second);
        LeftClick(r, bob, cabinet);
        Assert.Same(ext, r.In(() => bob.get_active_held_item()));
        Assert.Null(r.In(() => cabinet.stored_extinguisher)); // Exited
        Assert.Equal(1f, r.In(() => cabinet.opened));
        Assert.Equal("extinguisher", r.In(() => cabinet.icon_state));
        Assert.Contains("door_open", Overlays());
        Assert.Equal(2, r.Sounds.Of(bob, Click).Count);
        // taking the full extinguisher in hand sloshes (equipped: SFX_DEFAULT_LIQUID_SLOSH)
        Assert.Contains(r.Sounds.Heard, h => ReferenceEquals(h.Target, bob) && h.File != null && h.File.StartsWith(Slosh, StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EveryTypeOfTheExtinguisherPathIsCreatedAndDestroyed()
    {
        var r = new AudioRoom(9, 9, seed: 8107);
        r.Step(Second);
        var types = new List<Type>();
        foreach (Type root in new[] { typeof(obj_item_extinguisher), typeof(obj_effect_particle_effect_water), typeof(obj_effect_particle_effect_steam),
            typeof(obj_structure_extinguisher_cabinet), typeof(obj_structure_reagent_dispensers) })
            types.AddRange(r.In(() => Dm.each_cast<Type>(typesof(root)).Where(t => !InertSeam.IsInertType(t)).ToList()));
        Assert.Contains(typeof(obj_structure_reagent_dispensers_watertank), types);
        foreach (Type type in types)
        {
            var made = r.In(() => (atom)Dm.@new(type, locate(4, 4, 1)));
            r.Step();
            r.In(() => qdel(made));
            r.Step();
            r.AssertNoRuntimes();
        }
    }
}
