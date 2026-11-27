using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Science's anomaly cores in toy rooms (research/anomaly/, effects/anomalies/_anomalies.dm and the items that take a core): the
/// anomaly refinery implodes a raw core with a transfer valve into its finished core (its window shows the core, the radius it needs
/// and the bomb) and counts it towards the core limit, and gives a raw core back when the blast is too weak; an anomaly keeps a core
/// that a signal on its frequency and code neutralizes, and drops it; a signalled core does its own thing (the gravitational one
/// pulls, the pyroclastic one lights the tiles around it); the reactive armour shell takes a core and becomes that core's armour,
/// which switches on and off and burns the people around its wearer when it is hit; the polymorph belt and the perceptomatrix helm
/// wake up with their cores.
/// </summary>
public class AnomalyCoreTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed, int width = 13, int height = 9)
    {
        var r = new AudioRoom(width, height, seed: seed);
        r.In(() => (get_area(locate(4, 4, 1)) as area).requires_power = FALSE); // the toy room has no APC
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who, T item) where T : obj_item
    {
        r.In(() =>
        {
            foreach (obj_item held in Dm.each<obj_item>(who.held_items).ToList())
                Waiting.NoSleep(who.dropItemToGround(held));
            Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_active_hand(item))));
        });
        r.Step(2);
        return item;
    }

    private static void EmptyHands(AudioRoom r, mob_living_carbon_human who)
    {
        r.In(() =>
        {
            foreach (obj_item held in Dm.each<obj_item>(who.held_items).ToList())
                Waiting.NoSleep(who.dropItemToGround(held));
        });
        r.Step(2);
    }

    private static JsonElement Open(AudioRoom r, mob_living_carbon_human who, atom machine, out string windowId)
    {
        EmptyHands(r, who);
        Take(who);
        r.Step(Second);
        Click(r, who, machine);
        r.Step(4);
        return LastFullUpdate(Take(who), out windowId);
    }

    private static void Fill(datum_gas_mixture mix, Type gas, float moles, float temperature)
    {
        mix.assert_gases(gas);
        mix.moles[gas] = moles;
        mix.temperature = temperature;
    }

    /// <summary>A transfer valve with a hot plasma tank and an oxygen tank, or a weak one.</summary>
    private static obj_item_transfer_valve Bomb(AudioRoom r, turf where, bool weak = false)
    {
        return r.In(() =>
        {
            var valve = Dm.@new<obj_item_transfer_valve>(where);
            var one = Dm.@new<obj_item_tank_internals_plasma>(valve);
            var two = Dm.@new<obj_item_tank_internals_oxygen>(valve);
            // below the tanks' leak pressure until the valve mixes them (an overfilled tank bursts on its own in the refinery)
            Fill(Waiting.NoSleep(one.return_air()) as datum_gas_mixture, typeof(datum_gas_plasma), weak ? 1 : 15, weak ? T20C : 1500);
            Fill(Waiting.NoSleep(two.return_air()) as datum_gas_mixture, typeof(datum_gas_oxygen), weak ? 1 : 60, T20C);
            valve.tank_one = one;
            valve.tank_two = two;
            valve.update_appearance();
            return valve;
        });
    }

    [Fact]
    public void TheRefineryImplodesARawCoreIntoItsFinishedCore()
    {
        var (r, feed, bob) = Room(15001);
        var refinery = r.In(() => Dm.@new<obj_machinery_research_anomaly_refinery>(locate(5, 4, 1)));
        r.Step(2);
        Assert.Equal("explosive_compressor", r.In(() => Dm.str(refinery.icon_state)));
        float madeBefore = r.In(() => Dm.num(SSresearch.created_anomaly_types[typeof(obj_item_assembly_signaler_anomaly_flux)]));

        // the raw core and the bomb go in
        var raw = Hold(r, bob, r.In(() => Dm.@new<obj_item_raw_anomaly_core_flux>(bob.loc)));
        Click(r, bob, refinery);
        Assert.Same(raw, r.In(() => refinery.inserted_core));
        Assert.True(feed.Any("You insert"));
        var valve = Hold(r, bob, Bomb(r, r.In(() => bob.loc as turf)));
        Click(r, bob, refinery);
        Assert.Same(valve, r.In(() => refinery.inserted_bomb));
        Assert.Same(r.In(() => valve.tank_two), r.In(() => refinery.tank_to_target));

        // the window: the core, the radius it needs (four tiles for the first core of a kind), the bomb's tanks
        JsonElement payload = Open(r, bob, refinery, out string window);
        Assert.Equal("AnomalyRefinery", InterfaceOf(payload));
        JsonElement data = payload.GetProperty("data");
        Assert.Equal("raw flux core", data.GetProperty("core").GetString());
        Assert.Equal(4, data.GetProperty("requiredRadius").GetInt32());
        Assert.Equal(1, data.GetProperty("valvePresent").GetInt32());
        Assert.Equal(1, data.GetProperty("valveReady").GetInt32());
        Assert.Equal(3, data.GetProperty("gasList").GetArrayLength());

        // the implosion: the valve opens inside, the blast is caught, and the finished core comes out with the empty valve
        Act(r, bob, window, "start_implosion");
        Assert.True(feed.Any("Beginning compression test. Opening transfer valve."));
        obj_item_assembly_signaler_anomaly_flux core = null;
        for (int i = 0; i < 30 * Second && core == null; i++)
        {
            r.Step(1);
            core = r.In(() => Dm.each<obj_item_assembly_signaler_anomaly_flux>(locate(5, 4, 1)).FirstOrDefault());
        }
        Assert.NotNull(core);
        Assert.True(feed.Any("Core production complete."), "the refinery did not state the success");
        Assert.True(r.In(() => QDELETED(raw)));
        Assert.False(r.In(() => Dm.truthy(refinery.active)));
        Assert.Null(r.In(() => refinery.inserted_core));
        Assert.Null(r.In(() => refinery.inserted_bomb));
        Assert.Equal(madeBefore + 1, r.In(() => Dm.num(SSresearch.created_anomaly_types[typeof(obj_item_assembly_signaler_anomaly_flux)])));
        // the next flux core needs a wider blast
        Assert.True(r.In(() => Dm.num(refinery.get_required_radius(typeof(obj_item_assembly_signaler_anomaly_flux)))) > 4);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWeakBlastGivesTheRawCoreBack()
    {
        var (r, feed, bob) = Room(15002);
        var refinery = r.In(() => Dm.@new<obj_machinery_research_anomaly_refinery>(locate(5, 4, 1)));
        r.Step(2);
        var raw = Hold(r, bob, r.In(() => Dm.@new<obj_item_raw_anomaly_core_grav>(bob.loc)));
        Click(r, bob, refinery);
        Hold(r, bob, Bomb(r, r.In(() => bob.loc as turf), weak: true));
        Click(r, bob, refinery);
        // a second core is refused while one is in
        Hold(r, bob, r.In(() => Dm.@new<obj_item_raw_anomaly_core_pyro>(bob.loc)));
        Click(r, bob, refinery);
        Assert.True(feed.Any("There is already a core in"));

        r.In(() => Waiting.NoSleep(refinery.start_test()));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(refinery.active)));
        // nothing goes off: the test times out and ejects both
        r.Step(15 * Second);
        Assert.True(feed.Any("Transfer valve resulted in negligible explosive power. Items ejected."));
        Assert.Same(r.In(() => locate(5, 4, 1)), r.In(() => raw.loc));
        Assert.False(r.In(() => Dm.truthy(refinery.active)));
        Assert.Empty(r.In(() => Dm.each<obj_item_assembly_signaler_anomaly_grav>().ToList()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnAnomalyDropsItsCoreWhenItsSignalNeutralizesIt()
    {
        var (r, feed, bob) = Room(15003);
        var flux = r.In(() =>
        {
            var a = Dm.@new<obj_effect_anomaly_flux>(locate(7, 4, 1));
            a.move_chance = 0;
            return a;
        });
        r.Step(2);
        var core = r.In(() => flux.anomaly_core_value);
        Assert.NotNull(core);
        Assert.IsType<obj_item_assembly_signaler_anomaly_flux>(core);
        Assert.Same(flux, r.In(() => core.loc));
        Assert.Equal(typeof(obj_effect_anomaly_flux), r.In(() => core.anomaly_type));
        Assert.InRange(r.In(() => Dm.num(core.code)), 1, 100);

        // the analyzer reads its frequency and code
        var analyzer = Hold(r, bob, r.In(() => Dm.@new<obj_item_analyzer>(bob.loc)));
        r.In(() => flux.analyzer_act(bob, analyzer));
        Assert.True(feed.Any("unstable field is fluctuating along frequency"));

        // a signaler set to them neutralizes the anomaly: the core drops where it was
        var signaler = r.In(() =>
        {
            var s = Dm.@new<obj_item_assembly_signaler>(bob.loc);
            s.set_frequency(core.frequency);
            s.code = core.code;
            return s;
        });
        r.In(() => Waiting.NoSleep(signaler.signal()));
        r.Step(Second);
        Assert.True(r.In(() => QDELETED(flux)));
        Assert.False(r.In(() => QDELETED(core)));
        Assert.Same(r.In(() => locate(7, 4, 1)), r.In(() => core.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASignalledCoreDoesItsOwnThing()
    {
        var (r, _, bob) = Room(15004);
        // the gravitational core pulls what is loose within two tiles
        var grav = r.In(() => Dm.@new<obj_item_assembly_signaler_anomaly_grav>(locate(8, 4, 1)));
        var crayon = r.In(() => Dm.@new<obj_item_toy_crayon_red>(locate(10, 4, 1)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(grav.signal()));
        r.Step(2);
        Assert.Equal(9, r.In(() => (int)crayon.x));

        // the pyroclastic core lights the tiles around it
        var pyro = r.In(() => Dm.@new<obj_item_assembly_signaler_anomaly_pyro>(locate(8, 6, 1)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(pyro.signal()));
        Assert.True(r.In(() => Dm.each<obj_effect_hotspot>(locate(9, 7, 1)).Any()), "no fire next to the pyroclastic core");
        r.Step(Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheShellTakesACoreAndBecomesItsReactiveArmour()
    {
        var (r, feed, bob) = Room(15005);
        var shell = r.In(() => Dm.@new<obj_item_reactive_armor_shell>(locate(5, 4, 1)));
        var core = Hold(r, bob, r.In(() => Dm.@new<obj_item_assembly_signaler_anomaly_flux>(bob.loc)));
        Click(r, bob, shell);
        Assert.True(feed.Any("the armour gently hums to life"));
        Assert.True(r.In(() => QDELETED(shell)));
        Assert.True(r.In(() => QDELETED(core)));
        Assert.Single(r.In(() => Dm.each<obj_item_clothing_suit_armor_reactive_tesla>(locate(5, 4, 1)).ToList()));

        // a core without an armour of its own makes the stealth armour
        shell = r.In(() => Dm.@new<obj_item_reactive_armor_shell>(locate(5, 4, 1)));
        Hold(r, bob, r.In(() => Dm.@new<obj_item_assembly_signaler_anomaly_pyro>(bob.loc)));
        Click(r, bob, shell);
        Assert.Single(r.In(() => Dm.each<obj_item_clothing_suit_armor_reactive_stealth>(locate(5, 4, 1)).ToList()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheIncendiaryArmourSwitchesOnAndBurnsThePeopleAroundItsWearer()
    {
        var (r, feed, bob) = Room(15006);
        var armour = r.In(() => Dm.@new<obj_item_clothing_suit_armor_reactive_fire>(bob.loc));
        r.Step(2);
        Assert.Equal("reactiveoff", r.In(() => Dm.str(armour.icon_state)));
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(armour, ITEM_SLOT_OCLOTHING)))));
        r.Step(2);
        r.In(() => Waiting.NoSleep(armour.attack_self(bob)));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(armour.active)));
        Assert.Equal("reactive", r.In(() => Dm.str(armour.icon_state)));
        Assert.True(feed.Any("is now active"));

        var victim = r.In(() => Dm.@new<mob_living_carbon_human>(locate(6, 4, 1)));
        r.Step(2);
        r.In(() => armour.hit_reaction_chance = 100);
        object blocked = r.In(() => Waiting.NoSleep(armour.hit_reaction(bob, null, "the punch", 0, 10, MELEE_ATTACK)));
        Assert.True(Dm.truthy(blocked));
        Assert.True(r.In(() => Dm.num(victim.fire_stacks)) > 0, "the one beside the wearer is not burning");
        Assert.True(feed.Any("blocks the punch, sending out jets of flame!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBeltAndTheHelmWakeUpWithTheirCores()
    {
        var (r, feed, bob) = Room(15007);
        // the polymorph belt: a bioscrambler core switches it on
        var belt = r.In(() => Dm.@new<obj_item_polymorph_belt>(locate(5, 4, 1)));
        r.Step(2);
        Assert.Equal("polybelt_inactive", r.In(() => Dm.str(belt.icon_state)));
        var core = Hold(r, bob, r.In(() => Dm.@new<obj_item_assembly_signaler_anomaly_bioscrambler>(bob.loc)));
        Click(r, bob, belt);
        r.Step(4 * Second);
        Assert.True(r.In(() => Dm.truthy(belt.active)));
        Assert.Equal("polybelt", r.In(() => Dm.str(belt.icon_state)));
        Assert.True(r.In(() => QDELETED(core)));

        // it scans a simple animal and then gives its holder that animal's shape
        var roach = r.In(() => Dm.@new<mob_living_basic_cockroach>(locate(5, 5, 1)));
        r.Step(2);
        r.In(() => Waiting.Detach(belt.attack__proc(roach, bob)));
        r.Step(6 * Second);
        Assert.Equal(typeof(mob_living_basic_cockroach), r.In(() => belt.stored_mob_type));
        Assert.NotNull(r.In(() => belt.transform_action));
        Assert.Equal(typeof(mob_living_basic_cockroach), r.In(() => belt.transform_action.shapeshift_type));
        Assert.Equal("Assume your cockroach form!", r.In(() => Dm.str(belt.transform_action.desc)));

        // the perceptomatrix helm: blind without its core, clear with it
        var helm = r.In(() => Dm.@new<obj_item_clothing_head_helmet_perceptomatrix>(locate(5, 4, 1)));
        r.Step(2);
        Assert.Equal("perceptomatrix_helmet_inactive", r.In(() => Dm.str(helm.icon_state)));
        Assert.True(r.In(() => Dm.num(helm.tint)) > 1000);
        Hold(r, bob, r.In(() => Dm.@new<obj_item_assembly_signaler_anomaly_hallucination>(bob.loc)));
        Click(r, bob, helm);
        r.Step(4 * Second);
        Assert.True(r.In(() => Dm.truthy(helm.core_installed)));
        Assert.Equal(0f, r.In(() => Dm.num(helm.tint)));
        Assert.Equal("perceptomatrix_helmet", r.In(() => Dm.str(helm.icon_state)));
        Assert.Equal(2, r.In(() => Dm.length(helm.active_components)));
        Assert.True(r.In(() => HAS_TRAIT(helm, TRAIT_CASTING_CLOTHING)));
        r.AssertNoRuntimes();
    }

    private static readonly string[] NewlyLive =
    {
        "/obj/machinery/research/anomaly_refinery", "/obj/item/raw_anomaly_core", "/obj/item/assembly/signaler/anomaly", "/obj/machinery/gizmo",
        "/obj/item/gizmo", "/datum/gizmo_controller", "/datum/gizmo_interface", "/datum/gizmo_puzzle", "/datum/gizmodes", "/datum/gizpulse",
        "/datum/component/gizmo_voice", "/datum/wires/gizmo", "/datum/element/moving_randomly", "/obj/machinery/quantumpad",
        "/obj/machinery/computer/launchpad", "/obj/machinery/mechpad", "/obj/machinery/computer/mechpad", "/obj/machinery/ecto_sniffer",
        "/datum/wires/ecto_sniffer", "/obj/item/circuit_component/quantumpad", "/obj/item/circuit_component/bluespace_launchpad/console",
        "/obj/item/reactive_armor_shell", "/obj/item/clothing/suit/armor/reactive", "/datum/armour_dimensional_theme",
        "/obj/item/polymorph_belt", "/datum/action/cooldown/spell/shapeshift/polymorph_belt",
        "/datum/status_effect/shapechange_mob/from_spell/polymorph_belt", "/obj/item/clothing/head/helmet/perceptomatrix",
        "/datum/action/cooldown/spell/pointed/percept_hallucination",
    };

    // the kinds that stay inert with a row: the fugitive hunters' psyker armour (its antagonist, D333)
    private static readonly string[] LeftInert = { "/obj/item/clothing/suit/armor/reactive/psykerboost" };

    [Fact]
    public void EveryNewlyLiveTypeIsMadeAndDestroyed()
    {
        var (r, _, _) = Room(15008, width: 15, height: 11);
        var types = typeof(obj_machinery_research_anomaly_refinery).Assembly.GetTypes()
            .Select(t => (Type: t, Path: DmTypes.PathOf(t)))
            .Where(t => t.Path != null && NewlyLive.Any(p => t.Path == p || t.Path.StartsWith(p + "/", StringComparison.Ordinal)))
            .ToList();
        Assert.True(types.Count > 100, "too few types found: " + types.Count);
        var inert = types.Where(t => Attribute.IsDefined(t.Type, typeof(DmInertAttribute), inherit: false) && !LeftInert.Contains(t.Path)).Select(t => t.Path).ToList();
        Assert.True(inert.Count == 0, "inert: " + string.Join(", ", inert));
        foreach (var (type, path) in types.Where(t => typeof(obj).IsAssignableFrom(t.Type) && !LeftInert.Contains(t.Path)))
        {
            var thing = r.In(() => Dm.@new(type, locate(8, 6, 1)) as datum);
            r.Step(2);
            r.In(() =>
            {
                if (!QDELETED(thing))
                    qdel(thing);
            });
            r.Step(2);
        }
        r.Step(Second);
        r.AssertNoRuntimes();
    }
}
