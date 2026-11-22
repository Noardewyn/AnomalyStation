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
/// M5 slice 8 through the real server on MetaStation: tg's crafting menu and slapcrafting. In the Auxiliary Base Construction room
/// (plan section 4) the player holds an iron stack and a rods stack from the room's table; a click on the HUD's craft button (tg's
/// /atom/movable/screen/craft at ui_human_crafting) opens tg's "PersonalCrafting" window (protocol 16) with tg's recipes, the
/// skateboard among the craftable ones; the window's Make button runs tg's construct_item: a 6 s do_after, then an (inert, D132)
/// improvised skateboard on the player's tile, 5 iron and 10 rods used up. Then a slapcraft: the rods on the table hit with a glass
/// shard, cable restraints on the floor, make tg's spear (an inert result) and use up the shard, the restraints and a rod.
/// </summary>
public class CraftingGameplayTests
{
    private const int Second = 20; // ticks per second
    private const int TableX = 51, TableY = 149; // the aux base table: iron x2 fifty and glass fifty
    private const int RodsX = 51, RodsY = 148; // rods fifty and plasteel 10

    private readonly ITestOutputHelper _output;

    public CraftingGameplayTests(ITestOutputHelper output) => _output = output;

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

    private static void PickUp(RealHeadlessWorld w, RealHeadlessWorld.Player p, mob me, obj_item item)
    {
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, item.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(me.is_holding(item))), item + " is in hand");
    }

    [MetaServerFact]
    public void The_craft_button_opens_tgs_crafting_window_which_builds_a_skateboard_and_rods_slapcraft_a_spear()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 58, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        int z = In(w, () => (int)me.z);
        int bootRuntimes = w.World.RuntimeErrorCount;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };

        // Stand between the table and the rods; take an iron stack in one hand and the rods in the other.
        Put(w, me, TableX - 1, TableY - 1);
        obj_item_stack_sheet_iron iron = On<obj_item_stack_sheet_iron>(w, TableX, TableY, z)[0];
        obj_item_stack_rods rods = On<obj_item_stack_rods>(w, RodsX, RodsY, z)[0];
        PickUp(w, p, me, iron);
        p.Session.Keybinding(KeybindingCode.SwapHands);
        w.Run(2);
        PickUp(w, p, me, rods);
        float ironBefore = In(w, () => Dm.num(iron.amount)), rodsBefore = In(w, () => Dm.num(rods.amount));

        // The HUD's craft button (tg's screen object, drawn by the client's HUD at ui_human_crafting): a screen click.
        var button = In(w, () => me.hud_used.screen_objects[Defines.HUD_MOB_CRAFTING_MENU] as atom_movable_screen_craft);
        Assert.NotNull(button);
        Assert.True(In(w, () => Dm.@in(button, me.client.screen)), "the craft button is on the player's screen");
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, button.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        _output.WriteLine($"window {window.Window}: {window.Interface} '{window.Title}', {window.Merged.Arr("recipes").Count} recipes, {window.Merged.Arr("categories").Count} categories");
        Assert.Equal("PersonalCrafting", window.Interface);
        Assert.Equal("Crafting", window.Title);
        UiValue skateboard = window.Merged.Arr("recipes").First(r => r.Str("name") == "Skateboard");
        string reference = skateboard.Str("ref");
        Assert.True(window.Merged.Get("craftability")?.Get(reference)?.Truthy ?? false, "the skateboard is craftable from the held stacks");

        // The owner's crash (2026-10-16): every category of tg's real recipes renders, and each ingredient's floating recipe opens
        // three levels deep (built when opened: tg's recipes have ingredients that are made from each other).
        var browse = new TguiLocalState();
        int floats = 0;
        void Open(UiNode node, int depth)
        {
            foreach (UiNode f in node.Walk().Where(n => n.Kind == UiKind.Floating).ToList())
            {
                floats++;
                if (depth < 3)
                    Open(f.Content(), depth + 1);
            }
        }
        foreach (string category in window.Merged.Arr("categories").Select(x => x.AsStr()).Prepend("Can Make"))
        {
            var ctx = new TguiContext(window.Window, window.Interface, window.Title, 2, window.Merged, (_, _) => { }, browse);
            ctx.SetState("activeCategory", (object)category);
            Open(Anomaly.Client.Shared.Tgui.Interfaces.PersonalCrafting.Render(new TguiContext(window.Window, window.Interface, window.Title, 2, window.Merged, (_, _) => { }, browse)), 0);
        }
        _output.WriteLine($"every category rendered, {floats} floating recipes opened");

        // The client's window opens on "Can Make" and its Make button sends tg's act.
        UiNode content = Shown(windows, replica, window.Window);
        Assert.NotNull(content);
        UiNode make = content.Walk().First(n => n.Key == "make:" + reference);
        Assert.False(make.Disabled);
        make.OnClick();
        w.Run(Second);
        Assert.True(replica.UiWindows[window.Window].Merged.Truthy("busy"), "busy during the do_after");
        Assert.True(Shown(windows, replica, window.Window).Walk().First(n => n.Key == "make:" + reference).Disabled);
        w.Run(6 * Second);
        var boards = On<obj_vehicle_ridden_scooter_skateboard_improvised>(w, TableX - 1, TableY - 1, z);
        Assert.Single(boards);
        Assert.Equal(ironBefore - 5, In(w, () => Dm.num(iron.amount)));
        Assert.Equal(rodsBefore - 10, In(w, () => Dm.num(rods.amount)));
        w.Run(Second); // SStgui's refresh: busy is over
        Assert.False(replica.UiWindows[window.Window].Merged.Truthy("busy"));

        // Slapcraft: put the stacks back on the floor, a shard in hand and cable restraints on the floor; click the rods.
        p.Session.Keybinding(KeybindingCode.DropItem);
        w.Run(2);
        p.Session.Keybinding(KeybindingCode.SwapHands);
        w.Run(2);
        p.Session.Keybinding(KeybindingCode.DropItem);
        w.Run(Second);
        obj_item_shard shard = null;
        using (w.World.Enter())
        {
            shard = Dm.@new<obj_item_shard>(me.loc);
            Waiting.NoSleep(me.put_in_hands(shard));
            Dm.@new<obj_item_restraints_handcuffs_cable>(Builtins.locate(TableX - 2, TableY - 1, z));
        }
        w.Run(2);
        Assert.True(In(w, () => Dm.truthy(me.is_holding(shard))));
        float rodsLeft = In(w, () => Dm.num(rods.amount));
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, rods.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(5 * Second);
        var spears = On<obj_item_spear>(w, TableX - 1, TableY - 1, z);
        _output.WriteLine($"spears: {spears.Count}; rods {rodsLeft} -> {In(w, () => Dm.num(rods.amount))}");
        Assert.Single(spears);
        Assert.True(In(w, () => GlobalProcs.QDELETED(shard)), "the shard went into the spear");
        Assert.Empty(On<obj_item_restraints_handcuffs_cable>(w, TableX - 2, TableY - 1, z));
        Assert.Equal(rodsLeft - 1, In(w, () => Dm.num(rods.amount)));

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
