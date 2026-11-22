using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M4c slice 3 through the real server on MetaStation: a headless Godot client (<see cref="PlaySession"/>) sees the item action
/// buttons tg puts in client.screen and clicks them. A breath mask goes on with E and an oxygen tank comes from the dev console
/// into the hand: the tank's "Set Internals" button shows on the client's HUD at tg's first listed place (the hud's template
/// background, the gold border, the tank's look) and the client's HUD layout puts it at the screen's top left; a click on it
/// opens the internals (the client hears the valve and sees the active background), a second click closes them. The magboots
/// on the feet bring their "Toggle" button; a click on it turns the mag-pulse on and the client sees magboots1 on the button.
/// Protocol version 14: hovering an empty inventory slot with the tank in hand puts tg's ghost of the tank on the slot
/// (MouseEntered), and the pointer leaving it to nothing (an Unhover, BYOND's MouseExited) takes the ghost away.
/// </summary>
public class ActionButtonGameplayTests
{
    private const int Second = 20; // ticks per second
    private const string On = "sound/items/internals/internals_on.ogg";

    private readonly ITestOutputHelper _output;

    public ActionButtonGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static ClickTarget Screen(long id) => new(TargetKind.Screen, id, 0, 0, 0, 16, 16);

    private static void ClickScreen(RealHeadlessWorld.Player p, long id, ClickModifiers modifiers = ClickModifiers.None)
        => Assert.True(p.Session.Click(Screen(id), ClickButton.Left, modifiers));

    private static T Give<T>(RealHeadlessWorld w, mob_living_carbon_human me, string path) where T : obj_item
    {
        w.Host.EnqueueConsoleLine($"give Assistant {path}");
        w.Run(5);
        var held = In(w, () => me.get_active_held_item()) as T;
        Assert.NotNull(held);
        return held;
    }

    private static void QuickEquip(RealHeadlessWorld w, RealHeadlessWorld.Player p)
    {
        p.Session.Keybinding(KeybindingCode.QuickEquip);
        w.Run(Second);
    }

    private static atom_movable_screen_movable_action_button ButtonOf(RealHeadlessWorld w, mob_living_carbon_human me, obj_item item)
        => In(w, () => Assert.Single(Dm.each_cast<datum_action>(item.actions)).viewers[me.hud_used] as atom_movable_screen_movable_action_button);

    // The icon states the client composes the screen entry from (its appearance with the overlays and underlays).
    private static List<string> SeenStates(ReplicaWorld replica, long id)
    {
        ScreenEntry entry = replica.Screen.Values.Single(e => e.Id == id);
        return CrewOutfitTests.Icons(replica, entry.Appearance).Select(i => i.State).ToList();
    }

    [MetaServerFact]
    public void The_client_sees_the_tank_and_magboots_buttons_and_a_click_on_each_toggles_it()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 12, TimeoutSteps = 6000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        int bootRuntimes = w.World.RuntimeErrorCount;
        int startButtons = In(w, () => (int)Dm.length(me.actions));
        _output.WriteLine($"buttons at the start: {startButtons}");

