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
/// Engineering package E15 (plumbing) on MetaStation through the real server and a client's own stack. Meta's chemical synthesizer in the
/// engineering maintenance (mapped making water) supplies the duct line that feeds the engineering showers, with the stationary water tank
/// on the same line. An engineer beside it unwrenches the synthesizer and wrenches it back, picks silicon in its ChemSynthesizer window,
/// builds a chemical tank beside the line with the plumbing constructor (its RapidPlumbingDevice window's Storage tab), lays a fluid duct
/// between the tank and the line, wrenches the tank down and sees it fill with the synthesizer's silicon and the water tank's water; his
/// client draws the tank and the duct's connections.
/// </summary>
public class PlumbingGameplayTests(ITestOutputHelper output)
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

    private static bool Free(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density) || a is mob);

    [MetaServerFact]
    public void An_engineer_plumbs_a_chemical_tank_to_the_synthesizer_with_the_plumbing_constructor_and_sees_it_fill()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 1501, TimeoutSteps = 9000 },
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
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        // Meta's one synthesizer, facing west into its duct line; the engineer stands on the line's duct west of it
        var synth = In(w, () => Dm.each<obj_machinery_plumbing_synthesizer>(Dm.world).Where(m => Dm.truthy(is_station_level(m.z))).Single()); // (the station's: a ruin may hold one)
        (int sx, int sy, int z) = In(w, () => ((int)synth.x, (int)synth.y, (int)synth.z));
        output.WriteLine($"the synthesizer at {sx},{sy},{z}");
        turf At(int tx, int ty) => In(w, () => locate(tx, ty, z));
        Assert.Equal(typeof(datum_reagent_water), In(w, () => synth.reagent_id));
        Assert.True(In(w, () => Dm.truthy(synth.anchored)));
        Assert.Single(In(w, () => Dm.each<obj_machinery_duct>(At(sx - 1, sy)).ToList()));
        var supply = In(w, () => (datum_component_plumbing)((DmList)synth.GetComponents(typeof(datum_component_plumbing)))[1]);
        var line = In(w, () => (datum_ductnet)supply.ducts[Dm.text("[]", WEST)]);
        Assert.NotNull(line);
        // the stationary water tank in the engineering showers supplies the same line
        Assert.Contains(In(w, () => Dm.each_cast<datum_component_plumbing>(line.suppliers).Select(s => s.parent).ToList()), a => a is obj_structure_reagent_dispensers_plumbed);

        // a free tile beside a duct of the line for the new duct (the engineer stands on it to build), a free tile east of it for the tank (a
        // chemical tank demands from its west side) and no duct east of that
        bool NoDuct(turf t) => !Dm.each<obj_machinery_duct>(t).Any();
        (int dx, int dy) = In(w, () =>
        {
            foreach (obj_machinery_duct lineDuct in Dm.each_cast<obj_machinery_duct>(line.ducts).OrderBy(d => d.dm_id))
            {
                foreach ((int ox, int oy) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
                {
                    int tx = (int)lineDuct.x + ox, ty = (int)lineDuct.y + oy;
                    if (locate(tx, ty, z) is turf wt && Free(wt) && NoDuct(wt) && locate(tx + 1, ty, z) is turf tt && Free(tt) && NoDuct(tt)
                        && locate(tx + 2, ty, z) is turf et && NoDuct(et))
                        return (tx, ty);
                }
            }
            return (0, 0);
        });
        Assert.NotEqual(0, dx);
        output.WriteLine($"the new duct at {dx},{dy}, the tank at {dx + 1},{dy}");

        In(w, () => Waiting.NoSleep(me.forceMove(At(sx - 1, sy))));
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
        void EmptyHands() => In(w, () =>
        {
            foreach (object held in Dm.each(me.held_items.Copy()))
            {
                if (held is obj_item old)
                    qdel(old);
            }
        });
        UiNode Shown(UiWindowState window)
        {
            windows.Sync(replica.UiWindows, 1280, 720, 1);
            return windows.Get(window.Window).Content;
        }
        void Click(atom target) => Assert.True(p.Session.Click(target.dm_id, ClickButton.Left, ClickModifiers.None));
        void ClickTurf(int tx, int ty) => Assert.True(p.Session.Click(new ClickTarget(TargetKind.Turf, 0, tx, ty, z), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);

        // ---- the wrench: the synthesizer comes loose (its supply leaves the line) and goes back down (it joins again)
        Hold<obj_item_wrench>();
        w.Run(Second);
        Click(synth);
        w.Run(4 * Second);
        Assert.False(In(w, () => Dm.truthy(synth.anchored)));
        Assert.Equal(0, In(w, () => supply.ducts.len));
        Assert.Contains(heard, e => e.File == "sound/items/tools/ratchet.ogg");
        Click(synth);
        w.Run(4 * Second);
        Assert.True(In(w, () => Dm.truthy(synth.anchored)));
        Assert.NotNull(In(w, () => supply.ducts[Dm.text("[]", WEST)]));

        // ---- the synthesizer's window: silicon
        EmptyHands();
        w.Run(Second);
        Click(synth);
        w.Run(Second);
        UiWindowState synthWindow = Assert.Single(replica.UiWindows.Tgui(), u => u.Interface == "ChemSynthesizer");
        UiNode content = Shown(synthWindow);
        Assert.True(ByKey(content, "chem-Water").Selected);
        ByKey(content, "chem-Silicon").OnClick();
        w.Run(Second);
        Assert.Equal(typeof(datum_reagent_silicon), In(w, () => synth.reagent_id));
        Assert.True(ByKey(Shown(synthWindow), "chem-Silicon").Selected);

        // ---- the plumbing constructor, beside the line: a chemical tank and a duct west of it
        In(w, () => Waiting.NoSleep(me.forceMove(At(dx, dy))));
        var rpld = Hold<obj_item_construction_plumbing>();
        w.Run(Second);
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        UiWindowState rpldWindow = Assert.Single(replica.UiWindows.Tgui(), u => u.Interface == "RapidPlumbingDevice");
        content = Shown(rpldWindow);
        Assert.Equal(TguiSpritesheets.Plumbing32, ByKey(content, "icon-chemical synthesizer").DmIcon);
        ByKey(content, "tab-Storage").OnClick();
        content = Shown(rpldWindow);
        ByKey(content, "recipe-chemical tank").OnClick();
        w.Run(Second);
        Assert.Equal(typeof(obj_machinery_plumbing_tank), In(w, () => rpld.blueprint));
        ClickTurf(dx + 1, dy);
        w.Run(4 * Second);
        var tank = In(w, () => Dm.each<obj_machinery_plumbing_tank>(At(dx + 1, dy)).Single());
        Assert.False(In(w, () => Dm.truthy(tank.anchored)));
        Assert.Contains(heard, e => e.File == "sound/machines/click.ogg");

        content = Shown(rpldWindow);
        ByKey(content, "tab-Distributors").OnClick();
        content = Shown(rpldWindow);
        ByKey(content, "recipe-fluid duct").OnClick();
        w.Run(Second);
        ClickTurf(dx, dy);
        w.Run(2 * Second);
        var duct = In(w, () => Dm.each<obj_machinery_duct>(At(dx, dy)).Single());
        // the new duct's net took the line's ducts and suppliers over (ducts.dm:93, the nets merged)
        Assert.Same(In(w, () => supply.ducts[Dm.text("[]", WEST)]), In(w, () => duct.net));
        Assert.Equal(200f - 20f - 1f, In(w, () => rpld.matter));

        // ---- the tank wrenched down joins the line from its west side and fills from both suppliers
        Hold<obj_item_wrench>();
        w.Run(Second);
        Click(tank);
        w.Run(4 * Second);
        Assert.True(In(w, () => Dm.truthy(tank.anchored)));
        string connected = In(w, () => duct.icon_state);
        Assert.EndsWith("_e", connected.Replace("_w", ""), StringComparison.Ordinal); // the duct joins the tank east of it
        w.Run(10 * Second);
        (float silicon, float water) = In(w, () => (Dm.num(((datum_reagents)tank.reagents).get_reagent_amount(typeof(datum_reagent_silicon))),
            Dm.num(((datum_reagents)tank.reagents).get_reagent_amount(typeof(datum_reagent_water)))));
        output.WriteLine($"the tank holds {silicon}u of silicon and {water}u of water");
        Assert.True(silicon > 0, "the synthesizer's silicon reaches the tank");
        Assert.True(water > 0, "the stationary water tank's water reaches the tank");

        // his client draws the tank and the duct's connections
        ReplicaEntity tankSeen = replica.Entities.FirstOrDefault(e => e.Id == tank.dm_id);
        Assert.NotNull(tankSeen);
        Assert.True(replica.TryGetAppearance(tankSeen.State.Appearance, out AppearanceEntry tankLook));
        Assert.Equal(("icons/obj/pipes_n_cables/hydrochem/plumbers.dmi", "tank"), (replica.ResolveResource(tankLook.Icon), replica.ResolveString(tankLook.IconState)));
        ReplicaEntity ductSeen = replica.Entities.FirstOrDefault(e => e.Id == duct.dm_id);
        Assert.NotNull(ductSeen);
        Assert.True(replica.TryGetAppearance(ductSeen.State.Appearance, out AppearanceEntry ductLook));
        Assert.Equal(connected, replica.ResolveString(ductLook.IconState));

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
