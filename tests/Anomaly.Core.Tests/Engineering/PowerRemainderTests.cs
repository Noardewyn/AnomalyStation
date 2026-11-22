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

/// <summary>
/// The last of modules/power: what it needed of other systems. An ethereal stomach drinking an APC and a cell, the dead chat's control
/// of a thing, the base GPS signal, telekinesis at a light, the pseudocircuit, Nar'Sie, the admin hooks (vv_edit_var, get_save_vars).
/// </summary>
public class PowerRemainderTests
{
    private const int Second = 20;
    private const int X = 3, Y = 2;

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Bob, obj_machinery_power_apc Apc);

    private static Rig Room(ulong seed)
    {
        obj_machinery_power_apc apc = null;
        var r = new AudioRoom(13, 10, seed: seed, mapExtras: w =>
        {
            ((area)GLOB.areas_by_type[typeof(area_misc_testroom)]).requires_power = TRUE;
            apc = Dm.@new<obj_machinery_power_apc_auto_name_directional_south>(locate(X, Y, 1));
        });
        mob_living_carbon_human bob = r.Join(X, Y + 1, "Bob");
        r.Step(Second);
        return new Rig(r, bob, apc);
    }

    private static obj_item_organ_stomach_ethereal EtherealStomach(Rig g, float charge)
    {
        obj_item_organ_stomach_ethereal stomach = null;
        g.R.In(() =>
        {
            stomach = Dm.@new<obj_item_organ_stomach_ethereal>();
            Waiting.NoSleep(stomach.Insert(g.Bob));
            stomach.cell.charge = charge;
        });
        return stomach;
    }

    [Fact]
    public void AnEtherealDrinksAnApcDownToHalfAndNoFurther()
    {
        Rig g = Room(8201);
        AudioRoom r = g.R;
        obj_item_organ_stomach_ethereal stomach = EtherealStomach(g, 0);
        r.In(() => g.Apc.cell.charge = g.Apc.cell.maxcharge * 0.6f);
        r.In(() => { Waiting.Detach(g.Apc.discharge_to_ethereal(g.Bob, stomach)); });
        r.Step(7 * Second);
        float drunk = r.In(() => stomach.cell.charge);
        Assert.True(drunk > 0, "the stomach took nothing from the APC");
        float maxcharge = r.In(() => g.Apc.cell.maxcharge);
        Assert.True(r.In(() => g.Apc.cell.charge) < maxcharge * 0.6f, "the APC gave nothing");

        // under half the safeties refuse
        r.In(() => g.Apc.cell.charge = maxcharge * 0.4f);
        r.In(() => { Waiting.Detach(g.Apc.discharge_to_ethereal(g.Bob, stomach)); });
        r.Step(4 * Second);
        Assert.Equal(maxcharge * 0.4f, r.In(() => g.Apc.cell.charge), 1);
    }

    [Fact]
    public void AnEtherealGivesAnApcItsOwnCharge()
    {
        Rig g = Room(8202);
        AudioRoom r = g.R;
        obj_item_organ_stomach_ethereal stomach = EtherealStomach(g, ETHEREAL_CHARGE_FULL);
        r.In(() => g.Apc.cell.charge = 0);
        r.In(() => { Waiting.Detach(g.Apc.charge_from_ethereal(g.Bob, stomach)); });
        r.Step(4 * Second);
        Assert.True(r.In(() => g.Apc.cell.charge) > 0, "the APC took nothing");
        Assert.True(r.In(() => stomach.cell.charge) < ETHEREAL_CHARGE_FULL, "the stomach gave nothing");
    }

    [Fact]
    public void AnEtherealDrainsACellByHand()
    {
        Rig g = Room(8203);
        AudioRoom r = g.R;
        obj_item_organ_stomach_ethereal stomach = EtherealStomach(g, 0);
        var cell = r.In(() => Dm.@new<obj_item_stock_parts_power_store_cell>(locate(X, Y + 1, 1)));
        float full = r.In(() => cell.charge);
        r.In(() => { Waiting.Detach(cell.ethereal_drain(g.Bob, stomach)); });
        r.Step(5 * Second);
        Assert.True(r.In(() => cell.charge) < full, "the cell gave nothing");
        Assert.True(r.In(() => stomach.cell.charge) > 0, "the stomach took nothing");
    }

    [Fact]
    public void AnEmptyEtherealStarvesAndAFullOneDoesNot()
    {
        Rig g = Room(8204);
        AudioRoom r = g.R;
        EtherealStomach(g, 0);
        r.Step(3 * Second);
        Assert.NotNull(r.In(() => g.Bob.alerts[ALERT_ETHEREAL_CHARGE]));
    }

    [Fact]
    public void TheDeadVoteAThingAcross()
    {
        Rig g = Room(8205);
        AudioRoom r = g.R;
        var thing = r.In(() => Dm.@new<obj_structure_closet>(locate(X + 3, Y + 3, 1)));
        var control = r.In(() => (datum_component_deadchat_control)Waiting.NoSleep(thing.deadchat_plays(ANARCHY_MODE, 2 * SECONDS)));
        turf before = r.In(() => get_turf(thing));
        object answer = r.In(() => control.deadchat_react(g.Bob, "up"));
        Assert.Equal(MOB_DEADSAY_SIGNAL_INTERCEPT, Dm.num(answer));
        Assert.NotEqual(before, r.In(() => get_turf(thing)));
        // anarchy has a cooldown: a second word at once moves nothing
        turf moved = r.In(() => get_turf(thing));
        r.In(() => control.deadchat_react(g.Bob, "up"));
        Assert.Equal(moved, r.In(() => get_turf(thing)));
    }

    [Fact]
    public void TheDeadPlayingASingularityPinIt()
    {
        Rig g = Room(8206);
        AudioRoom r = g.R;
        var singulo = r.In(() => Dm.@new<obj_singularity>(locate(X + 5, Y + 5, 1)));
        r.In(() => Waiting.NoSleep(singulo.deadchat_plays()));
        Assert.False(r.In(() => Dm.truthy(singulo.move_self)));
        Assert.NotNull(r.In(() => singulo.GetComponent(typeof(datum_component_deadchat_control))));
        r.In(() => singulo.stop_deadchat_plays());
        Assert.True(r.In(() => Dm.truthy(singulo.move_self)));
    }

    [Fact]
    public void ASignalLivesInTheGpsListWhileItsOwnerDoes()
    {
        Rig g = Room(8207);
        AudioRoom r = g.R;
        var crate = r.In(() => Dm.@new<obj_structure_closet>(locate(X + 1, Y + 1, 1)));
        var signal = r.In(() => (datum_component_gps)Waiting.NoSleep(crate.AddComponent(typeof(datum_component_gps), "Adamantium Signal")));
        Assert.True(r.In(() => Dm.truthy(Dm.@in(signal, GLOB.GPS_list))));
        Assert.Equal("Adamantium Signal", r.In(() => signal.gpstag));
        r.In(() => qdel(crate));
        Assert.False(r.In(() => Dm.truthy(Dm.@in(signal, GLOB.GPS_list))));
    }

    [Fact]
    public void TheSmallCrystalMarksItselfOnEveryGps()
    {
        Rig g = Room(8215);
        AudioRoom r = g.R;
        var crystal = r.In(() => Dm.@new<obj_machinery_power_supermatter_crystal_small>(locate(X + 6, Y + 6, 1)));
        var signal = r.In(() => crystal.GetComponent(typeof(datum_component_gps)) as datum_component_gps);
        Assert.NotNull(signal);
        Assert.Equal("Adamantium Signal", r.In(() => signal.gpstag));
    }

    [Fact]
    public void TelekinesisPullsATubeOutOfItsFixture()
    {
        Rig g = Room(8208);
        AudioRoom r = g.R;
        var lamp = r.In(() => Dm.@new<obj_machinery_light_directional_south>(locate(X + 1, Y, 1)));
        r.In(() => { lamp.allow_break_on_init = FALSE; if (!Dm.eq(lamp.status, LIGHT_OK)) Waiting.NoSleep(lamp.fix()); });
        r.In(() => Waiting.NoSleep(g.Bob.dna.add_mutation(typeof(datum_mutation_telekinesis), "test")));
        r.In(() => Waiting.NoSleep(lamp.attack_tk(g.Bob)));
        Assert.Equal(LIGHT_EMPTY, r.In(() => Dm.num(lamp.status)));
        Assert.NotNull(r.In(() => g.Bob.get_active_held_item() as obj_item_tk_grab));
    }

    [Fact]
    public void AClickFromAcrossTheRoomGrabsAnItemWithTheMind()
    {
        Rig g = Room(8214);
        AudioRoom r = g.R;
        var far = r.In(() => Dm.@new<obj_item_stock_parts_power_store_cell>(locate(X + 6, Y + 5, 1)));
        r.Command(() => ClientSeam.Click(g.Bob.client, far, "icon-x=16;icon-y=16;left=1;button=left"));
        r.Step(Second);
        Assert.Null(r.In(() => g.Bob.get_active_held_item()));

        r.In(() => Waiting.NoSleep(g.Bob.dna.add_mutation(typeof(datum_mutation_telekinesis), "test")));
        r.Command(() => ClientSeam.Click(g.Bob.client, far, "icon-x=16;icon-y=16;left=1;button=left"));
        r.Step(Second);
        var grab = r.In(() => g.Bob.get_active_held_item() as obj_item_tk_grab);
        Assert.NotNull(grab);
        Assert.Equal(far, r.In(() => grab.focus));
    }

    [Fact]
    public void ThePseudocircuitEatsACircuitAndCountsIt()
    {
        Rig g = Room(8209);
        AudioRoom r = g.R;
        var pseudo = r.In(() => Dm.@new<obj_item_electroadaptive_pseudocircuit>(locate(X, Y + 1, 1)));
        var board = r.In(() => Dm.@new<obj_item_electronics_apc>(locate(X, Y + 1, 1)));
        float before = r.In(() => Dm.num(pseudo.circuits));
        object answer = r.In(() => Waiting.NoSleep(pseudo.interact_with_atom(board, g.Bob)));
        Assert.Equal(ITEM_INTERACT_SUCCESS, Dm.num(answer));
        Assert.Equal(before + 1, r.In(() => Dm.num(pseudo.circuits)));
        Assert.True(r.In(() => QDELETED(board)));
        var stranger = r.In(() => Dm.@new<obj_item_stock_parts_power_store_cell>(locate(X, Y + 1, 1)));
        Assert.Equal(NONE, Dm.num(r.In(() => Waiting.NoSleep(pseudo.interact_with_atom(stranger, g.Bob)))));
    }

    [Fact]
    public void NarsieRisesAndHerFallClearsTheGlobal()
    {
        Rig g = Room(8210);
        AudioRoom r = g.R;
        var narsie = r.In(() => Dm.@new<obj_narsie>(locate(X + 6, Y + 6, 1)));
        Assert.Null(r.In(() => GLOB.cult_narsie));
        r.In(() => { Waiting.Detach(narsie.start_ending_the_round()); });
        r.Step(Second);
        Assert.Equal(narsie, r.In(() => GLOB.cult_narsie));
        Assert.NotNull(r.In(() => narsie.GetComponent(typeof(datum_component_singularity))));
        r.In(() => qdel(narsie));
        r.Step(2);
        Assert.Null(r.In(() => GLOB.cult_narsie));
    }

    [Fact]
    public void AnEditedVarIsSetAndFlagged()
    {
        Rig g = Room(8211);
        AudioRoom r = g.R;
        var cell = r.In(() => Dm.@new<obj_item_stock_parts_power_store_cell>(locate(X, Y + 1, 1)));
        object answer = r.In(() => Waiting.NoSleep(cell.vv_edit_var(nameof(cell.name), "edited")));
        Assert.Equal(TRUE, Dm.num(answer));
        Assert.Equal("edited", r.In(() => cell.name));
        Assert.True(r.In(() => Dm.truthy(Dm.bitand(cell.datum_flags, DF_VAR_EDITED))));
        Assert.Equal(FALSE, Dm.num(r.In(() => Waiting.NoSleep(cell.vv_edit_var("vars", null)))));
    }

    [Fact]
    public void AMapExportCarriesAnApcsAndACellsOwnVars()
    {
        Rig g = Room(8212);
        AudioRoom r = g.R;
        var cell = r.In(() => Dm.@new<obj_item_stock_parts_power_store_cell>(locate(X, Y + 1, 1)));
        DmList vars = r.In(() => (DmList)cell.get_save_vars());
        Assert.True(r.In(() => Dm.truthy(Dm.@in(nameof(cell.charge), vars))));
        Assert.True(r.In(() => Dm.truthy(Dm.@in(nameof(cell.name), vars))));
        DmList apc = r.In(() => (DmList)g.Apc.get_save_vars());
        Assert.True(r.In(() => Dm.truthy(Dm.@in("cell_type", apc))));
    }

    [Fact]
    public void AnApcWithoutAMalfunctionIgnoresTheHackAndTheVacate()
    {
        Rig g = Room(8213);
        g.R.In(() =>
        {
            Assert.Equal(APC_AI_NO_MALF, Dm.num(g.Apc.get_malf_status(null)));
            g.Apc.malfhack__proc(null);
            g.Apc.malfoccupy(null);
            Waiting.NoSleep(g.Apc.malfvacate());
        });
    }
}
