using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.M5.FurnitureTests;

namespace Anomaly.Core.Tests.M8x;

/// <summary>
/// M8x slice A: the cryo cell (atmospherics/machinery/components/unary_devices/cryo.dm), its pipe connection
/// (machine_connector.dm) and cryoxadone and pyroxadone (medicine_reagents.dm:143-216) in a booted toy room. A cell at (5, 4) faces
/// south onto a line of pipes; its process and process_atmos are called as SSmachines and SSair call them (seconds_per_tick 2 and
/// 0.5 as a step), so each assert is one of tg's steps. tg's unit test machine_disassembly is redone for the cell.
/// </summary>
public class CryoCellTests
{
    private const int Second = 20;
    private const int CellX = 5, CellY = 4;
    private const string Cold = "o2=100;TEMP=73.15";

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Bob, obj_machinery_cryo_cell Cell);

    /// <summary>The cell and two pipes south of it, joined the way a map's machines are.</summary>
    private static Rig Build(ulong seed)
    {
        var (r, bob) = Room(seed);
        var pipes = new List<obj_machinery_atmospherics>();
        for (int y = CellY - 1; y >= CellY - 2; y--)
            pipes.Add(r.In(() => Dm.@new<obj_machinery_atmospherics_pipe_smart>(locate(CellX, y, 1), TRUE, SOUTH)));
        var cell = r.In(() => Dm.@new<obj_machinery_cryo_cell>(locate(CellX, CellY, 1)));
        r.AssertNoRuntimes();
        r.In(() =>
        {
            pipes.Add(cell.internal_connector.gas_connector);
            SSair.setup_template_machinery(Dm.list(pipes.ToArray()));
        });
        r.Step(Second);
        return new Rig(r, bob, cell);
    }

    private static datum_gas_mixture Air(obj_machinery_cryo_cell cell) => (datum_gas_mixture)cell.internal_connector.gas_connector.airs[1];

    /// <summary>The cell's own gas becomes exactly this mix (the pipes keep theirs until the next SSair reconcile).</summary>
    private static void Fill(obj_machinery_cryo_cell cell, string mix)
    {
        datum_gas_mixture air = Air(cell);
        if (Dm.num(air.total_moles()) > 0)
            air.remove(air.total_moles());
        air.merge(SSair.parse_gas_string(mix) as datum_gas_mixture);
    }

    private static void Insert(obj_machinery_cryo_cell cell, mob_living who)
    {
        Waiting.NoSleep(cell.open_machine());
        Waiting.NoSleep(cell.close_machine(who));
    }

    private static datum_reagents Blood(mob_living_carbon who) => (datum_reagents)who.reagents;

    private static float Num(object x) => Dm.num(x);

    [Fact]
    public void TheConnectorJoinsThePipeAndSharesOnePipenet()
    {
        var g = Build(8801);
        g.R.In(() =>
        {
            var connector = g.Cell.internal_connector.gas_connector;
            Assert.Equal(SOUTH, connector.dir);
            Assert.Equal(CELL_VOLUME * 0.5f, Num(Air(g.Cell).volume)); // new(loc, src, dir, CELL_VOLUME * 0.5) (cryo.dm:118)
            var pipe = Dm.locate<obj_machinery_atmospherics_pipe_smart>(locate(CellX, CellY - 1, 1));
            Assert.Same(pipe, connector.nodes[1]);
            Assert.NotNull(connector.parents[1]);
            Assert.Same(pipe.parent, connector.parents[1]);
            // SSair.start_processing_machine (machine_connector.dm:17); the first SSair fire found it off (cryo.dm:442: PROCESS_KILL)
            Assert.False(Dm.truthy(g.Cell.atmos_processing));
            Assert.Equal("pod-off", g.Cell.icon_state);
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ClosingOnAPatientTurnsItOnAndOpeningTurnsItOff()
    {
        var g = Build(8802);
        mob_living_carbon_human pat = g.R.Dummy(CellX + 1, CellY);
        g.R.In(() =>
        {
            Fill(g.Cell, Cold);
            Insert(g.Cell, pat);
            Assert.Same(g.Cell, pat.loc);
            Assert.Same(pat, g.Cell.occupant);
            Assert.Equal(1f, g.Cell.on); // close_machine (:510): auto on with an occupant
            Assert.Equal("pod-on", g.Cell.icon_state);
            Assert.Equal(ACTIVE_POWER_USE, g.Cell.use_power);
            // the occupant visual (:37): the patient in its vis_contents, kept standing
            Assert.Same(pat, g.Cell.occupant_vis.occupant);
            Assert.True(Dm.@in(pat, g.Cell.occupant_vis.vis_contents));
            Assert.True(HAS_TRAIT_FROM(pat, TRAIT_FORCED_STANDING, CRYO_TRAIT));
            Assert.True(HAS_TRAIT_FROM(pat, TRAIT_IMMOBILIZED, CRYO_TRAIT));
            Assert.Equal(SOUTH, pat.dir);

            Waiting.NoSleep(g.Cell.open_machine());
            Assert.Equal(0f, g.Cell.on);
            Assert.Equal("pod-open", g.Cell.icon_state);
            Assert.Same(locate(CellX, CellY, 1), pat.loc);
            Assert.Null(g.Cell.occupant);
            Assert.Null(g.Cell.occupant_vis.occupant);
            Assert.False(HAS_TRAIT(pat, TRAIT_FORCED_STANDING));
            Assert.False(Dm.@in(pat, g.Cell.occupant_vis.vis_contents));
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void UnderFiveMolesItTurnsOffAndEjects()
    {
        var g = Build(8803);
        mob_living_carbon_human pat = g.R.Dummy(CellX + 1, CellY);
        g.R.In(() =>
        {
            Fill(g.Cell, "o2=4.9;TEMP=73.15");
            Insert(g.Cell, pat);
            Assert.Equal(1f, g.Cell.on);
            // process_atmos (:448): CRYO_MIN_GAS_MOLES 5; autoeject opens the cell
            Assert.Equal(PROCESS_KILL, g.Cell.process_atmos(0.5f));
            Assert.Equal(0f, g.Cell.on);
            Assert.True(Dm.truthy(g.Cell.state_open));
            Assert.Same(locate(CellX, CellY, 1), pat.loc);
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheBodyAndTheGasShareHeatByTgsConductionAndTheCoreFollows()
    {
        var g = Build(8804);
        mob_living_carbon_human pat = g.R.Dummy(CellX + 1, CellY);
        g.R.In(() =>
        {
            Fill(g.Cell, Cold);
            Insert(g.Cell, pat);
            datum_gas_mixture air = Air(g.Cell);
            float body = pat.bodytemperature, gas = air.temperature;
            float cold = Num(pat.get_cold_protection(gas));
            float gasCapacity = Num(air.heat_capacity());
            // cryo.dm:470-474 with tier-1 parts: heat_capacity 20000, conduction_coefficient 0.3
            float heat = ((1 - cold) * 0.1f + 0.3f) * CALCULATE_CONDUCTION_ENERGY(gas - body, 20000, gasCapacity);
            g.Cell.process_atmos(0.5f);
            Assert.Equal(body + heat / 20000, pat.bodytemperature, 3);
            Assert.Equal(gas - heat / gasCapacity, air.temperature, 3);
            Assert.True(pat.bodytemperature < body);
            Assert.Equal(pat.bodytemperature, pat.coretemperature, 3); // :479
            Assert.Equal(air.temperature, Num(Waiting.NoSleep(g.Cell.return_temperature()))); // above 5 moles: the cell's gas (:499)
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheBeakerGivesItsVapourAtTgsRate()
    {
        var g = Build(8805);
        mob_living_carbon_human pat = g.R.Dummy(CellX + 1, CellY);
        g.R.In(() =>
        {
            Waiting.NoSleep(pat.adjust_brute_loss(20)); // not fully healed (:413)
            var beaker = Dm.@new<obj_item_reagent_containers_cup_beaker_cryoxadone>(g.Cell);
            g.Cell.beaker = beaker;
            Fill(g.Cell, Cold);
            Insert(g.Cell, pat);
            Assert.Same(g.Cell, beaker.loc); // dump_inventory_contents (:341) drops only the mobs
            float before = Num(((datum_reagents)beaker.reagents).total_volume);
            g.Cell.process(2f);
            // :434 trans_to(occupant, CRYO_TX_QTY / (efficiency * 25) * seconds_per_tick, efficiency * 25, VAPOR)
            Assert.Equal(before - 0.04f, Num(((datum_reagents)beaker.reagents).total_volume), 4);
            // 0.04 * 25 = 1 u added, and the vapour's exposure (holder.dm:97, reagents.dm:137: penetrates_skin VAPOR) puts
            // round(1 * (1 - touch_protection), 0.1) = 1 u more through a bare skin
            Assert.Equal(2f, Num(Blood(pat).get_reagent_amount(typeof(datum_reagent_medicine_cryoxadone))), 3);
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void CryoxadoneHealsBelowFreezingByItsFormula()
    {
        var r = new AudioRoom(9, 9, seed: 8806);
        mob_living_carbon_human pat = r.Dummy(4, 4);
        r.In(() =>
        {
            Waiting.NoSleep(pat.adjust_brute_loss(40));
            Waiting.NoSleep(pat.adjust_oxy_loss(40));
            Waiting.NoSleep(pat.adjust_tox_loss(20));
            var cryox = Dm.@new<datum_reagent_medicine_cryoxadone>();
            float Brute() => Num(pat.get_brute_loss());

            // at or above T0C: no healing, the rate still follows the temperature (:156)
            pat.bodytemperature = T0C;
            float b0 = Brute();
            Waiting.NoSleep(cryox.on_mob_life(pat, 2f, 0.5f));
            Assert.Equal(b0, Brute(), 4);
            Assert.Equal(REAGENTS_METABOLISM * (0.00001f * T0C * T0C + 0.5f), cryox.metabolization_rate, 5);

            // 100 K: power -0.00003 T^2 + 3 = 2.7; brute -0.5 * power * ratio * seconds_per_tick, oxygen -1.5 *
            pat.bodytemperature = 100;
            float power = -0.00003f * 100 * 100 + 3;
            float o0 = Num(pat.get_oxy_loss()), t0 = Num(pat.get_tox_loss());
            b0 = Brute();
            Assert.Equal(UPDATE_MOB_HEALTH, Waiting.NoSleep(cryox.on_mob_life(pat, 2f, 0.5f)));
            Assert.Equal(b0 - 0.5f * power * 0.5f * 2, Brute(), DAMAGE_PRECISION); // a limb rounds its damage to DAMAGE_PRECISION
            Assert.Equal(o0 - 1.5f * power * 0.5f * 2, Num(pat.get_oxy_loss()), 3);
            Assert.Equal(t0 - 0.5f * power * 0.5f * 2, Num(pat.get_tox_loss()), 3);

            // unconscious: twice the power
            ADD_TRAIT(pat, TRAIT_KNOCKEDOUT, "test");
            b0 = Brute();
            Waiting.NoSleep(cryox.on_mob_life(pat, 2f, 0.5f));
            Assert.Equal(b0 - 0.5f * power * 2 * 0.5f * 2, Brute(), DAMAGE_PRECISION);

            // a disfigured head is fixed (:173)
            var head = (obj_item_bodypart)pat.get_bodypart(BODY_ZONE_HEAD);
            ADD_TRAIT(head, TRAIT_DISFIGURED, TRAIT_GENERIC);
            Waiting.NoSleep(cryox.on_mob_life(pat, 2f, 0.5f));
            Assert.False(HAS_TRAIT(head, TRAIT_DISFIGURED));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void PyroxadoneHealsAboveTheHeatLimitInThreeBands()
    {
        var r = new AudioRoom(9, 9, seed: 8807);
        mob_living_carbon_human pat = r.Dummy(4, 4);
        r.In(() =>
        {
            Waiting.NoSleep(pat.adjust_fire_loss(80));
            var pyrox = Dm.@new<datum_reagent_medicine_pyroxadone>();
            float Burn() => Num(pat.get_fire_loss());
            foreach (var (temp, power) in new[] { (BODYTEMP_HEAT_DAMAGE_LIMIT, 0f), (350f, 2f), (430f, 3f), (500f, 5f) })
            {
                pat.bodytemperature = temp;
                float f0 = Burn();
                Waiting.NoSleep(pyrox.on_mob_life(pat, 2f, 0.5f));
                Assert.Equal(f0 - 0.75f * power * 0.5f * 2, Burn(), DAMAGE_PRECISION); // :198-212, a limb rounds to DAMAGE_PRECISION
            }
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AFullyHealedPatientIsEjectedWithTheWarning()
    {
        var g = Build(8808);
        mob_living_carbon_human pat = g.R.Dummy(CellX + 1, CellY);
        g.R.In(() =>
        {
            Fill(g.Cell, Cold);
            Insert(g.Cell, pat);
            Assert.Equal(PROCESS_KILL, g.Cell.process(2f)); // :413: health at max
            Assert.Equal(0f, g.Cell.on);
            Assert.True(Dm.truthy(g.Cell.state_open));
            Assert.Same(locate(CellX, CellY, 1), pat.loc);
        });
        Assert.Contains(g.R.Sounds.Of(g.Bob, "sound/machines/cryo_warning.ogg"), h => h.Volume > 0);
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void WoundsOnlyKeepTheTreatmentGoingAndDeathEjects()
    {
        var g = Build(8809);
        mob_living_carbon_human pat = g.R.Dummy(CellX + 1, CellY);
        g.R.In(() =>
        {
            Fill(g.Cell, Cold);
            Insert(g.Cell, pat);
            // a wound and no damage (:416): treating_wounds, the cell stays on
            var arm = (obj_item_bodypart)pat.get_bodypart(BODY_ZONE_L_ARM);
            Assert.NotNull(Waiting.NoSleep(pat.cause_wound_of_type_and_severity(WOUND_BLUNT, arm, WOUND_SEVERITY_MODERATE)));
            Assert.True(Num(pat.get_organic_health()) >= Num(pat.getMaxHealth()));
            Assert.NotEqual(PROCESS_KILL, g.Cell.process(2f));
            Assert.Equal(1f, g.Cell.treating_wounds);
            Assert.Equal(1f, g.Cell.on);

            // dead: off, ejected (:404)
            Waiting.NoSleep(pat.death());
            Assert.Equal(PROCESS_KILL, g.Cell.process(2f));
            Assert.Equal(0f, g.Cell.on);
            Assert.Same(locate(CellX, CellY, 1), pat.loc);
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void APatientInTheCellSkipsTheEnvironmentAndTheBodyRegulation()
    {
        var g = Build(8810);
        mob_living_carbon_human pat = g.R.Dummy(CellX + 1, CellY);
        g.R.In(() =>
        {
            Fill(g.Cell, Cold);
            Waiting.NoSleep(pat.adjust_brute_loss(20)); // the cell keeps him (not fully healed)
            Insert(g.Cell, pat);
            var species = (datum_species)pat.dna.species;
            pat.bodytemperature = 150;
            pat.coretemperature = 150;
            // human/life.dm:109 and _species.dm:1048: nothing happens to a body in a cryo cell
            Waiting.NoSleep(pat.handle_environment(Air(g.Cell), 2f));
            Waiting.NoSleep(species.handle_body_temperature(pat, 2f));
            Assert.Equal(150f, pat.bodytemperature);
            Assert.Equal(150f, pat.coretemperature);
        });
        // R3: ten seconds at 73 K in the cell give no cold burns (tg's cell cools without the body's cold damage)
        float burn = g.R.In(() => Num(pat.get_fire_loss()));
        g.R.Step(10 * Second);
        g.R.In(() =>
        {
            Assert.Same(g.Cell, pat.loc);
            Assert.True(pat.bodytemperature < 150, $"body {pat.bodytemperature}");
            Assert.Equal(burn, Num(pat.get_fire_loss()));
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void APatientKicksTheReleaseInThirtySeconds()
    {
        var g = Build(8811);
        mob_living_carbon_human pat = g.R.Dummy(CellX + 1, CellY);
        g.R.In(() =>
        {
            Fill(g.Cell, "o2=21;n2=79;TEMP=293.15");
            Waiting.NoSleep(pat.adjust_brute_loss(20));
            Insert(g.Cell, pat);
            Waiting.Detach(g.Cell.container_resist_act(pat));
        });
        g.R.Step(29 * Second);
        Assert.Same(g.Cell, g.R.In(() => pat.loc));
        g.R.Step(2 * Second); // CRYO_BREAKOUT_TIME 30 s (:518)
        g.R.In(() =>
        {
            Assert.Same(locate(CellX, CellY, 1), pat.loc);
            Assert.True(Dm.truthy(g.Cell.state_open));
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheWindowShowsTgsDataAndTheClicksSwitchItAndItsDoor()
    {
        var g = Build(8812);
        mob_living_carbon_human pat = g.R.Dummy(CellX + 1, CellY);
        g.R.In(() =>
        {
            var beaker = Dm.@new<obj_item_reagent_containers_cup_beaker_cryoxadone>(g.Cell);
            g.Cell.beaker = beaker;
            Fill(g.Cell, Cold);
            Waiting.NoSleep(pat.adjust_brute_loss(20));
            Insert(g.Cell, pat);
            Assert.Same(GLOB.notcontained_state, g.Cell.ui_state(g.Bob));
            Assert.Equal(T0C, Num(((DmList)Waiting.NoSleep(g.Cell.ui_static_data(g.Bob)))["T0C"]));
            var data = (DmList)Waiting.NoSleep(g.Cell.ui_data(g.Bob));
            Assert.Equal(1f, Num(data["isOperating"]));
            Assert.Equal(0f, Num(data["isOpen"]));
            Assert.Equal(1f, Num(data["autoEject"]));
            Assert.Equal(73.15f, Num(data["cellTemperature"]), 2);
            var occ = (DmList)data["occupant"];
            Assert.Equal(pat.name, Dm.str(occ["name"]));
            Assert.Equal("Stable", Dm.str(occ["stat"]));
            Assert.Equal(20f, Num(occ["bruteLoss"]), 1);
            Assert.Equal(round(pat.bodytemperature, 1), Num(occ["bodyTemperature"]));
            var beakerData = (DmList)data["beaker"];
            Assert.Equal(Num(beaker.volume), Num(beakerData["maxVolume"]));
            var contents = (DmList)beakerData["contents"];
            Assert.Equal("Cryoxadone", Dm.str(((DmList)contents[1])["name"]));

            // the window's buttons (:587)
            var ui = Dm.@new<datum_tgui>(g.Bob, g.Cell, "Cryo", g.Cell.name);
            Assert.Equal(TRUE, Waiting.NoSleep(g.Cell.ui_act("autoeject", Dm.list(), ui, null)));
            Assert.Equal(0f, g.Cell.autoeject);
            Waiting.NoSleep(g.Cell.ui_act("power", Dm.list(), ui, null));
            Assert.Equal(0f, g.Cell.on);
            Assert.Equal("pod-off", g.Cell.icon_state);
            Waiting.NoSleep(g.Cell.ui_act("eject", Dm.list(), ui, null));
            Assert.Null(g.Cell.beaker);
            Assert.Same(beaker, g.Bob.get_active_held_item());

            // ctrl-click turns it on (:626); alt-click opens the door (:633)
            Assert.Equal(CLICK_ACTION_SUCCESS, Waiting.NoSleep(g.Cell.click_ctrl(g.Bob)));
            Assert.Equal(1f, g.Cell.on);
            Assert.Equal(CLICK_ACTION_SUCCESS, Waiting.NoSleep(g.Cell.click_alt(g.Bob)));
            Assert.True(Dm.truthy(g.Cell.state_open));
            Assert.Equal(0f, g.Cell.on);
            // the patient on the open cell's tile cannot close it on himself, nor reach its window
            Assert.Equal(CLICK_ACTION_BLOCKING, Waiting.NoSleep(g.Cell.click_alt(pat)));
            Assert.False(Dm.truthy(Waiting.NoSleep(g.Cell.can_interact(pat))));
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheCellIsTakenApartIntoItsBoardPartsAndFrameAndBuiltAgain()
    {
        // tg's unit test machine_disassembly (code/modules/unit_tests/machine_disassembly.dm), redone for the cell
        var g = Build(8813);
        var r = g.R;
        var bob = g.Bob;
        Assert.Contains(r.In(() => Dm.each(g.Cell.component_parts).ToList()), p => p is datum_stock_part_matter_bin);
        Assert.Equal(1f, r.In(() => g.Cell.efficiency)); // RefreshParts (:330): one tier-1 bin
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, g.Cell, Left, 1);
        Assert.True(r.In(() => Dm.truthy(g.Cell.panel_open)));
        Assert.Contains("pod-panel", r.In(() => Dm.each(g.Cell.overlays).Select(o => o is string str ? str : Dm.str(((dynamic)o).icon_state)).ToList()));
        Hold<obj_item_crowbar>(r, bob);
        Click(r, bob, g.Cell, Left, 1);
        Assert.True(r.In(() => QDELETED(g.Cell)));
        var frame = Assert.Single(On<obj_structure_frame_machine>(r));
        Assert.Single(On<obj_item_circuitboard_machine_cryo_tube>(r));
        Assert.Single(On<obj_item_stock_parts_matter_bin>(r));
        Assert.Equal(4f, Amount<obj_item_stack_sheet_glass>(r));
        Assert.Equal(1f, Amount<obj_item_stack_cable_coil>(r));
        Assert.Empty(On<obj_machinery_atmospherics_components_unary>(r)); // the connector went with the cell

        foreach (Type part in new[] { typeof(obj_item_circuitboard_machine_cryo_tube), typeof(obj_item_stack_sheet_glass),
            typeof(obj_item_stack_cable_coil), typeof(obj_item_stock_parts_matter_bin) })
        {
            r.In(() =>
            {
                foreach (object held in Dm.each(bob.held_items.Copy()))
                    if (held is obj_item i)
                        qdel(i);
                Waiting.NoSleep(bob.put_in_active_hand(On<obj_item>(r).First(i => i.GetType() == part)));
            });
            Click(r, bob, frame, Left, 1);
        }
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, frame, Left, 1);
        Assert.True(r.In(() => QDELETED(frame)));
        var built = Assert.Single(On<obj_machinery_cryo_cell>(r));
        r.In(() =>
        {
            Assert.IsType<obj_item_circuitboard_machine_cryo_tube>(built.circuit_value);
            Assert.Equal(1f, built.efficiency);
            Assert.NotNull(built.internal_connector.gas_connector);
            Assert.Same(Dm.locate<obj_machinery_atmospherics_pipe_smart>(locate(CellX, CellY - 1, 1)), built.internal_connector.gas_connector.nodes[1]);
        });
        r.AssertNoRuntimes();
    }
}