        // The breath mask goes on with E; the oxygen tank comes into the hand: its button is on the client's HUD.
        var mask = Give<obj_item_clothing_mask_breath>(w, me, "/obj/item/clothing/mask/breath");
        QuickEquip(w, p);
        Assert.Same(mask, In(w, () => me.wear_mask));
        var tank = Give<obj_item_tank_internals_oxygen>(w, me, "/obj/item/tank/internals/oxygen");
        w.Run(Second);
        atom_movable_screen_movable_action_button tankButton = ButtonOf(w, me, tank);
        Assert.NotNull(tankButton);
        ScreenEntry entry = replica.Screen.Values.Single(e => e.Id == tankButton.dm_id);
        List<string> seen = SeenStates(replica, tankButton.dm_id);
        _output.WriteLine($"the tank's button: {entry.ScreenLoc}, {string.Join(", ", seen)}");
        Assert.Equal("WEST+0:6,NORTH+0:-6", entry.ScreenLoc); // SCRN_OBJ_INSERT_FIRST, the listed group's first place
        Assert.Contains("template", seen);           // the hud's background (get_action_buttons_icons)
        Assert.Contains("ab_goldborder", seen);      // set_internals' border
        Assert.Contains(In(w, () => tank.icon_state as string), seen); // the item's look (action_item_overlay)
        // The client's HUD layout (D107) puts tg's WEST/NORTH place at the screen's top left corner.
        Assert.True(ScreenLoc.TryParse(entry.ScreenLoc, out ScreenLocSpec spec));
        HudRect rect = ScreenLoc.Place(spec, new HudMetrics(1920, 1080, 1), 32, 32, 0, 0, out _);
        _output.WriteLine($"drawn at {rect}");
        Assert.True(rect.Left < 100 && rect.Top < 100, "top left: " + rect);
        // The empty palette is not sent (INVISIBILITY_ABSTRACT, BYOND hides it).
        long paletteId = In(w, () => ((atom)me.hud_used.screen_objects[Defines.HUD_MOB_TOGGLE_PALETTE]).dm_id);
        Assert.DoesNotContain(replica.Screen.Values, e => e.Id == paletteId);

        // A click on the button opens the internals (set_internals -> the tank's ui_action_click -> toggle_internals).
        heard.Clear();
        ClickScreen(p, tankButton.dm_id);
        w.Run(Second);
        Assert.Same(tank, In(w, () => me.@internal));
        Assert.Contains(heard, e => e.File == On);
        Assert.Contains("template_active", SeenStates(replica, tankButton.dm_id)); // is_action_active: the internals' tank
        // Version 27: the button names its item, and the tank's volume bar (tanks.dm:96, on the tank in the hand, so no map entity)
        // names its holder; the client labels it and draws it under the button (the owner, 2026-10-18).
        Assert.Equal(tank.dm_id, replica.Screen.Values.Single(e => e.Id == tankButton.dm_id).ActionTarget);
        ProgressBarInfo volume = Assert.Single(ProgressBars.Of(replica));
        _output.WriteLine($"the volume bar: {volume}");
        Assert.Equal(0, volume.AttachedTo);
        Assert.Equal(tank.dm_id, volume.Holder);
        Assert.Equal(In(w, () => tank.name), volume.HolderName);
        Assert.Equal("Oxygen tank", ProgressBars.Label(volume, replica.OwnEntity));
        PlacedBar under = Assert.Single(ProgressBarLayout.Place(new[] { volume }, replica.OwnEntity, 2f, 1920, 30f,
            id => id == tank.dm_id ? (true, rect) : (false, default), s => s.Length * 7f));
        Assert.True(under.Bar.Top >= rect.Bottom && under.Bar.Left >= rect.Left && under.Bar.Right <= rect.Right, "under the button: " + under.Bar);
        ClickScreen(p, tankButton.dm_id);
        w.Run(Second);
        Assert.Null(In(w, () => me.@internal));
        Assert.DoesNotContain("template_active", SeenStates(replica, tankButton.dm_id));
        Assert.Empty(ProgressBars.Of(replica)); // closing the internals deletes the volume bar (tanks.dm:103)

