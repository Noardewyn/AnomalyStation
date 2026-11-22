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
/// Package E12b (docs/ai/specs/engineering.md) on MetaStation through the real server and a client's own stack: an engineer takes Meta's HFR kit
/// (the nine boxes on the two tables of the atmospherics engine room) to a clear 3x3 floor in that room, multitools the core box and the
/// machine stands up with tg's directions and pictures; a multitool on the interface links the parts (their _active overlays reach the
/// client); with an empty hand the interface opens tg's Hypertorus window, where the engineer picks the hydrogen and tritium recipe,
/// starts the power, the cooling and the fuel injection and turns the heating conductor up. Fuel spawned into the fuel port and coolant
/// into the core's port heat the fusion mix past 500 K: the fusion level, the temperatures and the bars follow in the window, and the
/// started fusion locks the parts.
/// </summary>
public class HypertorusGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20; // ticks per second

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

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private static string IconOf(ReplicaWorld replica, atom a)
    {
        ReplicaEntity seen = replica.Entities.FirstOrDefault(e => e.Id == a.dm_id);
        return seen != null && replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look) ? replica.ResolveString(look.IconState) : null;
    }

    private static List<string> OverlaysOf(ReplicaWorld replica, atom a)
    {
        var shown = new List<string>();
        ReplicaEntity seen = replica.Entities.FirstOrDefault(e => e.Id == a.dm_id);
        if (seen == null || !replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look))
            return shown;
        foreach (TableRef o in look.Overlays)
            if (replica.TryGetAppearance(o, out AppearanceEntry overlay))
                shown.Add(replica.ResolveString(overlay.IconState));
        return shown;
    }

    // a clear 3x3 floor in the room (and the tile north of it, where the engineer stands at the interface): open, no dense thing, no pipe
    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density)
        && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density) || a is obj_machinery_atmospherics || a is obj_structure);

    [MetaServerFact]
    public void An_engineer_assembles_Metas_HFR_kit_links_it_and_starts_a_hydrogen_tritium_fusion_from_the_window()
    {
        var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 122, TimeoutSteps = 24000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        using var _ = w;
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        w.World.RuntimeErrorSink = e => output.WriteLine("RUNTIME: " + e);
        UiNode Content(UiWindowState window)
        {
            windows.Sync(replica.UiWindows, 1280, 720, 1);
            return windows.Get(window.Window)?.Content;
        }
        void Click(atom target)
        {
            Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, target.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
            w.Run(Second);
        }
        void DropHeld()
        {
            using (w.World.Enter())
                foreach (object held in Dm.each(me.held_items.Copy()))
                    if (held is obj_item old)
                        Waiting.NoSleep(me.dropItemToGround(old));
        }

        // Meta's kit: the four bodies on one table, the four corners and the core on the other, in the atmospherics engine room
        var boxes = In(w, () =>
        {
            var found = new List<obj_item_hfr_box>();
            w.World.Objects.ForEachLiveInIdOrder((_, o) => { if (o is obj_item_hfr_box b) found.Add(b); });
            return found;
        });
        Assert.Equal(9, boxes.Count);
        Assert.All(boxes, b => Assert.True(In(w, () => b.loc is turf t && Dm.each_cast<atom>(t.contents).Any(a => a is obj_structure_table)), "on a table"));
        obj_item_hfr_box_core coreBox = boxes.OfType<obj_item_hfr_box_core>().Single();
        area room = In(w, () => get_area(coreBox) as area);
        Assert.IsType<area_station_engineering_atmospherics_engine>(room);

        // the nearest clear 3x3 floor of the room
        (int cx, int cy, int z) = In(w, () =>
        {
            var table = (turf)coreBox.loc;
            for (int r = 2; r < 20; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        int x = (int)table.x + dx, y = (int)table.y + dy;
                        bool ok = true;
                        for (int i = -1; i <= 1 && ok; i++)
                            for (int j = -1; j <= 2 && ok; j++)
                                ok = locate(x + i, y + j, table.z) is turf t && Clear(t) && get_area(t) == room;
                        if (ok)
                            return (x, y, (int)table.z);
                    }
            return (0, 0, 0);
        });
        Assert.NotEqual(0, cx);
        output.WriteLine($"the kit's table at {In(w, () => (coreBox.loc as turf)?.x)},{In(w, () => (coreBox.loc as turf)?.y)}; the reactor at {cx},{cy}");

        // the engineer carries the boxes over and lays them out: the core in the middle, the corners on the diagonals, the bodies at the sides
        In(w, () =>
        {
            var corners = new Queue<obj_item_hfr_box>(boxes.OfType<obj_item_hfr_box_corner>());
            foreach ((int dx, int dy) in new[] { (1, 1), (1, -1), (-1, -1), (-1, 1) })
                Waiting.NoSleep(corners.Dequeue().forceMove(locate(cx + dx, cy + dy, z)));
            Waiting.NoSleep(boxes.OfType<obj_item_hfr_box_body_interface>().Single().forceMove(locate(cx, cy + 1, z)));
            Waiting.NoSleep(boxes.OfType<obj_item_hfr_box_body_moderator_input>().Single().forceMove(locate(cx + 1, cy, z)));
            Waiting.NoSleep(boxes.OfType<obj_item_hfr_box_body_waste_output>().Single().forceMove(locate(cx, cy - 1, z)));
            Waiting.NoSleep(boxes.OfType<obj_item_hfr_box_body_fuel_input>().Single().forceMove(locate(cx - 1, cy, z)));
            Waiting.NoSleep(coreBox.forceMove(locate(cx, cy, z)));
            Waiting.NoSleep(me.forceMove(locate(cx, cy - 1, z))); // on the waste output box, beside the core box
        });
        w.Run(Second);
        Assert.Equal("box_core", IconOf(replica, coreBox));

        // a multitool on the core box: the machine stands up (hfr_parts.dm:462)
        DropHeld();
        In(w, () => DevCommands.Give(me, "/obj/item/multitool"));
        Click(coreBox);
        w.Run(Second);
        var core = In(w, () => Dm.each_cast<atom>(locate(cx, cy, z).contents).OfType<obj_machinery_atmospherics_components_unary_hypertorus_core>().SingleOrDefault());
        Assert.NotNull(core);
        var hfrInterface = In(w, () => Dm.each_cast<atom>(locate(cx, cy + 1, z).contents).OfType<obj_machinery_hypertorus_interface>().Single());
        var corner = In(w, () => Dm.each_cast<atom>(locate(cx + 1, cy + 1, z).contents).OfType<obj_machinery_hypertorus_corner>().Single());
        Assert.Equal(("core", "interface", "corner"), (IconOf(replica, core), IconOf(replica, hfrInterface), IconOf(replica, corner)));
        Assert.Equal((NORTH, EAST), (In(w, () => hfrInterface.dir), In(w, () => corner.dir)));
        Assert.Empty(OverlaysOf(replica, core));

        // a multitool on the interface links the parts: their _active overlays reach the client
        In(w, () => Waiting.NoSleep(me.forceMove(locate(cx, cy + 2, z))));
        w.Run(Second);
        Click(hfrInterface);
        w.Run(Second);
        Assert.Equal(1f, In(w, () => Dm.num(core.active)));
        Assert.Contains("core_active", OverlaysOf(replica, core));
        Assert.Contains("interface_active", OverlaysOf(replica, hfrInterface));
        Assert.Contains("corner_active", OverlaysOf(replica, corner));
        Assert.Contains("fuel_input_active", OverlaysOf(replica, In(w, () => core.linked_input)));
        Assert.NotNull(In(w, () => core.soundloop));

        // an empty hand opens tg's Hypertorus window
        DropHeld();
        Click(hfrInterface);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui(), v => v.Interface == "Hypertorus");
        UiNode content = Content(window);
        Assert.Equal("Off", ByKey(content, "start_power").Text);
        Assert.Equal("No recipe selected", ByKey(content, "no-recipe").Text);

        // the hydrogen and tritium recipe, the power, the cooling, the fuel injection, the heating conductor at its maximum
        ByKey(content, "recipe:h2_t2_fuel").OnClick();
        w.Run(Second);
        Assert.Equal("h2_t2_fuel", In(w, () => core.selected_fuel?.id));
        content = Content(window);
        ByKey(content, "start_power").OnClick();
        w.Run(Second);
        content = Content(window);
        Assert.False(ByKey(content, "start_cooling").Disabled);
        ByKey(content, "start_cooling").OnClick();
        w.Run(Second);
        content = Content(window);
        ByKey(content, "start_fuel").OnClick();
        w.Run(Second);
        content = Content(window);
        ByKey(content, "fuel_injection_rate").OnChange(150); // the fastest injection, to fill the mix within the test
        w.Run(Second);
        content = Content(window);
        ByKey(content, "heating_conductor:max").OnClick();
        w.Run(Second);
        Assert.Equal((1f, 1f, 1f, 500f, 150f), In(w, () => (core.start_power, core.start_cooling, core.start_fuel, core.heating_conductor.Value, core.fuel_injection_rate.Value)));

        // fuel into the fuel port (tg's guide: about a thousand moles of each into the machine), a little coolant into the core's cooling
        // port (spawned: no pipes in this test)
        In(w, () =>
        {
            var fuel = (datum_gas_mixture)core.linked_input.airs[1];
            fuel.assert_gases(typeof(datum_gas_hydrogen), typeof(datum_gas_tritium));
            fuel.moles[typeof(datum_gas_hydrogen)] = 1200f;
            fuel.moles[typeof(datum_gas_tritium)] = 1200f;
            fuel.temperature = T20C;
            var coolant = (datum_gas_mixture)core.airs[1];
            coolant.assert_gas(typeof(datum_gas_plasma));
            coolant.moles[typeof(datum_gas_plasma)] = 50f;
            coolant.temperature = T20C;
        });

        // the reaction heats the mix past 500 K: fusion level 1, shown in the window
        float fusionTemperature = 0;
        for (int i = 0; i < 60 && In(w, () => core.power_level) < 1; i++)
        {
            w.Run(Second);
            fusionTemperature = In(w, () => core.internal_fusion.temperature);
            if (i % 5 == 0)
                output.WriteLine($"{i} s: fusion {fusionTemperature} K, fuel {In(w, () => Dm.num(core.internal_fusion.moles[typeof(datum_gas_hydrogen)]))} H2, heat output {In(w, () => core.heat_output)}, energy {In(w, () => core.energy)}");
        }
        w.Run(2 * Second);
        Assert.True(In(w, () => core.power_level) >= 1, "the fusion reaches level 1: " + fusionTemperature);
        Assert.True(In(w, () => core.internal_fusion.temperature) > 500);
        UiValue data = replica.UiWindows[window.Window].Merged;
        Assert.True(data.Num("power_level") >= 1);
        Assert.True(data.Num("internal_fusion_temperature") > 500);
        Assert.True(data.Num("energy_level") > 1e15);
        content = Content(window);
        Assert.True(ByKey(content, "power_level").Value >= 1);
        Assert.True(ByKey(content, "start_power").Disabled); // tg: the power cannot be cut at a fusion level over 0
        Assert.Contains(content.Walk(), n => n.Key == "bar:Fusion");
        Assert.Contains("start_fuel:hydrogen", content.Walk().Select(n => n.Key));
        Assert.Contains("core_active", OverlaysOf(replica, core));

        // the started fusion locks the parts: a screwdriver does not open the interface
        In(w, () => DevCommands.Give(me, "/obj/item/screwdriver"));
        Click(hfrInterface);
        Assert.Equal(0f, In(w, () => Dm.num(hfrInterface.panel_open)));

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
