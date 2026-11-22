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
/// Engineering package E13 (RCD.dm, RLD.dm, RTD.dm, RDD.dm) on MetaStation through the real server and a client's own stack: an engineer in a
/// maintenance corridor beside a wall. With a loaded RCD he lays plating on space outside the hull, raises a wall on it (his client sees
/// tg's constructing effect), takes the wall down with a right click, then, inside, picks the solid airlock in the RCD's window and builds
/// one. With the
/// RLD he mounts a light tube on the wall, picks Deconstruct in its radial and removes it. With the RTD he picks the edge tile facing east in
/// its window and lays it on the plating, then a click on that floor picks its design back. With the RDD he places a plastic decoration
/// that his client draws.
/// </summary>
public class RcdGameplayTests(ITestOutputHelper output)
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

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private static bool Clear(turf t) => t?.GetType() == typeof(turf_open_floor_plating) && !Dm.truthy(t.density)
        && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density) || a is obj_item || a is mob || a is obj_machinery_door || a is obj_machinery_light || a is obj_structure_decoration);

    // a plating tile with clear plating west, east and north of it (pipes and cables under it allowed), a wall south and no light: a
    // maintenance corridor's side
    private static (int X, int Y) Spot(RealHeadlessWorld w) => In(w, () =>
    {
        foreach (turf_open_floor_plating t in Dm.each<turf_open_floor_plating>(Dm.world).OrderBy(t => t.dm_id))
        {
            if (!Dm.eq(t.z, 1) || !Clear(t))
                continue;
            int tx = (int)t.x, ty = (int)t.y;
            if (locate(tx - 1, ty, 1) is turf a && Clear(a) && locate(tx + 1, ty, 1) is turf b && Clear(b) && locate(tx, ty + 1, 1) is turf c && Clear(c)
                && locate(tx, ty - 1, 1) is turf_closed_wall && Dm.locate<obj_machinery_light>(t) == null)
                return (tx, ty);
        }
        return (0, 0);
    });

    // an airless plating tile outside the hull with bare space east of it (no lattice) and nothing dense on it
    private static (int X, int Y) Hull(RealHeadlessWorld w) => In(w, () =>
    {
        foreach (turf_open_floor_plating_airless t in Dm.each<turf_open_floor_plating_airless>(Dm.world).OrderBy(t => t.dm_id))
        {
            int tx = (int)t.x, ty = (int)t.y;
            if (Dm.eq(t.z, 1) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density))
                && locate(tx + 1, ty, 1) is turf_open_space space && !Dm.each_cast<atom>(space.contents).Any())
                return (tx, ty);
        }
        return (0, 0);
    });

    [MetaServerFact]
    public void An_engineer_builds_and_takes_down_with_the_rcd_lights_with_the_rld_tiles_with_the_rtd_and_decorates_with_the_rdd()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 1301, TimeoutSteps = 9000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        var texts = new List<ServerText>();
        p.Client.ServerTextReceived += texts.Add;
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        (int x, int y) = Spot(w);
        Assert.NotEqual(0, x);
        output.WriteLine($"the engineer at {x},{y}; the wall south of him");
        In(w, () => Waiting.NoSleep(me.forceMove(locate(x, y, 1))));
        In(w, () => ADD_TRAIT(me, TRAIT_ADVANCEDTOOLUSER, "test"));

        T Hold<T>() where T : obj_item => In(w, () =>
        {
            foreach (object held in Dm.each(me.held_items.Copy()))
            {
                if (held is obj_item old)
                    qdel(old);
            }
            T item = Dm.@new<T>(me.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_active_hand(item))));
            return item;
        });
        UiWindowState Open(string iface)
        {
            p.Session.Keybinding(KeybindingCode.ActivateInhand);
            w.Run(Second);
            UiWindowState window = Assert.Single(replica.UiWindows.Tgui(), u => u.Interface == iface);
            return window;
        }
        UiNode Shown(UiWindowState window)
        {
            windows.Sync(replica.UiWindows, 1280, 720, 1);
            return windows.Get(window.Window).Content;
        }
        void ClickTurf(int tx, int ty, ClickButton button = ClickButton.Left) =>
            Assert.True(p.Session.Click(new ClickTarget(TargetKind.Turf, 0, tx, ty, 1), button, ClickModifiers.None));
        turf At(int tx, int ty) => In(w, () => locate(tx, ty, 1));

        // ---- the RCD outside the hull: plating on space, a wall on it, the wall taken down
        (int hx, int hy) = Hull(w);
        Assert.NotEqual(0, hx);
        output.WriteLine($"the hull's edge at {hx},{hy}, space east of it");
        In(w, () => Waiting.NoSleep(me.forceMove(locate(hx, hy, 1))));
        var rcd = Hold<obj_item_construction_rcd_loaded>();
        w.Run(Second);
        ClickTurf(hx + 1, hy);
        w.Run(Second / 2);
        Assert.IsType<turf_open_floor_plating>(At(hx + 1, hy));

        ClickTurf(hx + 1, hy);
        w.Run(Second);
        // tg's constructing effect on the tile while the wall builds, on his client too
        var effect = In(w, () => Dm.each<obj_effect_constructing_effect>(locate(hx + 1, hy, 1)).Single(e => e.icon_state != "rcd_end"));
        ReplicaEntity seen = replica.Entities.FirstOrDefault(e => e.Id == effect.dm_id);
        Assert.NotNull(seen);
        Assert.True(replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look));
        Assert.Equal(("icons/effects/rcd.dmi", "rcd_short"), (replica.ResolveResource(look.Icon), replica.ResolveString(look.IconState)));
        w.Run(2 * Second);
        Assert.IsType<turf_closed_wall>(At(hx + 1, hy));

        ClickTurf(hx + 1, hy, ClickButton.Right);
        w.Run(5 * Second);
        Assert.IsType<turf_open_floor_plating>(At(hx + 1, hy));
        Assert.Equal(160f - 3f - 16f - 26f, In(w, () => rcd.matter));
        Assert.Contains(heard, e => e.File == "sound/items/deconstruct.ogg");

        // ---- back inside, the RCD's window: an airlock
        In(w, () => Waiting.NoSleep(me.forceMove(locate(x, y, 1))));
        w.Run(Second);

        // the RCD's window: the Airlocks root, the Solid Airlocks tab, the plain airlock
        UiWindowState rcdWindow = Open("RapidConstructionDevice");
        UiNode content = Shown(rcdWindow);
        Assert.Contains("Units Left", content.Walk().Select(n => n.Label));
        Assert.Equal(TguiSpritesheets.Rcd32, ByKey(content, "icon-Floor/Wall").DmIcon);
        ByKey(content, "root-Airlocks").OnClick();
        w.Run(Second);
        content = Shown(rcdWindow);
        ByKey(content, "tab-Solid Airlocks").OnClick();
        content = Shown(rcdWindow);
        ByKey(content, "design-Airlock").OnClick();
        w.Run(Second);
        Assert.Equal(typeof(obj_machinery_door_airlock), In(w, () => rcd.rcd_design_path));
        ClickTurf(x - 1, y);
        w.Run(6 * Second);
        Assert.Single(In(w, () => Dm.each<obj_machinery_door_airlock>(locate(x - 1, y, 1)).ToList()));
        Assert.Equal(115f - 16f, In(w, () => rcd.matter));

        // ---- the RLD: a tube on the wall south of him, on his own tile, then the radial's Deconstruct takes it off
        var rld = Hold<obj_item_construction_rld>();
        w.Run(Second);
        ClickTurf(x, y - 1);
        w.Run(2 * Second);
        var tube = In(w, () => Dm.each<obj_machinery_light>(locate(x, y, 1)).Single());
        Assert.Equal(SOUTH, In(w, () => Dm.num(tube.dir)));
        Assert.Equal(190f, In(w, () => rld.matter));
        Assert.Contains(heard, e => e.File == "sound/effects/light_flicker.ogg");
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        RadialMenuMessage menu = replica.TopRadial;
        Assert.NotNull(menu);
        Assert.Equal(new[] { "Color Pick", "Glow Stick", "Deconstruct", "Light Fixture" }, menu.Elements.Select(e => e.Name).ToArray());
        Assert.True(p.Session.RadialPick(menu.MenuId, 3));
        w.Run(Second);
        Assert.Equal(3f, In(w, () => Dm.num(rld.mode)));
        Assert.True(p.Session.Click(tube.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(3 * Second);
        Assert.True(In(w, () => QDELETED(tube)));
        Assert.Equal(180f, In(w, () => rld.matter));

        // ---- the RTD: the edge tile facing east from its window, laid on the plating east of him; a click on the floor picks its design back
        var rtd = Hold<obj_item_construction_rtd_loaded>();
        w.Run(Second);
        UiWindowState rtdWindow = Open("RapidTilingDevice");
        content = Shown(rtdWindow);
        ByKey(content, "recipe-Edge").OnClick();
        w.Run(Second);
        content = Shown(rtdWindow);
        ByKey(content, "dir-east").OnClick();
        w.Run(Second);
        Assert.Equal(("Edge", (float)EAST), In(w, () => (Dm.str(rtd.selected_design.name), rtd.selected_direction)));
        ClickTurf(x + 1, y);
        w.Run(2 * Second);
        turf edge = At(x + 1, y);
        Assert.IsType<turf_open_floor_iron_edge>(edge);
        Assert.Equal(EAST, In(w, () => Dm.num(edge.dir)));
        Assert.Equal(345f, In(w, () => rtd.matter));
        content = Shown(rtdWindow);
        ByKey(content, "recipe-Base").OnClick();
        w.Run(Second);
        ClickTurf(x + 1, y);
        w.Run(Second);
        Assert.Equal("Edge", In(w, () => Dm.str(rtd.selected_design.name)));
        Assert.Contains(texts, t => t.Type == "balloon" && t.Html.Contains("tile changed to Edge", StringComparison.Ordinal));

        // ---- the RDD: a plastic grass patch north of him, drawn on his client
        var rdd = Hold<obj_item_construction_rdd_loaded>();
        w.Run(Second);
        UiWindowState rddWindow = Open("RapidDecorationDevice");
        Assert.Equal(TguiSpritesheets.Rdd32, ByKey(Shown(rddWindow), "design-Plastic Grass Patch").DmIcon);
        ClickTurf(x, y + 1);
        w.Run(2 * Second);
        var grass = In(w, () => Dm.each<obj_structure_decoration_grass_first>(locate(x, y + 1, 1)).Single());
        Assert.Equal(59f, In(w, () => rdd.matter));
        ReplicaEntity grassSeen = replica.Entities.FirstOrDefault(e => e.Id == grass.dm_id);
        Assert.NotNull(grassSeen);
        Assert.True(replica.TryGetAppearance(grassSeen.State.Appearance, out AppearanceEntry grassLook));
        Assert.Equal(In(w, () => (Dm.str(grass.icon), grass.icon_state)), (replica.ResolveResource(grassLook.Icon), replica.ResolveString(grassLook.IconState)));

        Assert.DoesNotContain(texts, t => t.Html.Contains("not ported yet", StringComparison.Ordinal));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
