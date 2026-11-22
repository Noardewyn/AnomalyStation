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
/// M4c slice 2 through the real server on MetaStation: a headless Godot client (<see cref="PlaySession"/>) suits up with
/// clicks and keys. The dev console gives the items (the suit storage units come with slice 4): the EVA suit and helmet
/// and a breath mask go on with E; the oxygen tank's valve opens with an alt-click in hand and a click on the suit storage
/// slot hangs it on the suit; the spacesuit display's click turns the thermal regulator on; the old shoes come off with a
/// click on their slot and Q, the magboots' mag-pulse goes on with Z in hand and they go on with E. A dev breach next to the
/// player empties the room: in 60 seconds the player takes no pressure damage and no oxygen damage, and the wind does not
/// move the magbooted player. The client sees the suit, the helmet and the magboots (magboots1) on the body and the cell on
/// the HUD, and hears the valve.
/// </summary>
public class SpaceSuitGameplayTests
{
    private const int Second = 20; // ticks per second
    private const string On = "sound/items/internals/internals_on.ogg";

    private readonly ITestOutputHelper _output;

    public SpaceSuitGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void ClickScreen(RealHeadlessWorld.Player p, long id, ClickModifiers modifiers = ClickModifiers.None)
        => Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, id, 0, 0, 0, 16, 16), ClickButton.Left, modifiers));

    private static List<string> States(ReplicaWorld replica, atom a)
    {
        ReplicaEntity seen = replica.Entities.Single(e => e.Id == a.dm_id);
        return CrewOutfitTests.Icons(replica, seen.State.Appearance).Select(i => i.State).ToList();
    }

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

    private static long SlotId(RealHeadlessWorld w, mob_living_carbon_human me, object slot)
        => In(w, () => ((atom)me.hud_used.screen_objects[Dm.text("item_slot:[]", slot)]).dm_id);

    [MetaServerFact]
    public void A_player_suits_up_with_magboots_and_stays_put_and_unhurt_through_a_breach()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 11, TimeoutSteps = 6000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        int z = In(w, () => (int)me.z);
        int bootRuntimes = w.World.RuntimeErrorCount;

        // The old shoes come off (a click on them in their slot with the empty hand, then Q), the magboots' mag-pulse goes on in hand
        // (Z: attack_self, magboots.dm:63) and they go on with E.
        var oldShoes = In(w, () => me.shoes);
        if (oldShoes != null)
        {
            // the shoe slot is in the toggleable inventory: open it with its button (human.dm:15), then click the shoes drawn in
            // their slot (a clickable vis child, protocol 12)
            if (!replica.Screen.Values.Any(e => e.Id == oldShoes.dm_id))
            {
                ClickScreen(p, In(w, () => ((atom)me.hud_used.screen_objects[Defines.HUD_HUMAN_TOGGLE_INVENTORY]).dm_id));
                w.Run(Second);
            }
            ClickScreen(p, oldShoes.dm_id);
            w.Run(Second);
            Assert.Same(oldShoes, In(w, () => me.get_active_held_item()));
            p.Session.Keybinding(KeybindingCode.DropItem);
            w.Run(Second);
            Assert.Null(In(w, () => me.shoes));
        }
        var boots = Give<obj_item_clothing_shoes_magboots>(w, me, "/obj/item/clothing/shoes/magboots");
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        Assert.Equal(1f, In(w, () => boots.magpulse));
        QuickEquip(w, p);
        Assert.Same(boots, In(w, () => me.shoes));
        Assert.True(In(w, () => HAS_TRAIT(me, Defines.TRAIT_NEGATES_GRAVITY)));

        List<string> feet = States(replica, me);
        _output.WriteLine("the body with the magboots as the client sees it: " + string.Join(", ", feet));
        Assert.Contains("magboots1", feet); // feet.dmi, the mag-pulse state (magboots.dm:93), redrawn on the body (update_icon_updates_onmob)

        // The EVA suit and helmet (softsuit.dm:31, :39) and a breath mask, each from the dev console into the hand and on with E.
        var suit = Give<obj_item_clothing_suit_space_eva>(w, me, "/obj/item/clothing/suit/space/eva");
        QuickEquip(w, p);
        Assert.Same(suit, In(w, () => me.wear_suit));
        var helmet = Give<obj_item_clothing_head_helmet_space_eva>(w, me, "/obj/item/clothing/head/helmet/space/eva");
        QuickEquip(w, p);
        Assert.Same(helmet, In(w, () => me.head));
        var mask = Give<obj_item_clothing_mask_breath>(w, me, "/obj/item/clothing/mask/breath");
        QuickEquip(w, p);
        Assert.Same(mask, In(w, () => me.wear_mask));

        // The oxygen tank: alt-click in hand opens the valve (tank_types.dm:15), a click on the suit storage slot hangs it on
        // the suit (the suit's allowed list, _species.dm:643).
        var tank = Give<obj_item_tank_internals_oxygen>(w, me, "/obj/item/tank/internals/oxygen");
        heard.Clear();
        ClickScreen(p, tank.dm_id, ClickModifiers.Alt);
        w.Run(Second);
        Assert.Same(tank, In(w, () => me.@internal));
        Assert.Contains(heard, e => e.File == On);
        ClickScreen(p, SlotId(w, me, Defines.ITEM_SLOT_SUITSTORE));
        w.Run(Second);
        Assert.Same(tank, In(w, () => me.s_store));
        Assert.Same(tank, In(w, () => me.@internal));

        // The spacesuit display (human.dm:31): a click turns the thermal regulator on (screen_objects.dm:472).
        var display = In(w, () => me.hud_used.screen_objects[Defines.HUD_MOB_SPACESUIT] as atom_movable_screen_spacesuit);
        Assert.NotNull(display);
        Assert.Contains(replica.Screen.Values, e => e.Id == display.dm_id);
        ClickScreen(p, display.dm_id);
        w.Run(3 * Second);
        Assert.Equal(1f, In(w, () => suit.thermal_on));
        Assert.Equal("spacesuit_high", In(w, () => display.icon_state));
        ScreenEntry shown = replica.Screen.Values.Single(e => e.Id == display.dm_id);
        Assert.Contains("spacesuit_high", CrewOutfitTests.Icons(replica, shown.Appearance).Select(i => i.State));

        List<string> body = States(replica, me);
        _output.WriteLine("the body as the client sees it: " + string.Join(", ", body));
        Assert.Equal(2, body.Count(st => st == "space")); // the EVA suit and helmet (spacesuit.dmi, spacehelm.dmi)
        Assert.DoesNotContain("magboots1", body); // the suit's HIDESHOES hides them (flags_inv, _spacesuits.dm:100)

        // The breach two tiles east: the room empties into space and the wind blows toward the hole.
        (int x, int y) = In(w, () => ((int)me.x, (int)me.y));
        float moles = In(w, () => Dm.num(tank.air_contents.total_moles()));
        w.Host.EnqueueConsoleLine($"breach {x + 2} {y} {z}");
        var spots = new HashSet<(int, int)>();
        for (int s = 0; s < 60; s++)
        {
            w.Run(Second);
            spots.Add(In(w, () => ((int)me.x, (int)me.y)));
        }
        float pressure = In(w, () => Dm.num(((datum_gas_mixture)Waiting.NoSleep(((turf)me.loc).return_air())).return_pressure()));
        _output.WriteLine($"after 60 s: pressure {pressure} kPa, brute {In(w, () => Dm.num(me.get_brute_loss()))}, burn {In(w, () => Dm.num(me.get_fire_loss()))}, " +
            $"oxy {In(w, () => Dm.num(me.get_oxy_loss()))}, body {In(w, () => me.bodytemperature)} K, tank moles {moles} -> {In(w, () => Dm.num(tank.air_contents.total_moles()))}, " +
            $"cell {In(w, () => suit.cell_instance.charge)}, spots {string.Join(" ", spots)}");
        Assert.True(pressure < 10f, "the room emptied: " + pressure);
        Assert.Equal(0f, In(w, () => Dm.num(me.get_brute_loss()))); // no pressure damage (STOPSPRESSUREDAMAGE)
        Assert.Equal(0f, In(w, () => Dm.num(me.get_oxy_loss())));
        Assert.Equal(0f, In(w, () => Dm.num(me.get_fire_loss())));
        Assert.Equal(0f, In(w, () => me.stat)); // CONSCIOUS
        Assert.Equal(new HashSet<(int, int)> { (x, y) }, spots); // the magboots hold against the wind (living.dm:1113)
        Assert.True(In(w, () => Dm.num(tank.air_contents.total_moles())) < moles, "the breaths came from the tank");
        Assert.True(In(w, () => suit.cell_instance.charge) < Defines.STANDARD_CELL_CHARGE * 10f, "the regulator drew the cell");

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount); // nothing the player did ran into a runtime
    }
}
