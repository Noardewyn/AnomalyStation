using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Package E12a on MetaStation through the real server and a client's own stack: an engineer opens Meta's crystallizer with an empty
/// hand, picks the hyper-noblium recipe, sets the gas input and turns it on in tg's Crystallizer window; the recipe's gases, spawned
/// cold into its input, are drawn in, the window's progress climbs and the crystal is made south of the machine. Then Meta's
/// electrolyzer in the incinerator room: a screwdriver opens its panel, a cell goes in, the panel closes, the Electrolyzer window
/// turns it on, and the water vapour on its tile becomes hydrogen and oxygen while the cell drains.
/// </summary>
public class CrystallizerElectrolyzerGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    private static turf Beside(atom thing)
    {
        foreach (float dir in new float[] { SOUTH, NORTH, EAST, WEST, NORTHEAST, NORTHWEST, SOUTHEAST, SOUTHWEST })
        {
            turf t = get_step(thing, dir);
            if (t != null && Clear(t))
                return t;
        }
        return null;
    }

    private static ClickTarget On(atom a) => new ClickTarget(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static void Hold(RealHeadlessWorld w, mob_living_carbon_human me, obj_item item) => In(w, () =>
    {
        foreach (object held in Dm.each(me.held_items.Copy()))
        {
            if (held is obj_item old)
                qdel(old);
        }
        if (item != null)
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(item))));
    });

    private static float Moles(datum_gas_mixture air, Type gas) => Dm.num(air.moles[gas]);

    [MetaServerFact]
    public void An_engineer_makes_a_hyper_noblium_crystal_in_the_crystallizer_and_splits_water_vapour_with_the_electrolyzer()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 61, TimeoutSteps = 12000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        // ---- Meta's crystallizer (off, no recipe)
        var c = In(w, () => Assert.Single(Dm.each<obj_machinery_atmospherics_components_binary_crystallizer>(Dm.world).ToList()));
        output.WriteLine($"the crystallizer at {In(w, () => $"{c.x},{c.y} dir {c.dir} in {Dm.str(((area)get_area(c)).name)}")}");
        In(w, () =>
        {
            Assert.Equal(0f, c.on);
            Assert.Null(c.selected_recipe);
            Assert.Equal("crystallizer-off", Dm.str(c.icon_state));
        });
        In(w, () => Waiting.NoSleep(me.forceMove(Beside(c))));
        w.Run(Second);
        Assert.True(p.Session.Click(On(c), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("Crystallizer", window.Interface);
        Assert.Equal(19, window.Merged.Arr("selected_recipes").Count); // Nothing, tg's blank base recipe and the 17 recipes
        UiNode content = Shown(windows, replica, window.Window);
        Assert.Equal((500.0, 600.0), (windows.Get(window.Window).Context.WindowWidth, windows.Get(window.Window).Context.WindowHeight));
        Assert.Contains("Select a recipe to see the requirements", content.AllText());

        // the recipe, the gas input and the power, from the window
        content.Walk().First(n => n.Kind == UiKind.Button && n.Text == "Hyper-Noblium Crystal").OnClick();
        w.Run(Second);
        content = Shown(windows, replica, window.Window);
        Assert.True(content.Walk().First(n => n.Kind == UiKind.Button && n.Text == "Hyper-Noblium Crystal").Selected);
        Assert.Contains("-85 moles of Hyper-Noblium", content.AllText());
        ByKey(content, "gas_input").OnChange(1);
        w.Run(Second);
        ByKey(Shown(windows, replica, window.Window), "power").OnClick();
        w.Run(Second);
        In(w, () =>
        {
            Assert.Equal("hyper_crystalium", c.selected_recipe?.id);
            Assert.Equal(1f, c.gas_input);
            Assert.Equal(1f, c.on);
            Assert.Equal("crystallizer-on", Dm.str(c.icon_state));
        });
        Assert.Equal("On", ByKey(Shown(windows, replica, window.Window), "power").Text);

        // the recipe's gases spawned cold into its input (airs[2]) and a cold coolant on its other port keep the mix in the window
        In(w, () =>
        {
            ((datum_gas_mixture)c.airs[2]).merge(SSair.parse_gas_string("o2=1100;hypernoblium=90;TEMP=100") as datum_gas_mixture);
            ((datum_gas_mixture)c.airs[1]).merge(SSair.parse_gas_string("n2=500;TEMP=100") as datum_gas_mixture);
            c.update_parents();
        });
        turf south = In(w, () => get_step(c, SOUTH));
        double shownProgress = 0;
        for (int i = 0; i < 80 && In(w, () => !Dm.each_cast<atom>(south.contents).OfType<obj_item_hypernoblium_crystal>().Any()); i++)
        {
            w.Run(Second / 2);
            shownProgress = Math.Max(shownProgress, ByKey(Shown(windows, replica, window.Window), "progress").Value);
        }
        output.WriteLine($"internal {In(w, () => $"{c.@internal.temperature} K, o2 {Moles(c.@internal, typeof(datum_gas_oxygen))}, nob {Moles(c.@internal, typeof(datum_gas_hypernoblium))}")}");
        var crystal = In(w, () => Dm.each_cast<atom>(south.contents).OfType<obj_item_hypernoblium_crystal>().FirstOrDefault());
        Assert.NotNull(crystal);
        Assert.True(shownProgress > 0, "the window's progress bar climbs");
        output.WriteLine($"made: {In(w, () => Dm.str(crystal.name))}");
        Assert.EndsWith("Hyper-Noblium crystal", In(w, () => Dm.str(crystal.name)));
        Assert.Contains(In(w, () => Dm.str(crystal.name).Split(' ')[0]), new[] { "Masterwork", "Supreme", "Good", "Decent", "Average", "Ok", "Poor", "Ugly", "Cracked" });
        ByKey(Shown(windows, replica, window.Window), "power").OnClick();
        w.Run(Second);
        Assert.Equal(0f, In(w, () => c.on));
        p.Session.UiClose(window.Window);
        w.Run(2);

        // ---- Meta's electrolyzer (no cell, not anchored)
        var e = In(w, () => Assert.Single(Dm.each<obj_machinery_electrolyzer>(Dm.world).ToList()));
        output.WriteLine($"the electrolyzer at {In(w, () => $"{e.x},{e.y} in {Dm.str(((area)get_area(e)).name)}")}");
        In(w, () =>
        {
            Assert.Null(e.cell);
            Assert.Equal((0f, 0f, "electrolyzer-off"), (e.on, e.anchored, Dm.str(e.icon_state)));
        });
        In(w, () => Waiting.NoSleep(me.forceMove(Beside(e))));
        w.Run(Second);
        Hold(w, me, In(w, () => Dm.@new<obj_item_screwdriver>(me.loc)));
        Assert.True(p.Session.Click(On(e), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Equal(1f, In(w, () => e.panel_open));
        var cell = In(w, () => Dm.@new<obj_item_stock_parts_power_store_cell_high>(me.loc));
        Hold(w, me, cell);
        Assert.True(p.Session.Click(On(e), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Same(cell, In(w, () => e.cell));
        Hold(w, me, In(w, () => Dm.@new<obj_item_screwdriver>(me.loc)));
        Assert.True(p.Session.Click(On(e), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Equal(0f, In(w, () => e.panel_open));

        // the window, opened with an empty hand: the cell's charge; its power button turns it on
        Hold(w, me, null);
        w.Run(Second);
        Assert.True(p.Session.Click(On(e), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("Electrolyzer", window.Interface);
        Assert.Equal(1, window.Merged.Num("hasPowercell"));
        Assert.Equal(100, window.Merged.Num("powerLevel"));
        content = Shown(windows, replica, window.Window);
        Assert.Equal((400.0, 305.0), (windows.Get(window.Window).Context.WindowWidth, windows.Get(window.Window).Context.WindowHeight));
        Assert.False(ByKey(content, "power").Disabled);
        Assert.True(ByKey(content, "eject").Disabled); // the panel is closed

        In(w, () =>
        {
            // the tile's air becomes hot vapour at about one atmosphere: no wind to push the engineer onto the floors it wets
            var t = (turf_open)e.loc;
            t.air = SSair.parse_gas_string("water_vapor=100;TEMP=350") as datum_gas_mixture;
            SSair.add_to_active(t);
        });
        float charge = In(w, () => cell.charge);
        ByKey(content, "power").OnClick();
        w.Run(3 * Second);
        In(w, () =>
        {
            Assert.Equal(1f, e.on);
            Assert.Equal(("working", "electrolyzer-working"), (e.mode, Dm.str(e.icon_state)));
            var air = ((turf_open)e.loc).air;
            output.WriteLine($"the tile: h2 {Moles(air, typeof(datum_gas_hydrogen))}, o2 {Moles(air, typeof(datum_gas_oxygen))}, h2o {Moles(air, typeof(datum_gas_water_vapor))}");
            Assert.True(Moles(air, typeof(datum_gas_hydrogen)) > 0, "hydrogen appears");
            Assert.True(cell.charge < charge, "the cell drains");
        });
        Assert.Equal("On", ByKey(Shown(windows, replica, window.Window), "power").Text);
        ByKey(Shown(windows, replica, window.Window), "power").OnClick();
        w.Run(Second);
        Assert.Equal((0f, "electrolyzer-off"), In(w, () => (e.on, Dm.str(e.icon_state))));

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
