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
/// M4c slice 4 through the real server on MetaStation (protocol version 15, DV40): a player with the Captain's ID (EVA access; the job
/// system is M9/M10, every player joins as an assistant) suits up in the E.V.A. Storage room by clicks. A click on the suit storage
/// unit at (91,124) brings tg's radial to the client (a RadialMenu message: open, disinfect, lock, drawn with radial.dmi's pictures);
/// the client's picks open the unit (the client sees its open overlays) and take the EVA suit, the helmet and the breath mask, each
/// worn with E; the centre closes the menu. The magboots and a CO2 jetpack come from the racks behind the "Magboot Storage" and
/// "Jetpack Storage" windoors (EVA access; the jetpack's pickup waits for its own code, M4c slice 6, and is refused by D132's guard
/// until then), an oxygen tank from the dispenser with a right click. Another unit's UV cycle is seen
/// (its lights red and the UV on top) and heard opening (airlockclose.ogg).
/// </summary>
public class SuitStorageGameplayTests
{
    private const int Second = 20; // ticks per second
    private const string Opened = "sound/machines/airlock/airlockclose.ogg";

    private readonly ITestOutputHelper _output;

    public SuitStorageGameplayTests(ITestOutputHelper output) => _output = output;

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

    private static T Find<T>(RealHeadlessWorld w, int x, int y, int z) where T : class
        => In(w, () => Dm.each_cast<atom>(((turf)Builtins.locate(x, y, z)).contents).OfType<T>().FirstOrDefault());

    private static List<string> States(ReplicaWorld replica, atom a)
    {
        ReplicaEntity seen = replica.Entities.Single(e => e.Id == a.dm_id);
        return CrewOutfitTests.Icons(replica, seen.State.Appearance).Select(i => i.State).ToList();
    }

    private static void Click(RealHeadlessWorld w, RealHeadlessWorld.Player p, atom target, ClickButton button = ClickButton.Left)
    {
        Assert.True(p.Session.Click(target.dm_id, button, ClickModifiers.None));
        w.Run(Second);
    }

    // The client picks the slice named name of the radial it draws (the newest open menu of its replica).
    private void PickSlice(RealHeadlessWorld w, RealHeadlessWorld.Player p, string name)
    {
        RadialMenuMessage menu = p.Session.Replica.TopRadial;
        Assert.NotNull(menu);
        int element = menu.Elements.FindIndex(e => e.Name == name) + 1;
        _output.WriteLine($"radial #{menu.MenuId}: {string.Join(", ", menu.Elements.Select(e => e.Name))}; picking {name}");
        Assert.True(element > 0, name + " is not on the radial");
        Assert.True(p.Session.RadialPick(menu.MenuId, element));
        w.Run(Second / 2);
    }

    private static void QuickEquip(RealHeadlessWorld w, RealHeadlessWorld.Player p)
    {
        p.Session.Keybinding(KeybindingCode.QuickEquip);
        w.Run(Second);
    }

    [MetaServerFact]
    public void The_captain_suits_up_in_EVA_storage_through_the_suit_storage_units_radial()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 21, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        int z = In(w, () => (int)me.z);
        int bootRuntimes = w.World.RuntimeErrorCount;

