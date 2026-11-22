using Anomaly.Client.Shared;
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
/// M7 power helpers slice (D164): tg's rapid pipe cleaner layer (RCL.dm, with pipecleaners.dm and the persistent radial) through
/// the real server on MetaStation. An engineer picks a loaded layer up from a corridor floor (the pickup hooks his moves),
/// wields it with Z (both hands: the layer is two-handed) and walks east along the corridor: his client sees a pipe cleaner
/// laid behind him on each tile he leaves (a stub on the tile he enters, joined into a wire when he walks on, one piece each).
/// The layer's colour action turns the wire pink; its menu action opens tg's persistent radial of the eight wire directions, a pick
/// lays a stub at his feet and the menu stays up for the next pick, and the centre closes it.
/// </summary>
public class RclGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const double East = -Math.PI / 2; // Godot's yaw: 0 looks north, +90 degrees west

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

    private static bool Clear(turf t) => t is turf_open_floor && !Dm.truthy(t.density) && !Dm.each<atom_movable>(t).Any(a => Dm.truthy(a.density) || a is obj_machinery);

    [MetaServerFact]
    public void A_wielded_layer_lays_pipe_cleaner_behind_its_holder_and_its_menu_and_colour_actions_work()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 97, TimeoutSteps = 12000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;

        // a corridor: seven clear floor tiles in a row, and a clear row of floor north of its first five for the menu's pick
        (int X, int Y) start = In(w, () => Dm.each<turf_open_floor>(Dm.world)
            .Where(t => Dm.eq(t.z, 1) && Clear(t))
            .Select(t => ((int)Dm.num(t.x), (int)Dm.num(t.y)))
            .Where(c => Enumerable.Range(0, 7).All(i => Clear(locate(c.Item1 + i, c.Item2, 1))))
            .OrderBy(c => Math.Abs(c.Item1 - 46) + Math.Abs(c.Item2 - 122))
            .First());
        output.WriteLine($"the corridor from {start.X},{start.Y}");
        var rcl = In(w, () =>
        {
            Waiting.NoSleep(me.forceMove(locate(start.X, start.Y, 1)));
            return Dm.@new<obj_item_rcl_pre_loaded>(locate(start.X, start.Y, 1));
        });
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        // the layer picked up from the floor (attack_hand: pickup hooks the holder's moves) and wielded with Z
        Assert.True(p.Session.Click(rcl.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Same(rcl, In(w, () => me.get_active_held_item()));
        Assert.Same(me, In(w, () => rcl.listeningTo));
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(rcl.active)), "wielded");
        float full = In(w, () => Dm.num(rcl.loaded.amount));
        Assert.Equal(90f, full);

        // the layer's two actions are buttons on the client's HUD, named by tg
        var buttons = In(w, () => Dm.each_cast<datum_action>(rcl.actions).Select(a => (a.name as string, (atom_movable_screen_movable_action_button)a.viewers[me.hud_used])).ToList());
        Assert.Equal(new[] { "Change Cable Color", "Toggle Fast Wiring Gui" }, buttons.Select(b => b.Item1).ToArray());
        Assert.All(buttons, b => Assert.Contains(replica.Screen.Values, e => e.Id == b.Item2.dm_id));

        // walking east: the wire is laid behind him
        p.Yaw = East;
        p.Keys = MoveKeys.Forward;
        for (int i = 0; i < 6 * Second && In(w, () => me.x) < start.X + 4; i++)
            w.Run(1);
        p.Keys = MoveKeys.None;
        w.Run(Second);
        int x = In(w, () => (int)Dm.num(me.x));
        output.WriteLine($"the engineer is at {x},{In(w, () => me.y)}");
        Assert.True(x >= start.X + 4);
        List<obj_structure_pipe_cleaner> Wires(int tx) => In(w, () => Dm.each<obj_structure_pipe_cleaner>(locate(tx, start.Y, 1)).ToList());
        string Icon(atom a)
        {
            ReplicaEntity seen = replica.Entities.FirstOrDefault(e => e.Id == a.dm_id);
            return seen != null && replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look) ? replica.ResolveString(look.IconState) : null;
        }
        for (int tx = start.X + 1; tx < x; tx++)
        {
            obj_structure_pipe_cleaner wire = Assert.Single(Wires(tx));
            Assert.Equal("4-8", In(w, () => wire.icon_state)); // a wire from the east to the west, joined when he walked on
            Assert.Equal("4-8", Icon(wire)); // and the client draws it so
            ReplicaEntity seen = replica.Entities.First(e => e.Id == wire.dm_id);
            Assert.True(replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look));
            Assert.Equal("icons/obj/pipes_n_cables/pipe_cleaner.dmi", replica.ResolveResource(look.Icon));
            Assert.Equal(CellState.ParseColor((string)In(w, () => wire.color)), look.Rgba);
        }
        Assert.Equal("0-8", In(w, () => Assert.Single(Wires(x)).icon_state)); // the newest stub, on the tile he stands on
        float used = full - In(w, () => Dm.num(rcl.loaded.amount));
        output.WriteLine($"{used} pieces used for {x - start.X} tiles");
        Assert.Equal(2 * (x - start.X) - 1, used); // a stub for each tile entered and a join for each tile left

        // the colour action: the next colour is pink (red is the layer's first, then yellow, green, blue, pink): four more clicks
        string colour = In(w, () => rcl.loaded.pipecleaner_color);
        Assert.Equal("red", colour);
        for (int i = 0; i < 4; i++)
        {
            Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, buttons[0].Item2.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
            w.Run(Second / 2);
        }
        Assert.Equal("pink", In(w, () => rcl.loaded.pipecleaner_color));
        Assert.Equal(5f, In(w, () => rcl.current_color_index));

        // the menu action: tg's persistent radial of the eight wire directions on the client
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, buttons[1].Item2.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        RadialMenuMessage menu = replica.TopRadial;
        Assert.NotNull(menu);
        Assert.Equal(8, menu.Elements.Count);
        Assert.True(menu.Flags.HasFlag(RadialMenuFlags.CloseButton), "the centre closes it");
        Assert.Equal(new[] { "1", "5", "4", "6", "2", "10", "8", "9" }, menu.Elements.Select(e => e.Name).ToArray());
        float before = In(w, () => Dm.num(rcl.loaded.amount));
        // a pick of the direction 2 (south): a pink stub at his feet, and the menu is still on his client afterwards
        int stubs = In(w, () => Dm.each<obj_structure_pipe_cleaner>(me.loc).Count());
        Assert.True(p.Session.RadialPick(menu.MenuId, 5));
        w.Run(Second);
        In(w, () =>
        {
            var here = Dm.each<obj_structure_pipe_cleaner>(me.loc).ToList();
            Assert.Equal(stubs + 1, here.Count);
            Assert.Contains(here, c => c.d1 == 0 && c.d2 == 2 && c.pipecleaner_color == "pink");
            Assert.Equal(before - 1, Dm.num(rcl.loaded.amount));
            Assert.NotNull(rcl.wiring_gui_menu);
        });
        Assert.NotNull(replica.TopRadial);
        Assert.Equal(menu.MenuId, replica.TopRadial.MenuId); // DV79: sent again after the pick, the menu stays open
        // the centre: closed
        Assert.True(p.Session.RadialPick(menu.MenuId, 0));
        w.Run(Second);
        Assert.Null(In(w, () => rcl.wiring_gui_menu));
        Assert.Null(replica.TopRadial);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
