using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M5;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Engineering;

/// <summary>
/// Engineering package E9: the cell charger (cell_charger.dm) in a booted toy room. A cell goes in only into an anchored, whole charger of a
/// powered area, one at a time; each machines cycle gives it charge_rate * seconds_per_tick from the grid (capped at the missing charge),
/// while the charger's pictures follow the charge (ccharger-o0..o4, the cell, its connector and its charge light); the hand takes the cell
/// out; the wrench, screwdriver and crowbar work only on an empty charger; the capacitors set the rate; the EMP reaches the cell.
/// </summary>
public class CellChargerTests
{
    private const int Second = 20;
    private const int X = 5, Y = 5;

    private static (AudioRoom R, mob_living_carbon_human Bob) Room(ulong seed, bool free = true)
    {
        var r = new AudioRoom(11, 11, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() =>
        {
            ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test");
            // the toy room has no APC: free power, as use_energy gives shuttles; or none at all
            (get_area(locate(X, Y, 1)) as area).requires_power = free ? FALSE : TRUE;
        });
        r.Step(Second);
        return (r, bob);
    }

    private static obj_machinery_cell_charger Charger(AudioRoom r)
    {
        var charger = r.In(() => Dm.@new<obj_machinery_cell_charger>(locate(X, Y, 1)));
        r.Step(Second);
        return charger;
    }

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance i ? i.icon_state : Dm.str(o)).ToList());

    private static obj_item_stock_parts_power_store_cell Cell(AudioRoom r, float charge) => r.In(() =>
    {
        var cell = Dm.@new<obj_item_stock_parts_power_store_cell_high>(locate(X - 1, Y - 1, 1));
        cell.charge = charge;
        return (obj_item_stock_parts_power_store_cell)cell;
    });

    [Fact]
    public void AnEmptyCellGoesInChargesByTgsRateAndShowsItsLevelOnTheCharger()
    {
        var (r, bob) = Room(9201);
        var charger = Charger(r);
        Assert.Equal((2500f, 1f), r.In(() => (charger.charge_rate, Dm.num(charger.anchored))));
        Assert.Empty(Overlays(r, charger)); // nothing in it, nothing drawn

        var cell = Cell(r, 0);
        Assert.Equal(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(charger.item_interaction(bob, cell, Dm.list()))));
        r.Step();
        Assert.Same(cell, r.In(() => charger.charging));
        Assert.Same(charger, r.In(() => cell.loc));
        // the pictures of tg: the level, the cell's own picture, its connector; an empty cell has no charge light
        List<string> shown = Overlays(r, charger);
        Assert.Contains("ccharger-o0", shown);
        Assert.Contains("ccharger-standard-on", shown);
        Assert.Contains(r.In(() => cell.icon_state), shown);
        Assert.DoesNotContain(shown, o => o.StartsWith("cell-standard-o"));

        // each SSmachines cycle gives charge_rate * seconds_per_tick (the cell has the room for it)
        float step = r.In(() => charger.charge_rate * Dm.num(SSmachines.wait) / 10f);
        float max = r.In(() => cell.maxcharge);
        Assert.True(step > 0 && step < max);
        var levels = new HashSet<string>();
        float last = 0;
        int cycles = 0;
        for (int i = 0; i < 400 * Second && r.In(() => cell.charge) < max; i++)
        {
            r.Step();
            float now = r.In(() => cell.charge);
            if (now != last)
            {
                cycles++;
                Assert.Equal(Math.Min(max, last + step), now, 0.01);
                last = now;
                levels.Add(Overlays(r, charger).Single(o => o.StartsWith("ccharger-o")));
            }
        }
        Assert.Equal((int)Math.Ceiling(max / step), cycles);
        Assert.Equal(max, r.In(() => cell.charge));
        // the level follows the percentage: floor(percent * 4 / 100), 0 to 4
        Assert.Equal(new[] { "ccharger-o0", "ccharger-o1", "ccharger-o2", "ccharger-o3", "ccharger-o4" }, levels.Append("ccharger-o0").Distinct().OrderBy(o => o));
        r.Step(Second);
        shown = Overlays(r, charger);
        Assert.Contains("ccharger-o4", shown);
        Assert.Contains("cell-standard-o2", shown); // charged to 99.5%: the lit charge light of tg
        Assert.Equal(max, r.In(() => cell.charge)); // a full cell takes nothing more

        // the hand takes it out; the charger is empty again and the cell's picture is redrawn
        r.In(() => Waiting.NoSleep(charger.attack_hand(bob, Dm.list())));
        r.Step();
        Assert.Null(r.In(() => charger.charging));
        Assert.Same(cell, r.In(() => bob.get_active_held_item()));
        Assert.Empty(Overlays(r, charger));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACellIsTurnedAwayFromAChargerThatIsUnanchoredBrokenOpenFullOrWithoutPower()
    {
        var (r, bob) = Room(9202);
        var charger = Charger(r);
        var cell = Cell(r, 0);
        var other = Cell(r, 0);

        // not anchored: "isn't attached to the ground!"
        r.In(() => Waiting.NoSleep(charger.set_anchored(FALSE)));
        Assert.Equal(ITEM_INTERACT_BLOCKING, r.In(() => Waiting.NoSleep(charger.item_interaction(bob, cell, Dm.list()))));
        r.In(() => Waiting.NoSleep(charger.set_anchored(TRUE)));

        // broken
        r.In(() => Waiting.NoSleep(charger.set_machine_stat(Dm.bitor(charger.machine_stat, BROKEN))));
        Assert.Equal(ITEM_INTERACT_BLOCKING, r.In(() => Waiting.NoSleep(charger.item_interaction(bob, cell, Dm.list()))));
        r.In(() => Waiting.NoSleep(charger.set_machine_stat(Dm.bitand(charger.machine_stat, Dm.bitnot(BROKEN)))));

        // an area without equipment power: "blinks red"
        r.In(() => (get_area(charger) as area).power_equip = 0);
        Assert.Equal(ITEM_INTERACT_BLOCKING, r.In(() => Waiting.NoSleep(charger.item_interaction(bob, cell, Dm.list()))));
        r.In(() => (get_area(charger) as area).power_equip = 1);
        Assert.Null(r.In(() => charger.charging));

        // a hatch open: the interaction is not the charger's (NONE), anything that is no cell too
        var screwdriver = WallTests.Hold<obj_item_screwdriver>(r, bob);
        Assert.Equal(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(charger.screwdriver_act(bob, screwdriver))));
        Assert.Equal(1f, r.In(() => charger.panel_open));
        Assert.Equal(NONE, r.In(() => Waiting.NoSleep(charger.item_interaction(bob, cell, Dm.list()))));
        r.In(() => Waiting.NoSleep(charger.screwdriver_act(bob, screwdriver)));
        Assert.Equal(NONE, r.In(() => Waiting.NoSleep(charger.item_interaction(bob, screwdriver, Dm.list()))));

        // one cell only
        Assert.Equal(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(charger.item_interaction(bob, cell, Dm.list()))));
        Assert.Equal(ITEM_INTERACT_BLOCKING, r.In(() => Waiting.NoSleep(charger.item_interaction(bob, other, Dm.list()))));
        Assert.Same(cell, r.In(() => charger.charging));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AChargerWithoutPowerGivesTheCellNothing()
    {
        var (r, bob) = Room(9203, free: false); // the area needs power and has no APC
        var charger = Charger(r);
        var cell = Cell(r, 0);
        r.In(() => (get_area(charger) as area).power_equip = 1); // let the cell in (tg's check is the area's flag only)
        Assert.Equal(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(charger.item_interaction(bob, cell, Dm.list()))));
        r.Step(20 * Second);
        Assert.Equal(0f, r.In(() => cell.charge));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheToolsWorkOnAnEmptyChargerAndTheCapacitorsSetTheRate()
    {
        var (r, bob) = Room(9204);
        var charger = Charger(r);

        // the wrench unanchors and anchors it (two seconds each)
        WallTests.Hold<obj_item_wrench>(r, bob);
        WallTests.Click(r, bob, charger, 3);
        Assert.Equal(0f, r.In(() => charger.anchored));
        WallTests.Click(r, bob, charger, 3);
        Assert.Equal(1f, r.In(() => charger.anchored));

        // with a cell inside none of the three tools does anything: wrench and screwdriver pass (NONE), the crowbar is refused
        var cell = Cell(r, 1000);
        r.In(() => Waiting.NoSleep(charger.item_interaction(bob, cell, Dm.list())));
        var wrench = r.In(() => Dm.@new<obj_item_wrench>(get_turf(bob)));
        var screwdriver = r.In(() => Dm.@new<obj_item_screwdriver>(get_turf(bob)));
        var crowbar = r.In(() => Dm.@new<obj_item_crowbar>(get_turf(bob)));
        Assert.Equal(NONE, r.In(() => Waiting.NoSleep(charger.wrench_act(bob, wrench))));
        Assert.Equal(NONE, r.In(() => Waiting.NoSleep(charger.screwdriver_act(bob, screwdriver))));
        Assert.False(Dm.truthy(r.In(() => charger.can_crowbar_deconstruct())));
        Assert.NotEqual(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(charger.crowbar_act(bob, crowbar))));
        Assert.False(r.In(() => QDELETED(charger)));
        r.In(() => Waiting.NoSleep(charger.attack_tk(bob))); // the telekinetic hand takes the cell out as well
        Assert.Null(r.In(() => charger.charging));
        Assert.Same(r.In(() => get_turf(charger)), r.In(() => cell.loc));

        // capacitors of tier 3: three times the rate of the tier 1 board's (0.25 * the standard rate)
        r.In(() =>
        {
            Assert.Equal(2500f, charger.charge_rate);
            for (int i = 1; i <= charger.component_parts.Count; i++)
                if (charger.component_parts[i] is datum_stock_part_capacitor)
                    charger.component_parts[i] = GLOB.stock_part_datums[typeof(datum_stock_part_capacitor_tier3)];
            Waiting.NoSleep(charger.RefreshParts());
            Assert.Equal(7500f, charger.charge_rate);
        });

        // empty, the screwdriver opens the hatch and the crowbar takes it apart: the board and the parts drop
        Assert.False(Dm.truthy(r.In(() => charger.can_crowbar_deconstruct()))); // the hatch is shut
        Assert.Equal(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(charger.screwdriver_act(bob, screwdriver))));
        Assert.True(Dm.truthy(r.In(() => charger.can_crowbar_deconstruct())));
        Assert.Equal(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(charger.crowbar_act(bob, crowbar))));
        r.Step(Second);
        Assert.True(r.In(() => QDELETED(charger)));
        r.In(() => Assert.Contains(Dm.each_cast<atom>(locate(X, Y, 1).contents), a => a is obj_item_circuitboard_machine_cell_charger or obj_structure_frame_machine));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheEmpReachesTheCellAndAnOpenDeconstructionDropsIt()
    {
        var (r, bob) = Room(9205);
        var charger = Charger(r);
        var cell = Cell(r, 50000);
        r.In(() => Waiting.NoSleep(charger.item_interaction(bob, cell, Dm.list())));
        r.In(() => Waiting.NoSleep(charger.emp_act(EMP_HEAVY)));
        Assert.Equal(50000f - STANDARD_CELL_CHARGE / EMP_HEAVY * 3, r.In(() => cell.charge), 0.01); // the high-capacity cell's emp_damage_modifier is 3

        // the charger destroyed takes its cell with it; deconstructed (on_deconstruction), it lets the cell fall to the floor
        r.In(() => Waiting.NoSleep(charger.deconstruct(TRUE)));
        r.Step();
        Assert.True(r.In(() => QDELETED(charger)));
        Assert.False(r.In(() => QDELETED(cell)));
        Assert.Same(r.In(() => locate(X, Y, 1)), r.In(() => cell.loc));
        r.AssertNoRuntimes();
    }
}
