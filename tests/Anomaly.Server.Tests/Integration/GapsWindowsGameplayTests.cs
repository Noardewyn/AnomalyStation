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
/// Gaps package G1 (safe.dm, iv_drip.dm, modules/lootpanel) on MetaStation through the real server and a client's own stack. A medic opens
/// medbay's IV drip with an empty hand (tg's IVDrip window) and slides its rate and flips its direction; an engineer works the vault's
/// combination safe through its window (the dial's number buttons down to the first tumbler and up to the second, tg's Safe), opens it and
/// takes the piggy bank out; a crewman alt-clicks a tile with several things on it (tg's LootPanel window) and takes one by clicking its row.
/// </summary>
public class GapsWindowsGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
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

    private static ClickTarget On(atom a) => new(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    // a clear tile next to a thing that the player can reach from (not behind a windoor)
    private static turf ClearNeighbour(atom a)
        => new[] { NORTH, SOUTH, EAST, WEST, NORTHEAST, NORTHWEST, SOUTHEAST, SOUTHWEST }.Select(d => get_step(a, d) as turf)
            .FirstOrDefault(t => t != null && Clear(t) && Dm.truthy(Waiting.NoSleep(a.Adjacent(t))));

    private static T InArea<T>(Func<area, bool> where) where T : atom_movable
    {
        for (int x = 1; x <= Dm.num(world.maxx); x++)
        {
            for (int y = 1; y <= Dm.num(world.maxy); y++)
            {
                turf t = locate(x, y, 1);
                if (t == null || get_area(t) is not area room || !where(room))
                    continue;
                T found = Dm.each_cast<atom>(t.contents).OfType<T>().Where(a => a.GetType() == typeof(T)).OrderBy(a => a.dm_id).FirstOrDefault();
                if (found != null && ClearNeighbour(found) != null)
                    return found;
            }
        }
        return null;
    }

    // the first tile of the station with several loose items on it (not in a closet or a box) that a crewman can stand next to
    private static (turf Tile, List<obj_item> Things) LootTile()
    {
        for (int x = 1; x <= Dm.num(world.maxx); x++)
        {
            for (int y = 1; y <= Dm.num(world.maxy); y++)
            {
                turf t = locate(x, y, 1);
                if (t == null || get_area(t) is not area_station)
                    continue;
                List<obj_item> things = Dm.each_cast<atom>(t.contents).OfType<obj_item>().Where(i => i.invisibility == 0 && i.mouse_opacity != MOUSE_OPACITY_TRANSPARENT).OrderBy(i => i.dm_id).ToList();
                if (things.Count >= 3 && things.Select(i => i.name).Distinct().Count() >= 2 && ClearNeighbour(things[0]) != null)
                    return (t, things);
            }
        }
        return (null, null);
    }

    private static (UiWindowState State, UiNode Content) Open(RealHeadlessWorld w, RealHeadlessWorld.Player p, TguiWindows windows, atom target, string interfaceName, ClickModifiers modifiers = ClickModifiers.None)
    {
        ReplicaWorld replica = p.Session.Replica;
        foreach (UiWindowState open in replica.UiWindows.Tgui().ToList())
            p.Session.UiClose(open.Window);
        w.Run(Second);
        Assert.True(p.Session.Click(On(target), ClickButton.Left, modifiers));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal(interfaceName, window.Interface);
        return (window, Shown(windows, replica, window.Window));
    }

    [MetaServerFact]
    public void A_medic_works_medbays_IV_drip_an_engineer_opens_the_vault_safe_by_its_dial_and_a_crewman_loots_a_tile_through_the_panel()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 93, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Crewman");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        void GoBeside(atom a) => Do(w, () =>
        {
            turf at = ClearNeighbour(a);
            Assert.NotNull(at);
            Waiting.NoSleep(me.forceMove(at));
        });

        // 1. medbay's IV drip: an empty hand opens the window; the slider sets the rate, the direction button flips it
        obj_machinery_iv_drip drip = In(w, () => InArea<obj_machinery_iv_drip>(a => a is area_station_medical));
        Assert.NotNull(drip);
        output.WriteLine(In(w, () => $"the IV drip at {drip.x},{drip.y} in {Dm.str(((area)get_area(drip)).name)}"));
        GoBeside(drip);
        w.Run(Second);
        (UiWindowState window, UiNode content) = Open(w, p, windows, drip, "IVDrip");
        Assert.Equal(400, windows.Get(window.Window).Context.WindowWidth);
        Assert.Equal(5, window.Merged.Num("transferRate"));
        Assert.Equal(0.01, window.Merged.Num("transferStep"), 3);
        Assert.Contains("Flow Rate", content.AllText());
        Assert.Contains("No container attached.", content.AllText());
        Assert.Equal("Injecting", ByKey(content, "mode").Text);
        ByKey(content, "rate-slider").OnChange(2.25);
        w.Run(Second);
        Assert.Equal(2.25f, In(w, () => drip.transfer_rate), 3);
        content = Shown(windows, replica, window.Window);
        Assert.Equal(2.25, ByKey(content, "rate-slider").Value, 3);
        ByKey(content, "mode").OnClick();
        w.Run(Second);
        Assert.Equal(IV_TAKING, In(w, () => drip.mode));
        content = Shown(windows, replica, window.Window);
        Assert.Equal("Draining", ByKey(content, "mode").Text);
        Assert.Equal("Blood into container", ByKey(content, "mode-text").Text);

        // 2. the vault's safe: the dial's buttons down to the first tumbler and up to the second; the door opens; the piggy bank comes out
        obj_structure_safe_vault safe = In(w, () => InArea<obj_structure_safe_vault>(_ => true));
        Assert.NotNull(safe);
        (int first, int second) = In(w, () => ((int)Dm.num(safe.tumblers[1]), (int)Dm.num(safe.tumblers[2])));
        output.WriteLine($"the vault safe at {In(w, () => safe.x)},{In(w, () => safe.y)}: combination {first}-{second}");
        GoBeside(safe);
        w.Run(Second);
        (window, content) = Open(w, p, windows, safe, "Safe");
        Assert.Equal((625, 800), (windows.Get(window.Window).Context.WindowWidth, windows.Get(window.Window).Context.WindowHeight));
        Assert.Equal((0, 0, 1), ((int)window.Merged.Num("dial"), (int)window.Merged.Num("open"), (int)window.Merged.Num("locked")));
        Assert.True(ByKey(content, "open").Disabled, "the lock is engaged");

        void Press(string key)
        {
            content = Shown(windows, replica, window.Window);
            ByKey(content, key).OnClick();
            w.Run(Second / 2);
        }
        // down (the "Left" buttons) from 0 to the first number: 100 - first ticks; 50s, then 10s, then 1s
        int down = (100 - first) % 100;
        for (int i = 0; i < down / 50; i++) Press("left:50");
        for (int i = 0; i < down % 50 / 10; i++) Press("left:10");
        for (int i = 0; i < down % 10; i++) Press("left:1");
        content = Shown(windows, replica, window.Window);
        Assert.Equal(first, (int)replica.UiWindows[window.Window].Merged.Num("dial"));
        // up (the "Right" buttons) to the second number; the last tick unlocks it
        int up = ((second - first) % 100 + 100) % 100;
        for (int i = 0; i < up / 50; i++) Press("right:50");
        for (int i = 0; i < up % 50 / 10; i++) Press("right:10");
        for (int i = 0; i < up % 10; i++) Press("right:1");
        content = Shown(windows, replica, window.Window);
        Assert.Equal((second, 0), ((int)replica.UiWindows[window.Window].Merged.Num("dial"), (int)replica.UiWindows[window.Window].Merged.Num("locked")));
        Assert.Equal(0f, In(w, () => safe.locked));
        Assert.False(ByKey(content, "open").Disabled);
        Press("open");
        w.Run(Second);
        content = Shown(windows, replica, window.Window);
        Assert.Equal(1f, In(w, () => safe.open ?? 0));
        Assert.Equal("Close", ByKey(content, "open").Text);
        List<UiNode> inside = content.Walk().Where(n => n.Key?.StartsWith("item:") == true).ToList();
        output.WriteLine("the safe holds: " + string.Join(", ", inside.Select(n => n.AllText())) + "; the server's contents: " + In(w, () => string.Join(", ", Dm.each_cast<atom>(safe.contents).Select(a => a.GetType().Name))));
        UiNode bank = inside.Single(n => n.AllText().Contains("piggy bank", StringComparison.Ordinal));
        bank.OnClick();
        w.Run(Second);
        Assert.IsType<obj_item_piggy_bank_vault>(In(w, () => me.get_active_held_item()));
        content = Shown(windows, replica, window.Window);
        Assert.Equal(inside.Count - 1, content.Walk().Count(n => n.Key?.StartsWith("item:") == true)); // the rest stays in the safe
        Assert.DoesNotContain(content.Walk(), n => n.Key?.StartsWith("item:") == true && n.AllText().Contains("piggy bank", StringComparison.Ordinal));

        // 3. a tile with several things on it: the alt-click lists them, a click on a row takes that one
        (turf tile, List<obj_item> things) = In(w, LootTile);
        Assert.NotNull(tile);
        string[] names = In(w, () => things.Select(i => (string)i.name).ToArray());
        output.WriteLine(In(w, () => $"the loot tile {tile.x},{tile.y} in {Dm.str(((area)get_area(tile)).name)}: {string.Join(", ", names)}"));
        GoBeside(things[0]);
        Do(w, () => Waiting.NoSleep(me.drop_all_held_items()));
        w.Run(Second);
        (window, content) = Open(w, p, windows, things[0], "LootPanel", ClickModifiers.Alt);
        Assert.Equal(300, windows.Get(window.Window).Context.WindowWidth);
        List<UiNode> rows = content.Walk().Where(n => n.Key?.StartsWith("box:") == true).ToList();
        string shown = string.Join(" | ", rows.Select(r => ByKey(r, "name").Text + ByKey(r, "amount").Text));
        output.WriteLine("the window lists: " + shown);
        Assert.True(rows.Count >= 2);
        foreach (string name in names.Distinct())
            Assert.Contains(char.ToUpperInvariant(name[0]) + name[1..], rows.Select(r => ByKey(r, "name").Text));
        Assert.Equal(In(w, () => (string)tile.name), ByKey(rows[0], "name").Text.ToLowerInvariant()); // the turf is first, as tg lists it

        // a click on the row of the first thing takes it into the hand (the grab is a click on it as the player would make)
        obj_item wanted = things[0];
        string wantedName = In(w, () => (string)wanted.name);
        UiNode row = rows.First(r => ByKey(r, "name").Text.ToLowerInvariant() == wantedName);
        w.Run(Second);
        row.OnClickWith(new UiClick(false, false, false, false));
        w.Run(Second);
        Assert.Same(wanted, In(w, () => me.get_active_held_item()));
        output.WriteLine("took " + wantedName);

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