        // The Captain's ID in the ID slot (tg's captain trim: every station access, EVA among them).
        In(w, () =>
        {
            if (me.wear_id is obj_item old)
                qdel(old);
            var card = Dm.@new<obj_item_card_id_advanced_gold_captains_spare>(me.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(me.equip_to_slot_if_possible(card, Defines.ITEM_SLOT_ID))), "the Captain's ID goes on");
            Assert.Contains(Defines.ACCESS_EVA, Dm.each_cast<object>(card.GetAccess() as DmList));
            return 0;
        });

        var unit = Find<obj_machinery_suit_storage_unit_standard_unit>(w, 91, 124, z);
        Assert.NotNull(unit);
        Assert.Equal(8, In(w, () => Enumerable.Range(117, 9).Sum(y => new[] { 91, 95 }.Count(x => Find<obj_machinery_suit_storage_unit_standard_unit>(w, x, y, z) != null))));
        Put(w, me, 92, 124); // M10 slice C: a client holds what is around it
        List<string> shut = States(replica, unit);
        _output.WriteLine("the unit as the client sees it: " + string.Join(", ", shut));
        Assert.Contains("classic_lights_closed", shut);
        Assert.Contains("classic_ready", shut);

        // A click on the unit: tg's radial reaches the client (version 15), drawn with radial.dmi's pictures.
        Put(w, me, 92, 124);
        Click(w, p, unit);
        RadialMenuMessage first = replica.TopRadial;
        Assert.NotNull(first);
        Assert.Equal(new[] { "open", "disinfect", "lock" }, first.Elements.Select(e => e.Name));
        Assert.True((first.Flags & RadialMenuFlags.CloseButton) != 0);
        var openIcon = CrewOutfitTests.Icons(replica, first.Elements[0].Icon).First();
        Assert.Equal(("icons/hud/radial.dmi", "radial_open"), (openIcon.Icon, openIcon.State));
        Assert.Equal("radial_slice", CrewOutfitTests.Icons(replica, first.Elements[0].Background).First().State);
        Assert.Equal("radial_slice_focus", CrewOutfitTests.Icons(replica, first.Elements[0].BackgroundFocus).First().State);
        Assert.Equal("radial_center", CrewOutfitTests.Icons(replica, first.Center).First().State);
        Assert.False(In(w, () => Dm.truthy(unit.state_open)));

        PickSlice(w, p, "open");
        Assert.True(In(w, () => Dm.truthy(unit.state_open)));
        w.Run(Second / 2);
        List<string> open = States(replica, unit);
        _output.WriteLine("the open unit: " + string.Join(", ", open));
        Assert.Contains("classic_open", open);
        Assert.Contains("classic_suit", open);
        Assert.Contains("classic_helm", open);
        Assert.Contains("classic_lights_open", open);
        RadialMenuMessage slots = replica.TopRadial;
        Assert.NotEqual(first.MenuId, slots.MenuId); // tg's interact shows a new menu after each pick
        Assert.DoesNotContain(replica.Radials, m => m.MenuId == first.MenuId);
        Assert.Equal(new[] { "close", "suit", "helmet", "mask", "mod", "storage" }, slots.Elements.Select(e => e.Name));
        string suitState = In(w, () => unit.suit_value.icon_state as string);
        Assert.Contains(suitState, CrewOutfitTests.Icons(replica, slots.Elements[1].Icon).Select(i => i.State));
        Assert.True(replica.TryGetAppearance(slots.Elements[4].Icon, out AppearanceEntry silhouette)); // no MOD: a red silhouette
        Assert.Equal(128, silhouette.Alpha);

        // The suit, the helmet and the mask, each into the hand and worn with E.
        obj_item suit = In(w, () => unit.suit_value), helmet = In(w, () => unit.helmet_value), mask = In(w, () => unit.mask_value);
        PickSlice(w, p, "suit");
        Assert.Same(suit, In(w, () => me.get_active_held_item()));
        QuickEquip(w, p);
        Assert.Same(suit, In(w, () => me.wear_suit));
        PickSlice(w, p, "helmet");
        Assert.Same(helmet, In(w, () => me.get_active_held_item()));
        QuickEquip(w, p);
        Assert.Same(helmet, In(w, () => me.head));
        PickSlice(w, p, "mask");
        Assert.Same(mask, In(w, () => me.get_active_held_item()));
        QuickEquip(w, p);
        Assert.Same(mask, In(w, () => me.wear_mask));
        w.Run(Second);
        List<string> emptied = States(replica, unit);
        Assert.DoesNotContain("classic_suit", emptied);
        Assert.DoesNotContain("classic_helm", emptied);

        // The centre button closes the menu on both sides.
        RadialMenuMessage last = replica.TopRadial;
        Assert.True(p.Session.RadialPick(last.MenuId, 0));
        w.Run(Second);
        Assert.Null(replica.TopRadial);
        Assert.Empty(In(w, () => Dm.each_cast<datum_radial_menu>(me.client.dm_radial_menus).ToList()));

        // The old shoes off, then the magboots from behind the "Magboot Storage" windoor (EVA access) onto the feet.
        obj_item shoes = In(w, () => me.shoes);
        if (shoes != null)
        {
            In(w, () => Waiting.NoSleep(me.dropItemToGround(shoes, force: Dm.TRUE)));
            w.Run(Second);
        }
        Put(w, me, 94, 122);
        var magWindoor = Find<obj_machinery_door_window>(w, 95, 122, z);
        Assert.NotNull(magWindoor);
        Click(w, p, magWindoor);
        w.Run(Second);
        Assert.False(In(w, () => Dm.truthy(magWindoor.density)), "the windoor opens for EVA access");
        var boots = Find<obj_item_clothing_shoes_magboots>(w, 95, 122, z);
        Assert.NotNull(boots);
        Click(w, p, boots);
        Assert.Same(boots, In(w, () => me.get_active_held_item()));
        QuickEquip(w, p);
        Assert.Same(boots, In(w, () => me.shoes));

        // A jetpack from behind the "Jetpack Storage" windoor. The jetpack's own code is M4c slice 6: until it is translated tg's
        // equipped override of the jetpack (jetpack.dm:63) is undeclared and D132's guard refuses the pickup with its message
        // (DV27); once it is, the jetpack comes into the hand.
        Put(w, me, 94, 120);
        var jetWindoor = Find<obj_machinery_door_window>(w, 95, 120, z);
        Click(w, p, jetWindoor);
        w.Run(Second);
        Assert.False(In(w, () => Dm.truthy(jetWindoor.density)));
        var jetpack = Find<obj_item_tank_jetpack_carbondioxide>(w, 95, 120, z);
        Assert.NotNull(jetpack);
        bool jetpackPorted = !In(w, () => InertSeam.HasUndeclaredOverride(jetpack, "equipped"));
        Click(w, p, jetpack);
        if (jetpackPorted)
            Assert.Same(jetpack, In(w, () => me.get_active_held_item()));
        else
        {
            _output.WriteLine("the jetpack's equipped is not translated yet (M4c slice 6): the pickup is refused by D132's guard");
            Assert.Null(In(w, () => me.get_active_held_item()));
            Assert.Same(In(w, () => Builtins.locate(95, 120, z)), In(w, () => jetpack.loc));
        }

        // An oxygen tank from the dispenser with a right click into the other hand (tank_dispenser.dm:47).
        p.Session.Keybinding(KeybindingCode.SwapHands);
        w.Run(Second);
        Put(w, me, 94, 121);
        var dispenser = Find<obj_structure_tank_dispenser_oxygen>(w, 93, 121, z);
        Assert.NotNull(dispenser);
        float before = In(w, () => dispenser.oxygentanks);
        Click(w, p, dispenser, ClickButton.Right);
        Assert.IsType<obj_item_tank_internals_oxygen>(In(w, () => me.get_active_held_item()));
        Assert.Equal(before - 1, In(w, () => dispenser.oxygentanks));
        obj_item tank = In(w, () => me.get_active_held_item() as obj_item);
        QuickEquip(w, p); // E: the tank goes where tg's quick equip puts it (the EVA suit's storage), a hand is free again
        Assert.Null(In(w, () => me.get_active_held_item()));
        _output.WriteLine("the tank went to " + In(w, () => tank.loc == me ? "slot " + me.get_slot_by_item(tank) : tank.loc?.ToString()));

        // Another unit's UV cycle: seen (the red lights and the UV) and heard opening.
        var other = Find<obj_machinery_suit_storage_unit_standard_unit>(w, 95, 123, z);
        Put(w, me, 94, 123);
        Click(w, p, other);
        PickSlice(w, p, "disinfect");
        w.Run(Second / 2);
        List<string> cooking = States(replica, other);
        _output.WriteLine("the unit cooking: " + string.Join(", ", cooking));
        Assert.Contains("classic_lights_red", cooking);
        Assert.Contains("classic_uv", cooking);
        Assert.Null(replica.TopRadial); // check_interactable is FALSE while it cooks: no new menu
        heard.Clear();
        w.Run(31 * Second);
        Assert.False(In(w, () => Dm.truthy(other.uv)));
        Assert.Contains(heard, e => e.File == Opened);
        Assert.Contains("classic_lights_open", States(replica, other));

        _output.WriteLine($"radials sent {w.Host.SessionOf(1).RadialsSent}, picks {p.Session.RadialPicksSent}");
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount); // nothing the player did ran into a runtime
    }
}
