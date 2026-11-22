using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M5 slice 2 through the real server on MetaStation: tg's stacks. In the Auxiliary Base Construction room (plan section 4) the
/// player takes an iron stack from the table at (51,149) and uses it in hand (Z): the client gets tg's "StackCrafting" window
/// (protocol 16) with tg's recipes; the window's "floor tile" x5 button builds 20 floor tiles into the other hand (4 a sheet);
/// a right click on the held stack with an empty hand asks tg's number input (the "NumberInputModal" window) and splits off the
/// amount; the "stool" recipe builds a stool on the player's tile (an inert result where its type is not ported, D132). A glass
/// shard on the floor cuts the barefoot player who steps onto its tile and plays tg's glass step sound.
/// </summary>
public class StackGameplayTests
{
    private const int Second = 20; // ticks per second
    private const int TableX = 51, TableY = 149; // the aux base table: iron x2 fifty and glass fifty
    private const string GlassStep = "sound/effects/footstep/glass_step.ogg";

    private readonly ITestOutputHelper _output;

    public StackGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Put(RealHeadlessWorld w, mob m, int x, int y)
    {
        using (w.World.Enter())
            Waiting.NoSleep(m.forceMove(Builtins.locate(x, y, m.z)));
        w.Run(Second);
    }

    private static List<T> On<T>(RealHeadlessWorld w, int x, int y, int z)
        => In(w, () => Dm.each_cast<atom>(((turf)Builtins.locate(x, y, z)).contents).OfType<T>().ToList());

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    [MetaServerFact]
    public void An_iron_stack_crafts_tiles_and_a_stool_from_tgs_window_and_splits_and_a_shard_cuts_a_bare_foot()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 52, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        int z = In(w, () => (int)me.z);
        int bootRuntimes = w.World.RuntimeErrorCount;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };

        // The aux base table holds two full iron stacks (they do not merge: both are full).
        List<obj_item_stack_sheet_iron> irons = On<obj_item_stack_sheet_iron>(w, TableX, TableY, z);
        _output.WriteLine($"iron on the table: {string.Join(", ", irons.Select(i => In(w, () => i.amount)))}");
        Assert.Equal(2, irons.Count);
        Assert.All(irons, i => Assert.Equal(50f, In(w, () => Dm.num(i.amount))));
        Assert.Equal("sheet-metal_3", In(w, () => irons[0].icon_state)); // stack.dm:234, a full stack
        Put(w, me, TableX - 1, TableY - 1);

        // Take one: a click with the empty hand picks it up.
        obj_item_stack_sheet_iron iron = irons[0];
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, iron.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(me.is_holding(iron))), "the iron is in hand");

        // Use it in hand (Z): tg's StackCrafting window.
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        _output.WriteLine($"window {window.Window}: {window.Interface} '{window.Title}' status {window.Status}");
        Assert.Equal("StackCrafting", window.Interface);
        Assert.Equal(50, window.Merged.Num("amount"));
        UiNode content = Shown(windows, replica, window.Window);
        Assert.NotNull(content);
        UiNode tileRecipe = content.Walk().First(n => n.Key == "floor tile");
        Assert.Contains("4x Floor Tile", tileRecipe.AllText());

        // The floor tile's x5 button: tg's act("make", {ref, multiplier: 5}) -> 20 tiles in the other hand, 45 sheets left.
        tileRecipe.Walk().Single(n => n.Key == "x5").OnClick();
        w.Run(Second);
        Assert.Equal(45f, In(w, () => Dm.num(iron.amount)));
        var tiles = In(w, () => Dm.each_cast<obj_item>(me.held_items).OfType<obj_item_stack_tile_iron_base>().ToList());
        Assert.Single(tiles);
        Assert.Equal(20f, In(w, () => Dm.num(tiles[0].amount)));
        Assert.Equal(45, replica.UiWindows[window.Window].Merged.Num("amount")); // ui_data follows the amount

        // Split: swap to the tile hand, drop the tiles, right click the held iron with the empty hand: tg's number input.
        p.Session.Keybinding(KeybindingCode.SwapHands);
        w.Run(2);
        p.Session.Keybinding(KeybindingCode.DropItem);
        w.Run(Second);
        Assert.Null(In(w, () => me.get_active_held_item()));
        // the held stack is on the player's screen (its hand slot, client.screen): a screen click
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, iron.dm_id, 0, 0, 0, 16, 16), ClickButton.Right, ClickModifiers.None));
        w.Run(Second);
        UiWindowState number = replica.UiWindows.Values.Single(v => v.Interface == "NumberInputModal");
        _output.WriteLine($"number window {number.Window}: '{number.Title}': {number.Merged.ToJson()}");
        Assert.Equal("Stack Split", number.Merged.Str("title")); // tgui_input_number sends its title as data (the window has none)
        Assert.Equal(45, number.Merged.Num("max_value"));
        Assert.True(p.Session.UiAct(number.Window, "submit", UiValue.ObjectOf(("entry", UiValue.Of(12)))));
        w.Run(Second);
        var split = In(w, () => me.get_active_held_item() as obj_item_stack_sheet_iron);
        Assert.NotNull(split);
        Assert.NotSame(iron, split);
        Assert.Equal(12f, In(w, () => Dm.num(split.amount)));
        Assert.Equal(33f, In(w, () => Dm.num(iron.amount)));

        // A stool from the remaining iron through the window (the stool's type is not ported yet: an inert stool, D132).
        UiNode stool = Shown(windows, replica, window.Window).Walk().First(n => n.Key == "stool");
        stool.OnClick();
        w.Run(Second);
        var stools = On<obj_structure_chair_stool>(w, TableX - 1, TableY - 1, z);
        Assert.Single(stools);
        Assert.Equal(32f, In(w, () => Dm.num(iron.amount)));
        // CRAFT_ONE_PER_TURF: a second stool on the same tile is refused
        Shown(windows, replica, window.Window).Walk().First(n => n.Key == "stool").OnClick();
        w.Run(Second);
        Assert.Single(On<obj_structure_chair_stool>(w, TableX - 1, TableY - 1, z));
        Assert.Equal(32f, In(w, () => Dm.num(iron.amount)));

        // A glass shard on the floor of the room, the player barefoot: stepping on it cuts the foot and sounds.
        int sx = TableX - 3, sy = TableY - 1;
        using (w.World.Enter())
        {
            if (me.shoes != null)
                qdel(me.shoes);
            Dm.@new<obj_item_shard>(Builtins.locate(sx, sy, z));
        }
        w.Run(Second);
        Assert.Null(In(w, () => me.shoes));
        Put(w, me, sx + 1, sy);
        float before = In(w, () => Dm.num(me.get_brute_loss()));
        heard.Clear();
        Put(w, me, sx, sy); // the step onto the shard's tile: its turf's Entered (connect_loc) reaches the caltrop and the shard
        Assert.Equal(sx, In(w, () => (int)me.x));
        float after = In(w, () => Dm.num(me.get_brute_loss()));
        _output.WriteLine($"brute {before} -> {after}; sounds: {string.Join(", ", heard.Select(e => e.File))}");
        Assert.True(after > before, "the shard cut the bare foot");
        Assert.Contains(heard, e => e.Kind == SoundKind.Play && e.File == GlassStep);

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    private static void qdel(object o) => GlobalProcs.qdel(o);
}
