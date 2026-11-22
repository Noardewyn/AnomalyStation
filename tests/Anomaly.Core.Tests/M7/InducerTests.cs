using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M5;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// M7 power helpers slice: tg's inducer (game/objects/items/tools/inducer.dm). It charges the cell of the clicked thing (an
/// APC's, a cell's own) from its own cell, one do_after of a second per transfer of at most rating_base * rating *
/// power_transfer_multiplier, sparking and updating both appearances; its battery compartment opens with a screwdriver, the cell
/// comes out with an empty hand use and goes in with the cell, and plasma sheets recharge it.
/// </summary>
public class InducerTests
{
    private const int Second = 20;
    private const int ApcX = 3, ApcY = 2;
    private const string Left = "icon-x=16;icon-y=16;left=1;button=left";

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Bob, obj_machinery_power_apc Apc);

    private static Rig Build(ulong seed)
    {
        obj_machinery_power_apc apc = null;
        var r = new AudioRoom(13, 10, seed: seed, mapExtras: w =>
        {
            ((area)GLOB.areas_by_type[typeof(area_misc_testroom)]).requires_power = TRUE;
            for (int x = 2; x <= 5; x++)
                Dm.@new<obj_structure_cable>(locate(x, ApcY, 1));
            apc = Dm.@new<obj_machinery_power_apc_auto_name_directional_south>(locate(ApcX, ApcY, 1));
        });
        mob_living_carbon_human bob = r.Join(ApcX, ApcY + 1, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return new Rig(r, bob, apc);
    }

    private static void Click(AudioRoom r, mob who, atom target, float seconds = 1)
    {
        r.Command(() => ClientSeam.Click(who.client, target, Left));
        r.Step(Math.Max(2, (int)(seconds * Second)));
    }

    private static List<string> Overlays(AudioRoom r, atom a)
        => r.In(() => Dm.each(a.managed_overlays is DmList l ? l : Dm.list(a.managed_overlays)).Select(Describe).Where(s => s != null).ToList());

    private static string Describe(object o) => o switch
    {
        string s => s,
        mutable_appearance m => m.icon_state,
        DmAppearance d => d.icon_state,
        null => null,
        _ => o.GetType().Name + ":" + o,
    };

    // the activate_inhand keybinding (Z): the held item's attack_self
    private static void UseInHand(AudioRoom r, mob_living_carbon_human who)
    {
        r.Command(() => ClientSeam.Keybinding(who.client, 4, true));
        r.Command(() => ClientSeam.Keybinding(who.client, 4, false));
        r.Step(2);
    }

    /// <summary>Gives <paramref name="who"/> a second item in the other hand.</summary>
    private static T Give<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item => r.In(() =>
    {
        T item = Dm.@new<T>(get_turf(who));
        Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(item, merge_stacks: FALSE))), "the item reaches a hand");
        return item;
    });

    [Fact]
    public void AnInducerChargesAnApcCellAtTgsRateOneDoAfterAtATime()
    {
        var g = Build(7701);
        var inducer = WallTests.Hold<obj_item_inducer>(g.R, g.Bob);
        (float Own, float Max, float PerSecond) start = g.R.In(() =>
        {
            Assert.IsType<obj_item_stock_parts_power_store_battery_high>(inducer.powerdevice_value);
            Assert.Same(inducer, inducer.powerdevice_value.loc);
            Assert.False(Dm.truthy(inducer.opened));
            Assert.Equal(inducer.powerdevice_value, inducer.get_cell());
            obj_item_stock_parts_power_store apcCell = g.Apc.cell;
            apcCell.charge = 0;
            return (inducer.powerdevice_value.charge, apcCell.maxcharge, apcCell.rating_base * (float)Dm.num(apcCell.rating));
        });
        float spt = g.R.In(() => Dm.num(SSmachines.wait) / 10);
        // not an instant action: nothing moves before the first second of do_after has passed
        g.R.Command(() => ClientSeam.Click(g.Bob.client, g.Apc, Left));
        g.R.Step(Second / 2);
        Assert.True(g.R.In(() => g.Apc.cell.charge) < 1000f, "no transfer yet");
        Assert.Equal(start.Own, g.R.In(() => inducer.powerdevice_value.charge));
        Assert.True(g.R.In(() => Dm.truthy(inducer.recharging)));
        g.R.Step(Second);
        // the first transfer: min(own charge, used charge, rating_base * rating * multiplier) = the cell's rating per second
        float after1 = g.R.In(() => g.Apc.cell.charge);
        float own1 = g.R.In(() => inducer.powerdevice_value.charge);
        Assert.Equal(start.PerSecond, after1, 0);
        Assert.Equal(start.Own - start.PerSecond, own1, 0);
        g.R.Step(Second);
        Assert.Equal(2 * start.PerSecond, g.R.In(() => g.Apc.cell.charge), 0);
        // the inducer's own cell is drawn by what the APC took
        Assert.Equal(start.Own - 2 * start.PerSecond, g.R.In(() => inducer.powerdevice_value.charge), 0);
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AnInducerRunsUntilItsOwnCellIsEmptyThenStopsAndTheApcOverlayFollowsTheCharge()
    {
        var g = Build(7702);
        var inducer = WallTests.Hold<obj_item_inducer>(g.R, g.Bob);
        g.R.In(() =>
        {
            g.Apc.cell.charge = 0;
            inducer.powerdevice_value.charge = 100000; // two seconds' worth is not available: the last transfer is the remainder
        });
        List<string> before = Overlays(g.R, g.Apc);
        Click(g.R, g.Bob, g.Apc, 1.5f);
        Click(g.R, g.Bob, g.Apc, 0.1f);
        g.R.Step(2 * Second);
        g.R.In(() =>
        {
            Assert.False(Dm.truthy(inducer.recharging), "the loop ended when the inducer's cell was empty (transferred = 0)");
            Assert.Equal(0f, inducer.powerdevice_value.charge, 0);
            Assert.True(g.Apc.cell.charge >= 99000f); // the APC's own channels may have drawn a little meanwhile
        });
        // the APC's charge light is drawn from the cell (apc_appearance.dm): its overlays were recomputed by update_appearance
        Assert.NotEmpty(Overlays(g.R, g.Apc));
        Assert.Equal(before.Count > 0, Overlays(g.R, g.Apc).Count > 0);
        // a drained inducer refuses: "no charge!"
        Click(g.R, g.Bob, g.Apc);
        Assert.False(g.R.In(() => Dm.truthy(inducer.recharging)));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AnInducerChargesACellInTheOtherHand()
    {
        var g = Build(7703);
        var inducer = WallTests.Hold<obj_item_inducer>(g.R, g.Bob);
        var cell = Give<obj_item_stock_parts_power_store_cell_high>(g.R, g.Bob);
        float perSecond = g.R.In(() =>
        {
            cell.charge = 0;
            return cell.rating_base * (float)Dm.num(cell.rating);
        });
        Assert.True(perSecond > 0);
        float own = g.R.In(() => inducer.powerdevice_value.charge);
        // the active hand is the inducer's: clicking the cell in the other hand is the item on an item
        g.R.Command(() => ClientSeam.Click(g.Bob.client, cell, Left));
        g.R.Step((int)(2.1f * Second));
        float given = g.R.In(() => cell.charge);
        Assert.True(given > 0 && given <= 2 * perSecond + 0.5f, "given " + given);
        Assert.Equal(own - given, g.R.In(() => inducer.powerdevice_value.charge), 0);
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AMovedChargerStopsTheTransfer()
    {
        var g = Build(7704);
        var inducer = WallTests.Hold<obj_item_inducer>(g.R, g.Bob);
        g.R.In(() => g.Apc.cell.charge = 0);
        g.R.Command(() => ClientSeam.Click(g.Bob.client, g.Apc, Left));
        g.R.Step(Second / 2);
        g.R.In(() => Waiting.NoSleep(((atom_movable)g.Bob).forceMove(locate(ApcX + 1, ApcY + 1, 1))));
        g.R.Step(3 * Second);
        g.R.In(() =>
        {
            Assert.False(Dm.truthy(inducer.recharging), "the do_after failed and the loop ended");
            Assert.True(g.Apc.cell.charge < 1000f, "nothing was transferred");
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ThePanelOpensWithAScrewdriverTheCellComesOutAndGoesBackAndPlasmaRechargesIt()
    {
        var g = Build(7705);
        var inducer = WallTests.Hold<obj_item_inducer>(g.R, g.Bob);
        Assert.DoesNotContain("inducer-bat", Overlays(g.R, inducer));
        var driver = Give<obj_item_screwdriver>(g.R, g.Bob);
        // the screwdriver on the closed inducer opens its battery compartment (screwdriver_act, use_tool with no delay)
        g.R.In(() => g.Bob.swap_hand());
        Click(g.R, g.Bob, inducer);
        Assert.True(g.R.In(() => Dm.truthy(inducer.opened)));
        Assert.Contains("inducer-bat", Overlays(g.R, inducer));
        // opened, it will not charge ("close first!")
        g.R.In(() => g.Apc.cell.charge = 0);
        g.R.In(() => g.Bob.swap_hand());
        g.R.Command(() => ClientSeam.Click(g.Bob.client, g.Apc, Left));
        g.R.Step(2 * Second);
        Assert.False(g.R.In(() => Dm.truthy(inducer.recharging)));
        Assert.True(g.R.In(() => g.Apc.cell.charge) < 1000f);
        // use in hand: the cell comes out into the other hand (attack_self)
        obj_item_stock_parts_power_store cell = g.R.In(() => inducer.powerdevice_value);
        g.R.In(() => qdel(driver));
        g.R.Step(2);
        UseInHand(g.R, g.Bob);
        g.R.In(() =>
        {
            Assert.Null(inducer.powerdevice_value);
            Assert.Null(inducer.get_cell());
            Assert.True(Dm.truthy(g.Bob.is_holding(cell)));
        });
        Assert.Contains("inducer-nobat", Overlays(g.R, inducer));
        // the cell goes back in with a click of the cell on the open inducer (item_interaction)
        g.R.In(() => g.Bob.swap_hand());
        Click(g.R, g.Bob, inducer);
        g.R.In(() =>
        {
            Assert.Same(cell, inducer.powerdevice_value);
            Assert.Same(inducer, cell.loc);
        });
        // tg's item_interaction (inducer.dm:131) does not update the overlays after the cell goes in: the picture follows at the
        // next panel toggle
        Assert.Contains("inducer-nobat", Overlays(g.R, inducer));
        g.R.In(() => Waiting.NoSleep(g.Bob.drop_all_held_items()));
        g.R.In(() => Waiting.NoSleep(g.Bob.put_in_hands(inducer, merge_stacks: FALSE)));
        var driver2 = Give<obj_item_screwdriver>(g.R, g.Bob);
        g.R.In(() => g.Bob.swap_hand());
        Click(g.R, g.Bob, inducer);
        Assert.False(g.R.In(() => Dm.truthy(inducer.opened)));
        Assert.Empty(Overlays(g.R, inducer));
        Click(g.R, g.Bob, inducer);
        Assert.Contains("inducer-bat", Overlays(g.R, inducer));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AnEmptyInducerTakesAPlasmaSheetToRechargeItsCellAtOneAndAHalfStandardCells()
    {
        var g = Build(7706);
        var inducer = g.R.In(() =>
        {
            var i = Dm.@new<obj_item_inducer_empty>(get_turf(g.Bob));
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Bob.put_in_hands(i, merge_stacks: FALSE))));
            return i;
        });
        g.R.In(() =>
        {
            Assert.True(Dm.truthy(inducer.opened));
            Assert.Null(inducer.powerdevice_value);
        });
        Assert.DoesNotContain("inducer-bat", Overlays(g.R, inducer));
        Assert.Contains("inducer-nobat", Overlays(g.R, inducer));
        var cell = Give<obj_item_stock_parts_power_store_battery_high>(g.R, g.Bob);
        g.R.In(() => cell.charge = 0);
        // the cell goes in
        g.R.In(() => g.Bob.swap_hand());
        Click(g.R, g.Bob, inducer);
        g.R.In(() => Assert.Same(cell, inducer.powerdevice_value));
        // a plasma sheet in the free hand recharges it by 1.5 standard cells and is used up by one
        var sheets = Give<obj_item_stack_sheet_mineral_plasma>(g.R, g.Bob);
        g.R.In(() => sheets.amount = 3);
        Click(g.R, g.Bob, inducer);
        Assert.Equal(1.5f * STANDARD_CELL_CHARGE, g.R.In(() => cell.charge), 0);
        Assert.Equal(2f, g.R.In(() => Dm.num(sheets.amount)));
        g.R.AssertNoRuntimes();
    }
}