        // Version 14: with the tank in hand, the pointer over an empty pocket slot shows tg's ghost of the tank there (MouseEntered,
        // screen_objects.dm:219); leaving it over nothing is an Unhover, BYOND's MouseExited, which takes the ghost away (:224).
        object slotKey = In(w, () => new object[] { Defines.ITEM_SLOT_LPOCKET, Defines.ITEM_SLOT_RPOCKET, Defines.ITEM_SLOT_BELT, Defines.ITEM_SLOT_ID }
            .First(s => me.get_item_by_slot(s) == null && me.hud_used.screen_objects[Dm.text("item_slot:[]", s)] is atom a && Dm.@in(a, me.client.screen)));
        var slot = In(w, () => (atom_movable_screen_inventory)me.hud_used.screen_objects[Dm.text("item_slot:[]", slotKey)]);
        Assert.DoesNotContain(In(w, () => tank.icon_state as string), SeenStates(replica, slot.dm_id));
        Assert.True(p.Session.Hover(Screen(slot.dm_id), w.Now));
        w.Run(Second);
        Assert.NotNull(In(w, () => slot.object_overlay));
        List<string> ghosted = SeenStates(replica, slot.dm_id);
        _output.WriteLine("the hovered slot: " + string.Join(", ", ghosted));
        Assert.Contains(In(w, () => tank.icon_state as string), ghosted);
        p.Session.Hover(ClickTarget.None, w.Now); // the hints clear at once; the unhover goes out on the hover interval
        w.Run(Second);
        p.Session.Hover(ClickTarget.None, w.Now);
        w.Run(Second);
        Assert.Equal(1, p.Session.UnhoversSent);
        Assert.Null(In(w, () => slot.object_overlay));
        Assert.DoesNotContain(In(w, () => tank.icon_state as string), SeenStates(replica, slot.dm_id));

        // Dropping the tank takes its button away from the client's HUD.
        p.Session.Keybinding(KeybindingCode.DropItem);
        w.Run(Second);
        Assert.DoesNotContain(replica.Screen.Values, e => e.Id == tankButton.dm_id);

        // The magboots: the old shoes come off (a click on them in their slot, then Q), the boots go on with E and bring their
        // button; a click on it turns the mag-pulse on (magboots.dm:63), and the client sees magboots1 on the button.
        var oldShoes = In(w, () => me.shoes);
        if (oldShoes != null)
        {
            if (!replica.Screen.Values.Any(e => e.Id == oldShoes.dm_id))
            {
                ClickScreen(p, In(w, () => ((atom)me.hud_used.screen_objects[Defines.HUD_HUMAN_TOGGLE_INVENTORY]).dm_id));
                w.Run(Second);
            }
            ClickScreen(p, oldShoes.dm_id);
            w.Run(Second);
            p.Session.Keybinding(KeybindingCode.DropItem);
            w.Run(Second);
            Assert.Null(In(w, () => me.shoes));
        }
        var boots = Give<obj_item_clothing_shoes_magboots>(w, me, "/obj/item/clothing/shoes/magboots");
        Assert.Null(ButtonOf(w, me, boots)); // in a hand the boots give no button (their slot is the feet)
        QuickEquip(w, p);
        Assert.Same(boots, In(w, () => me.shoes));
        w.Run(Second);
        atom_movable_screen_movable_action_button bootsButton = ButtonOf(w, me, boots);
        Assert.NotNull(bootsButton);
        List<string> bootsSeen = SeenStates(replica, bootsButton.dm_id);
        _output.WriteLine($"the magboots' button: {replica.Screen.Values.Single(e => e.Id == bootsButton.dm_id).ScreenLoc}, {string.Join(", ", bootsSeen)}");
        Assert.Contains("magboots0", bootsSeen);
        Assert.Equal("Toggle magboots", In(w, () => bootsButton.name));
        ClickScreen(p, bootsButton.dm_id);
        w.Run(Second);
        Assert.Equal(1f, In(w, () => boots.magpulse));
        Assert.True(In(w, () => HAS_TRAIT(me, Defines.TRAIT_NEGATES_GRAVITY)));
        bootsSeen = SeenStates(replica, bootsButton.dm_id);
        Assert.Contains("magboots1", bootsSeen);
        Assert.DoesNotContain("magboots0", bootsSeen);
        ClickScreen(p, bootsButton.dm_id);
        w.Run(Second);
        Assert.Equal(0f, In(w, () => boots.magpulse));
        Assert.Contains("magboots0", SeenStates(replica, bootsButton.dm_id));

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount); // nothing the player did ran into a runtime
    }
}
