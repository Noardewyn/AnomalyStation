using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M5;
using Anomaly.Core.Tests.M7;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Engineering;

// the explosive reagents' on_spark_act are chemistry's: a holder that answers as they will
[DmPath("/datum/reagents/test_destructive_spark")]
public class datum_reagents_test_destructive_spark : datum_reagents
{
    public override async DmTask<object> spark_act(object power_charge = null, object spark_flags = null, object banned_reagents = null) => SPARK_ACT_DESTRUCTIVE;
}

/// <summary>
/// What was left of modules/power: the floodlight (floodlight.dm) built from its frame and run on a fed cable, the portable SMES
/// (smes_portable.dm) lending its cells to a connector, the cell variants (cell.dm), the multi-z hub (multiz.dm) and tg's colour
/// matrix (colors.dm:184) that tints a floodlight and a fixture.
/// </summary>
public class PowerLeftoversTests
{
    private const int Second = 20;
    private const int LineY = 3, SourceX = 7, RigX = 3;

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Bob, obj_machinery_power_test_m7apc_source Source);

    private static Rig Room(ulong seed, float output = 2_000_000, Action<World> extras = null)
    {
        obj_machinery_power_test_m7apc_source source = null;
        var r = new AudioRoom(13, 10, seed: seed, mapExtras: w =>
        {
            for (int x = 2; x <= SourceX; x++)
            {
                Dm.@new<turf_open_floor_plating>(locate(x, LineY, 1));
                Dm.@new<obj_structure_cable>(locate(x, LineY, 1));
            }
            source = Dm.@new<obj_machinery_power_test_m7apc_source>(locate(SourceX, LineY, 1));
            extras?.Invoke(w);
        });
        r.In(() => source.output_energy = output);
        mob_living_carbon_human bob = r.Join(RigX, LineY + 1, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        MachinesTick(r, 2);
        return new Rig(r, bob, source);
    }

    private static void MachinesTick(AudioRoom r, int runs = 1) => r.Step(runs * r.In(() => (int)Dm.num(SSmachines.wait)) * 2);

    private static void Click(AudioRoom r, mob who, atom target, float seconds = 1)
    {
        r.Command(() => ClientSeam.Click(who.client, target, "icon-x=16;icon-y=16;left=1;button=left"));
        r.Step(Math.Max(2, (int)(seconds * Second)));
    }

    private static void EmptyHands(AudioRoom r, mob_living_carbon_human who) => r.In(() =>
    {
        foreach (object held in Dm.each(who.held_items.Copy()))
        {
            if (held is obj_item i)
                qdel(i);
        }
    });

    private static T One<T>(AudioRoom r, int x, int y) where T : class => r.In(() => Dm.locate<T>(locate(x, y, 1)));

    [Fact]
    public void AFrameTakesCableAScrewdriverAndATubeAndBecomesAFloodlight()
    {
        Rig g = Room(8101);
        AudioRoom r = g.R;
        obj_structure_floodlight_frame frame = r.In(() => Dm.@new<obj_structure_floodlight_frame>(locate(RigX, LineY, 1)));

        WallTests.Hold<obj_item_stack_cable_coil>(r, g.Bob, 10);
        Click(r, g.Bob, frame);
        Assert.Equal("floodlight_c2", r.In(() => frame.icon_state));
        EmptyHands(r, g.Bob);

        WallTests.Hold<obj_item_screwdriver>(r, g.Bob);
        Click(r, g.Bob, frame);
        Assert.Equal("floodlight_c3", r.In(() => frame.icon_state));
        EmptyHands(r, g.Bob);

        WallTests.Hold<obj_item_light_tube>(r, g.Bob);
        Click(r, g.Bob, frame);
        Assert.True(r.In(() => QDELETED(frame)));
        Assert.NotNull(One<obj_machinery_power_floodlight>(r, RigX, LineY));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWrenchedFloodlightDrawsFromTheCableAndAHandStepsItsPower()
    {
        Rig g = Room(8102);
        AudioRoom r = g.R;
        obj_machinery_power_floodlight light = r.In(() => Dm.@new<obj_machinery_power_floodlight>(locate(RigX, LineY, 1)));

        WallTests.Hold<obj_item_wrench>(r, g.Bob);
        Click(r, g.Bob, light, 3);
        Assert.True(r.In(() => Dm.truthy(light.anchored)));
        Assert.NotNull(r.In(() => light.powernet));
        EmptyHands(r, g.Bob);
        MachinesTick(r, 2);

        // from off a hand goes to the top of the list, then down it (attack_hand :295)
        Click(r, g.Bob, light);
        Assert.Equal(4, r.In(() => light.setting));
        Assert.Equal(15 * 200, r.In(() => light.active_power_usage));
        Assert.Equal("floodlight_on", r.In(() => light.icon_state));
        Assert.Equal(15, r.In(() => Dm.num(light.light_range)));

        // a user-visible load on the net
        MachinesTick(r, 2);
        Assert.True(r.In(() => Dm.num(((datum_powernet)light.powernet).load) > 0), "the floodlight drew nothing");

        Click(r, g.Bob, light);
        Assert.Equal(3, r.In(() => light.setting));
        r.Step(Second);
        Click(r, g.Bob, light);
        r.Step(Second);
        Click(r, g.Bob, light);
        Assert.Equal(1, r.In(() => light.setting));
        Assert.Equal("floodlight", r.In(() => light.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALitFloodlightGoesOutWhenTheNetCannotFeedIt()
    {
        Rig g = Room(8103);
        AudioRoom r = g.R;
        obj_machinery_power_floodlight light = r.In(() => Dm.@new<obj_machinery_power_floodlight>(locate(RigX, LineY, 1)));
        r.In(() =>
        {
            Waiting.NoSleep(light.set_anchored(TRUE));
            light.connect_to_network();
        });
        MachinesTick(r, 2);
        Click(r, g.Bob, light);
        Assert.Equal(4, r.In(() => light.setting));

        r.In(() => g.Source.output_energy = 0);
        MachinesTick(r, 4);
        Assert.Equal(1, r.In(() => light.setting));
        Assert.Equal("floodlight", r.In(() => light.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnOpenFloodlightGivesItsTubeToAHandAndLeavesItsFrame()
    {
        Rig g = Room(8104);
        AudioRoom r = g.R;
        obj_machinery_power_floodlight light = r.In(() => Dm.@new<obj_machinery_power_floodlight>(locate(RigX, LineY, 1)));

        WallTests.Hold<obj_item_screwdriver>(r, g.Bob);
        Click(r, g.Bob, light);
        Assert.True(r.In(() => Dm.truthy(light.panel_open)));
        EmptyHands(r, g.Bob);

        Click(r, g.Bob, light);
        Assert.True(r.In(() => QDELETED(light)));
        obj_structure_floodlight_frame frame = One<obj_structure_floodlight_frame>(r, RigX, LineY);
        Assert.NotNull(frame);
        Assert.Equal(FLOODLIGHT_NEEDS_LIGHTS, r.In(() => frame.state));
        Assert.True(r.In(() => g.Bob.get_active_held_item() is obj_item_light_tube), "the tube is not in the hand");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABrokenFloodlightShattersItsTubeOverItsFrame()
    {
        Rig g = Room(8105);
        AudioRoom r = g.R;
        obj_machinery_power_floodlight light = r.In(() => Dm.@new<obj_machinery_power_floodlight>(locate(RigX, LineY, 1)));

        r.In(() => Waiting.NoSleep(light.atom_break(MELEE)));
        r.Step();
        Assert.True(r.In(() => QDELETED(light)));
        Assert.NotNull(One<obj_structure_floodlight_frame>(r, RigX, LineY));
        Assert.Equal(LIGHT_BROKEN, r.In(() => One<obj_item_light_tube>(r, RigX, LineY).status));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/effects/glass/glassbr3.ogg");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABankWrenchedOntoAConnectorLendsItItsCellsUntilItIsUnwrenched()
    {
        Rig g = Room(8106);
        AudioRoom r = g.R;
        obj_machinery_power_smes_connector port = r.In(() => Dm.@new<obj_machinery_power_smes_connector>(locate(RigX, LineY, 1)));
        obj_machinery_smesbank bank = r.In(() => Dm.@new<obj_machinery_smesbank_super_full>(locate(RigX, LineY, 1)));
        float stored = r.In(() => Dm.each<obj_item_stock_parts_power_store>(bank.component_parts).Sum(c => Dm.num(c.charge__proc())));
        Assert.True(stored > 0, "the full bank holds nothing");
        Assert.False(r.In(() => Dm.truthy(bank.anchored)));

        // the connector's window needs a bank
        r.In(() => Waiting.NoSleep(port.ui_interact(g.Bob)));
        Assert.Null(r.In(() => port.connected_smes));

        WallTests.Hold<obj_item_wrench>(r, g.Bob);
        Click(r, g.Bob, bank, 5);
        Assert.Same(bank, r.In(() => port.connected_smes));
        Assert.Same(port, r.In(() => bank.connected_port));
        Assert.True(r.In(() => Dm.truthy(bank.anchored)));
        Assert.Equal(stored, r.In(() => Dm.num(port.total_charge())));
        float capacity = r.In(() => port.total_capacity);
        Assert.True(capacity >= stored, "the connector has not counted the cells");

        // a bound connector refuses its tools, and the bank its crowbar
        Assert.Equal(ITEM_INTERACT_BLOCKING, r.In(() => Dm.num(Waiting.NoSleep(port.crowbar_act(g.Bob, null)))));
        Assert.Equal(ITEM_INTERACT_FAILURE, r.In(() => Dm.num(Waiting.NoSleep(bank.crowbar_act(g.Bob, null)))));

        Click(r, g.Bob, bank, 9);
        Assert.Null(r.In(() => port.connected_smes));
        Assert.Null(r.In(() => bank.connected_port));
        Assert.False(r.In(() => Dm.truthy(bank.anchored)));
        Assert.Equal(1, r.In(() => port.total_capacity));
        Assert.Equal(0, r.In(() => Dm.num(port.total_charge())));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABankOnAnOpenConnectorRefusesToBind()
    {
        Rig g = Room(8107);
        AudioRoom r = g.R;
        obj_machinery_power_smes_connector port = r.In(() => Dm.@new<obj_machinery_power_smes_connector>(locate(RigX, LineY, 1)));
        obj_machinery_smesbank bank = r.In(() => Dm.@new<obj_machinery_smesbank>(locate(RigX, LineY, 1)));
        r.In(() => port.panel_open = TRUE);

        WallTests.Hold<obj_item_wrench>(r, g.Bob);
        Click(r, g.Bob, bank, 5);
        Assert.Null(r.In(() => port.connected_smes));
        Assert.False(r.In(() => Dm.truthy(bank.anchored)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMappedBankBindsItsConnectorAndSwitchesItOn()
    {
        obj_machinery_power_smes_connector port = null;
        obj_machinery_smesbank bank = null;
        Rig g = Room(8108, extras: w =>
        {
            port = Dm.@new<obj_machinery_power_smes_connector>(locate(RigX, LineY, 1));
            bank = Dm.@new<obj_machinery_smesbank>(locate(RigX, LineY, 1));
        });
        AudioRoom r = g.R;
        r.Step(Second);
        Assert.Same(bank, r.In(() => port.connected_smes));
        Assert.True(r.In(() => Dm.truthy(port.input_attempt) && Dm.truthy(port.output_attempt)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnEmpDrainsTheBanksCells()
    {
        obj_machinery_smesbank bank = null;
        Rig g = Room(8109, extras: w => bank = Dm.@new<obj_machinery_smesbank_super_full>(locate(RigX, LineY, 1)));
        AudioRoom r = g.R;
        float Stored() => r.In(() => Dm.each<obj_item_stock_parts_power_store>(bank.component_parts).Sum(c => Dm.num(c.charge__proc())));
        float before = Stored();
        r.In(() => Waiting.NoSleep(bank.emp_act(EMP_LIGHT)));
        Assert.True(Stored() < before, "the EMP drained nothing");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCellVariantsKeepTheirOwnRules()
    {
        Rig g = Room(8110);
        AudioRoom r = g.R;
        r.In(() =>
        {
            var infinite = Dm.@new<obj_item_stock_parts_power_store_cell_infinite>(locate(RigX, LineY, 1));
            float charge = Dm.num(infinite.charge__proc());
            Assert.Equal(500f, Dm.num(Waiting.NoSleep(infinite.use(500))));
            Assert.Equal(charge, Dm.num(infinite.charge__proc()));

            var potato = Dm.@new<obj_item_stock_parts_power_store_cell_potato>(locate(RigX, LineY, 1));
            Assert.Equal(potato.maxcharge * 0.3f, Dm.num(potato.charge__proc()), 1);

            var crap = Dm.@new<obj_item_stock_parts_power_store_cell_crap>(locate(RigX, LineY, 1));
            string icon = crap.icon_state;
            crap.update_appearance();
            Assert.Equal(icon, crap.icon_state);

            Assert.Null(Waiting.NoSleep(Dm.@new<obj_item_stock_parts_power_store_cell_ethereal>(locate(RigX, LineY, 1)).grind_results()));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TwoHubsOnTwoLevelsShareOnePowernet()
    {
        obj_structure_cable_multilayer_multiz low = null, high = null;
        var r = new AudioRoom(8, 8, seed: 8111, levels: 2, mapExtras: w =>
        {
            low = Dm.@new<obj_structure_cable_multilayer_multiz>(locate(3, 3, 1));
            high = Dm.@new<obj_structure_cable_multilayer_multiz>(locate(3, 3, 2));
        });
        r.In(() =>
        {
            SSmapping.multiz_levels.len = 2;
            SSmapping.multiz_levels[1] = Dm.list_n(LARGEST_Z_LEVEL_INDEX);
            SSmapping.multiz_levels[2] = Dm.list_n(LARGEST_Z_LEVEL_INDEX);
            ((DmList)SSmapping.multiz_levels[1])[Z_LEVEL_UP] = 1;
            ((DmList)SSmapping.multiz_levels[2])[Z_LEVEL_DOWN] = 1;
        });
        r.Step(Second);
        DmList links = r.In(() => (DmList)low.get_cable_connections());
        Assert.True(r.In(() => Dm.@in(high, links)), "the hub below does not reach the hub above");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AColourMatrixAppliesAsTgReadsIt()
    {
        Rig g = Room(8112);
        g.R.In(() =>
        {
            // the identity leaves a colour alone; a constants row adds a share of full (colors.dm:228)
            DmList identity = Dm.list(1, 0, 0, 0, 1, 0, 0, 0, 1);
            Assert.StartsWith("#336699", Dm.str(apply_matrix_to_color("#336699", identity, COLORSPACE_RGB)));
            DmList add_red = Dm.list(1, 0, 0, 0, 1, 0, 0, 0, 1, 0.2f, 0, 0);
            Assert.StartsWith("#666699", Dm.str(apply_matrix_to_color("#336699", add_red, COLORSPACE_RGB)));
            // a filter with no colour swap of channels: red takes green's value
            DmList swap = Dm.list(0, 1, 0, 1, 0, 0, 0, 0, 1);
            Assert.StartsWith("#ff0000", Dm.str(apply_matrix_to_color("#00ff00", swap, COLORSPACE_RGB)));
        });
    }

    [Fact]
    public void ABlackoutStopsTheGravityGeneratorAndAKickstartBringsItBack()
    {
        Rig g = Room(8113);
        AudioRoom r = g.R;
        obj_machinery_gravity_generator_main gen = r.In(() => Dm.@new<obj_machinery_gravity_generator_main>(locate(10, 6, 1)));
        r.Step(Second);
        r.In(() => Waiting.NoSleep(gen.blackout()));
        Assert.Equal(0, r.In(() => gen.charge_count));
        Assert.False(r.In(() => Dm.truthy(gen.breaker)));
        Assert.Equal(0, r.In(() => Dm.num(gen.get_radioactive_nebula_shielding())));

        r.In(() => Waiting.NoSleep(gen.kickstart()));
        Assert.Equal(100, r.In(() => gen.charge_count));
        Assert.True(r.In(() => Dm.truthy(gen.breaker)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnEmagShortsTheEmittersLockAndAnEnergyGunBecomesItsShot()
    {
        Rig g = Room(8114);
        AudioRoom r = g.R;
        obj_machinery_power_emitter emitter = r.In(() => Dm.@new<obj_machinery_power_emitter>(locate(RigX, LineY, 1)));
        r.In(() => emitter.locked = TRUE);
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(emitter.emag_act(g.Bob)))));
        Assert.False(r.In(() => Dm.truthy(emitter.locked)));
        Assert.False(r.In(() => Dm.truthy(Waiting.NoSleep(emitter.emag_act(g.Bob)))));

        var laser = WallTests.Hold<obj_item_gun_energy_laser>(r, g.Bob);
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(emitter.integrate(laser, g.Bob)))));
        Assert.Same(laser, r.In(() => emitter.gun));
        Assert.Equal(r.In(() => emitter.gun_properties["lethal_projectile"] as Type), r.In(() => emitter.projectile_type));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AGunnerBucklesToThePrototypeEmitterAndFiresItByHand()
    {
        Rig g = Room(8115);
        AudioRoom r = g.R;
        obj_machinery_power_emitter_prototype proto = r.In(() => Dm.@new<obj_machinery_power_emitter_prototype>(locate(RigX, LineY, 1)));
        r.In(() => Waiting.NoSleep(proto.user_buckle_mob(g.Bob, g.Bob)));
        r.Step();
        Assert.Same(proto, r.In(() => g.Bob.buckled));
        Assert.NotNull(r.In(() => proto.auto));
        Assert.Equal(14, r.In(() => Dm.num(g.Bob.pixel_y)));

        // the action trades the hands for turret controls
        r.In(() => Waiting.NoSleep(proto.auto.Activate()));
        Assert.True(r.In(() => Dm.truthy(proto.manual)));
        obj_item_turret_control control = r.In(() => Dm.each<obj_item_turret_control>(g.Bob.held_items).First());
        Assert.Equal("mech_zoom_off", r.In(() => proto.auto.button_icon_state));

        // a shot needs ten charge and turns the gunner to the target
        r.In(() => proto.charge = 20);
        r.In(() => Waiting.NoSleep(control.ranged_interact_with_atom(locate(RigX + 4, LineY, 1), g.Bob, Dm.list(Dm.kv(SCREEN_LOC, "8:16,8:16")))));
        Assert.Equal(EAST, r.In(() => Dm.num(proto.dir)));
        Assert.Equal(10, r.In(() => proto.charge));

        r.In(() => proto.charge = 0);
        r.In(() => Waiting.NoSleep(control.ranged_interact_with_atom(locate(RigX + 4, LineY, 1), g.Bob, Dm.list(Dm.kv(SCREEN_LOC, "8:16,8:16")))));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/machines/buzz/buzz-sigh.ogg");

        r.In(() => Waiting.NoSleep(proto.unbuckle_mob(g.Bob, TRUE)));
        r.Step();
        Assert.Null(r.In(() => g.Bob.buckled));
        Assert.Empty(r.In(() => Dm.each<obj_item_turret_control>(g.Bob.held_items).ToList()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADarkMatterSingularityShrugsOffBlastsAndHuntsTheNearestLife()
    {
        Rig g = Room(8116);
        AudioRoom r = g.R;
        obj_singularity_dark_matter dark = r.In(() => Dm.@new<obj_singularity_dark_matter>(locate(10, 7, 1)));
        var hunter = r.In(() => (datum_component_singularity_bloodthirsty)dark.singularity_component.resolve());
        Assert.Equal(100, r.In(() => hunter.chance_to_move_to_target));
        Assert.False(r.In(() => Dm.truthy(dark.ex_act(EXPLODE_DEVASTATE))), "the fresh singularity took a blast");
        Assert.Same(g.Bob, r.In(() => hunter.find_new_target()));

        r.In(() => ADD_TRAIT(g.Bob, TRAIT_GODMODE, "test"));
        Assert.Null(r.In(() => hunter.find_new_target()));

        r.In(() => dark.normalize_tracking());
        Assert.Equal(80, r.In(() => hunter.chance_to_move_to_target));
        r.In(() => Waiting.NoSleep(dark.supermatter_upgrade()));
        Assert.Equal("Dark Lord Singuloth", r.In(() => dark.name));
        Assert.Equal(90, r.In(() => hunter.chance_to_move_to_target));
    }

    [Fact]
    public void ABagOfHoldingCollapsesTheSingularity()
    {
        Rig g = Room(8117);
        AudioRoom r = g.R;
        obj_singularity singulo = r.In(() => Dm.@new<obj_singularity>(locate(10, 7, 1)));
        obj_item_storage_backpack_holding bag = r.In(() => Dm.@new<obj_item_storage_backpack_holding>(locate(10, 6, 1)));
        r.In(() => Waiting.NoSleep(singulo.consume(bag)));
        Assert.True(r.In(() => Dm.truthy(singulo.collapsing)));
        Assert.StartsWith("unstable", r.In(() => singulo.name));
        r.Step(5 * Second);
        Assert.True(r.In(() => QDELETED(singulo)));
    }

    [Fact]
    public void ATemporaryTearCollapsesIntoNothingAndAFullOneLeavesASingularity()
    {
        Rig g = Room(8118);
        AudioRoom r = g.R;
        obj_reality_tear_temporary small = r.In(() => Dm.@new<obj_reality_tear_temporary>(locate(10, 7, 1)));
        r.In(() => Waiting.NoSleep(small.reality_collapse()));
        Assert.True(r.In(() => QDELETED(small)));

        obj_reality_tear tear = r.In(() => Dm.@new<obj_reality_tear>(locate(10, 7, 1)));
        r.In(() => Waiting.NoSleep(tear.reality_collapse()));
        Assert.True(r.In(() => QDELETED(tear)));
        Assert.NotNull(One<obj_singularity>(r, 10, 7));
    }

    [Fact]
    public void ASabotagedFixtureBreaksAndGreyTideMakesItFlicker()
    {
        Rig g = Room(8119);
        AudioRoom r = g.R;
        obj_machinery_light light = r.In(() => Dm.@new<obj_machinery_light>(locate(RigX, LineY + 3, 1)));
        r.Step(Second);
        r.In(() => light.grey_tide(null, Dm.list(typeof(area))));
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(light.on_saboteur(null, 15 * SECONDS)))));
        Assert.Equal(LIGHT_BROKEN, r.In(() => light.status));

        float before = r.In(() => Dm.num(light.no_low_power));
        r.In(() => Waiting.NoSleep(light.attack_ai(g.Bob)));
        Assert.NotEqual(before, r.In(() => Dm.num(light.no_low_power)));
    }

    [Fact]
    public void ACableBitByARegalRatShocksHimAndComesApart()
    {
        Rig g = Room(8120);
        AudioRoom r = g.R;
        obj_structure_cable cable = One<obj_structure_cable>(r, RigX, LineY);
        var rat = r.In(() => Dm.@new<mob_living_basic_regal_rat>(locate(RigX, LineY + 1, 1)));
        r.In(() => cable.on_rat_eat(null, rat));
        Assert.True(r.In(() => QDELETED(cable)));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/effects/sparks/sparks2.ogg");
    }

    [Fact]
    public void AnElectrodeLickerTakesTheCellsChargeAndLivesIfItIsTooWeak()
    {
        Rig g = Room(8121);
        AudioRoom r = g.R;
        obj_item_stock_parts_power_store_cell cell = r.In(() => Dm.@new<obj_item_stock_parts_power_store_cell>(locate(RigX, LineY + 1, 1)));
        r.In(() => cell.charge = 10);
        object result = null;
        r.In(() => { Waiting.Detach(Lick(cell, g.Bob, v => result = v)); });
        r.Step(6 * Second);
        Assert.Equal(SHAME, Dm.num(result));
        Assert.Equal(0, r.In(() => cell.charge));
    }

    private static async DmTask Lick(obj_item_stock_parts_power_store cell, mob_living who, Action<object> done) => done(await cell.suicide_act(who));

    [Fact]
    public void ACellWhoseReagentsBlowOnASparkGoesOff()
    {
        Rig g = Room(8122);
        AudioRoom r = g.R;
        var rigged = r.In(() => Dm.@new<obj_item_stock_parts_power_store_cell>(locate(RigX, LineY + 1, 1)));
        r.In(() => rigged.reagents = Dm.@new<datum_reagents_test_destructive_spark>(5));
        r.In(() => Waiting.NoSleep(rigged.try_explode()));
        Assert.True(r.In(() => QDELETED(rigged)), "a rigged cell did not go off");
    }

    [Fact]
    public void TheGunsTellATurretTheirShots()
    {
        Rig g = Room(8123);
        g.R.In(() =>
        {
            var egun = Dm.@new<obj_item_gun_energy_e_gun>(locate(RigX, LineY + 1, 1));
            DmList props = (DmList)egun.get_turret_properties();
            Assert.NotNull(props["stun_projectile"]);
            Assert.NotNull(props["lethal_projectile"]);
            Assert.NotEqual(props["stun_projectile"], props["lethal_projectile"]);
            Assert.Equal("standard", Dm.str(props["base_icon_state"]));
        });
    }
}
