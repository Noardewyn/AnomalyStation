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
/// M7 lighting slice L1 (D168) on MetaStation through the real server: the 130 mapped wall light switches work, a room's
/// fixtures follow its APC, a bulb is broken, taken out and replaced, and a lit flare shows its light. Each step is read from
/// the client's replica: a fixture's entity carries its light (range, colour, on) and its icon state.
/// </summary>
public class LightingGameplayTests(ITestOutputHelper output)
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

    private sealed record Room(obj_machinery_light_switch Switch, area Area, obj_machinery_power_apc Apc, List<obj_machinery_light> Lights);

    // A station room with a mapped light switch, its APC and at least two lit fixtures near the switch.
    private static Room PickRoom()
    {
        foreach (obj_machinery_light_switch sw in Dm.each<obj_machinery_light_switch>(Dm.world).Where(s => Dm.eq(s.z, 1) && s.loc is turf_open).OrderBy(s => s.dm_id))
        {
            if (sw.area is not area room || !Dm.truthy(room.requires_power) || !Dm.@in(room.type, GLOB.the_station_areas))
                continue;
            obj_machinery_power_apc apc = room.apc;
            if (apc == null || apc.cell == null || apc.loc is not turf_open || !Dm.eq(apc.z, 1))
                continue;
            List<obj_machinery_light> lights = Dm.each<obj_machinery_light>(Dm.world)
                .Where(l => Dm.eq(l.z, 1) && ReferenceEquals(get_area(l), room) && Dm.truthy(l.on) && Dm.eq(l.status, LIGHT_OK) && l.loc is turf
                    && Math.Abs(Dm.num(l.x) - Dm.num(sw.x)) <= 8 && Math.Abs(Dm.num(l.y) - Dm.num(sw.y)) <= 8)
                .ToList();
            if (lights.Count >= 2)
                return new Room(sw, room, apc, lights);
        }
        return null;
    }

    private static string IconOf(ReplicaWorld replica, ReplicaEntity e)
        => replica.TryGetAppearance(e.State.Appearance, out AppearanceEntry look) ? replica.ResolveString(look.IconState) : null;

    private static List<ReplicaEntity> Seen(ReplicaWorld replica, IEnumerable<datum> things)
        => things.Select(t => replica.Entities.FirstOrDefault(e => e.Id == t.dm_id)).Where(e => e != null).ToList();

    private static bool Dark(ReplicaEntity e) => e.Light.IsNone || !e.Light.On;

    private static void StandOn(RealHeadlessWorld w, mob me, atom where)
    {
        In(w, () => Waiting.NoSleep(me.forceMove(where.loc)));
        w.Run(Second);
    }

    /// <summary>The wall switch: a click turns the room's fixtures off, the client's replica shows them dark and the switch lit with its off look; another click turns them on again.</summary>
    [MetaServerFact]
    public void A_wall_light_switch_turns_the_rooms_fixtures_off_and_on_in_the_clients_replica()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 91, TimeoutSteps = 12000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Electrician");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);

        Room room = In(w, PickRoom);
        Assert.NotNull(room);
        obj_machinery_light_switch sw = room.Switch;
        output.WriteLine($"{In(w, () => Dm.str(room.Area.name))}: the switch at {In(w, () => sw.x)},{In(w, () => sw.y)}, {room.Lights.Count} lit fixtures");
        StandOn(w, me, sw);
        int bootRuntimes = w.World.RuntimeErrorCount;

        List<ReplicaEntity> before = Seen(replica, room.Lights);
        Assert.True(before.Count >= 2, $"the client sees {before.Count} fixtures of the room");
        Assert.All(before, e => Assert.True(e.Light.On && e.Light.Range > 0f, "a lit fixture"));
        float Brightness(ReplicaEntity e) => In(w, () => Dm.num(room.Lights.Single(l => l.dm_id == e.Id).brightness));
        Assert.All(before, e => Assert.Equal(Brightness(e), e.Light.Range));
        ReplicaEntity switchEntity = replica.Entities.Single(e => e.Id == sw.dm_id);
        Assert.Equal("light-on", IconOf(replica, switchEntity));
        Assert.True(Dark(switchEntity), "an on switch gives no light");
        Assert.Equal("light switch (" + In(w, () => Dm.str(room.Area.name)) + ")", In(w, () => Dm.str(sw.name)));

        // the click (attack_hand -> interact): tg's off sound, the area's lightswitch, every fixture off
        Assert.True(p.Session.Click(sw.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        In(w, () =>
        {
            Assert.False(Dm.truthy(room.Area.lightswitch));
            Assert.All(room.Lights, l => Assert.False(Dm.truthy(l.on)));
            Assert.All(room.Lights, l => Assert.False(Dm.truthy(l.low_power_mode), "a switched-off fixture takes no emergency light"));
        });
        Assert.All(Seen(replica, room.Lights), e => Assert.True(Dark(e), "the fixture is dark in the replica"));
        switchEntity = replica.Entities.Single(e => e.Id == sw.dm_id);
        Assert.Equal("light-off", IconOf(replica, switchEntity));
        Assert.True(switchEntity.Light.On, "the off switch glows a little");
        Assert.Contains(heard, h => h.Kind == SoundKind.Play && h.File != null && (h.File ?? "").StartsWith("sound/items/weapons/magout", StringComparison.Ordinal));
        // the other rooms keep their light
        obj_machinery_light elsewhere = In(w, () => Dm.each<obj_machinery_light>(Dm.world).Where(l => Dm.eq(l.z, 1) && !ReferenceEquals(get_area(l), room.Area) && Dm.truthy(l.on) && l.loc is turf
            && Math.Abs(Dm.num(l.x) - Dm.num(sw.x)) <= 8 && Math.Abs(Dm.num(l.y) - Dm.num(sw.y)) <= 8).FirstOrDefault());
        if (elsewhere != null)
            Assert.True(In(w, () => Dm.truthy(elsewhere.on)), "a fixture of another room is not switched");

        // and on again
        Assert.True(p.Session.Click(sw.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        In(w, () =>
        {
            Assert.True(Dm.truthy(room.Area.lightswitch));
            Assert.All(room.Lights, l => Assert.True(Dm.truthy(l.on)));
        });
        List<ReplicaEntity> after = Seen(replica, room.Lights);
        Assert.Equal(before.Count, after.Count);
        Assert.All(after, e => Assert.True(e.Light.On && e.Light.Range == Brightness(e)));
        Assert.Equal("light-on", IconOf(replica, replica.Entities.Single(e => e.Id == sw.dm_id)));
        Assert.Contains(heard, h => h.Kind == SoundKind.Play && h.File != null && (h.File ?? "").StartsWith("sound/items/weapons/magin", StringComparison.Ordinal));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    /// <summary>The room's power: an engineer unlocks the APC's window with an ID and switches its main breaker off. The fixtures leave their normal light for tg's emergency light (a red, quarter-range one on the emergency cell); the breaker back on restores the normal light.</summary>
    [MetaServerFact]
    public void The_apc_breaker_takes_the_rooms_fixtures_to_the_emergency_light_and_back()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 92, TimeoutSteps = 12000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        UiNode Shown(int index)
        {
            windows.Sync(replica.UiWindows, 1280, 720, 1);
            return windows.Get(index)?.Content;
        }

        Room room = In(w, PickRoom);
        Assert.NotNull(room);
        obj_machinery_power_apc apc = room.Apc;
        StandOn(w, me, apc);
        int bootRuntimes = w.World.RuntimeErrorCount;
        uint emergency = CellState.ParseColor(COLOR_VIVID_RED); // bulb_low_power_colour
        float Brightness(ReplicaEntity e) => In(w, () => Dm.num(room.Lights.Single(l => l.dm_id == e.Id).brightness));

        List<ReplicaEntity> normal = Seen(replica, room.Lights);
        Assert.True(normal.Count >= 2, $"the client sees {normal.Count} fixtures of the room");
        Assert.All(normal, e => Assert.True(e.Light.On && e.Light.Rgba != emergency));
        In(w, () => Assert.True(Dm.truthy(apc.operating)));

        // the APC's window, unlocked with a debug ID (as ApcGameplayTests does)
        Assert.True(p.Session.Click(apc.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        In(w, () => Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(Dm.@new<obj_item_card_id_advanced_debug>(me.loc))))));
        Assert.True(p.Session.Click(apc.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiNode Breaker() => Shown(window.Window).Walk().First(n => n.Kind == UiKind.LabeledItem && n.Label == "Main Breaker").Buttons.Walk().First(n => n.Key == "breaker");
        Assert.Equal("On", Breaker().Text);

        // the main breaker off (toggle_breaker, apc_power_proc.dm:43): the channels go off and the fixtures take the emergency light
        Breaker().OnClick();
        w.Run(6 * Second);
        In(w, () =>
        {
            Assert.False(Dm.truthy(apc.operating));
            Assert.False(Dm.truthy(room.Area.power_light));
            Assert.All(room.Lights, l => Assert.True(Dm.truthy(l.low_power_mode)));
            Assert.All(room.Lights, l => Assert.False(Dm.truthy(l.on)));
        });
        List<ReplicaEntity> dim = Seen(replica, room.Lights);
        Assert.Equal(normal.Count, dim.Count);
        foreach (ReplicaEntity e in dim)
        {
            Assert.True(e.Light.On, "the emergency light is on");
            Assert.Equal(emergency, e.Light.Rgba);
            Assert.Equal(Math.Max(Brightness(e) * 0.25f, MINIMUM_USEFUL_LIGHT_RANGE), e.Light.Range); // brightness * bulb_low_power_brightness_mul
            Assert.EndsWith("_emergency", IconOf(replica, e));
        }
        Assert.Equal("Off", Breaker().Text);

        // the breaker back on: the normal light
        Breaker().OnClick();
        w.Run(8 * Second);
        In(w, () =>
        {
            Assert.True(Dm.truthy(apc.operating));
            Assert.True(Dm.truthy(room.Area.power_light));
            Assert.All(room.Lights, l => Assert.True(Dm.truthy(l.on)));
        });
        List<ReplicaEntity> back = Seen(replica, room.Lights);
        Assert.Equal(normal.Count, back.Count);
        Assert.All(back, e => Assert.True(e.Light.On && e.Light.Rgba != emergency && e.Light.Range == Brightness(e)));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    /// <summary>A bulb: an engineer breaks a fixture's tube with a hit, takes the broken tube out with his hand, puts in a new one. The replica shows the fixture broken (dark, its broken look), empty (dark, its empty look) and lit again.</summary>
    [MetaServerFact]
    public void A_fixture_hit_with_an_item_breaks_the_broken_tube_comes_out_by_hand_and_a_new_one_lights_it()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 93, TimeoutSteps = 12000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;

        Room room = In(w, PickRoom);
        Assert.NotNull(room);
        obj_machinery_light lamp = room.Lights[0];
        StandOn(w, me, lamp);
        int bootRuntimes = w.World.RuntimeErrorCount;
        string baseState = In(w, () => Dm.str(lamp.base_state));
        ReplicaEntity E() => replica.Entities.Single(e => e.Id == lamp.dm_id);
        Assert.True(E().Light.On);
        Assert.Equal(baseState, IconOf(replica, E()));

        // a hard hit with a heavy item: take_damage breaks the tube with a chance of damage * 5 % (light.dm:483)
        w.Host.EnqueueConsoleLine("give Engineer /obj/item/crowbar");
        w.Run(5);
        obj_item_crowbar bar = In(w, () => me.get_active_held_item() as obj_item_crowbar);
        Assert.NotNull(bar);
        In(w, () =>
        {
            bar.force = 60;
            me.combat_mode = TRUE;
        });
        Assert.True(p.Session.Click(lamp.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        In(w, () =>
        {
            Assert.Equal(LIGHT_BROKEN, Dm.num(lamp.status));
            Assert.False(Dm.truthy(lamp.on));
        });
        Assert.True(Dark(E()), "a broken fixture is dark");
        Assert.Equal(baseState + "-broken", IconOf(replica, E()));    // update_icon_state, light.dm:177

        // the broken tube out by hand (the right click, attack_hand_secondary: a light that is not on comes out without a burn)
        In(w, () =>
        {
            me.combat_mode = FALSE;
            foreach (obj_item held in Dm.each_cast<obj_item>(me.held_items.Copy()))
                qdel(held);
        });
        w.Run(5);
        Assert.True(p.Session.Click(lamp.dm_id, ClickButton.Right, ClickModifiers.None)); // attack_hand_secondary, light.dm:584
        w.Run(2 * Second);
        In(w, () =>
        {
            Assert.Equal(LIGHT_EMPTY, Dm.num(lamp.status));
            Assert.True(Dm.each(me.contents).OfType<obj_item_light>().Any() || Dm.each(me.loc.contents).OfType<obj_item_light>().Any(), "the broken tube is in the hand or on the floor");
        });
        Assert.True(Dark(E()));
        Assert.Equal(baseState + "-empty", IconOf(replica, E()));

        // a new tube (or bulb, as the fixture takes) lights it again
        In(w, () =>
        {
            foreach (obj_item_light l in Dm.each(me.contents).OfType<obj_item_light>().ToList())
                qdel(l);
            Type light_type = lamp.light_type;
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(Dm.@new(light_type, me.loc) as obj_item))));
        });
        w.Run(5);
        Assert.True(p.Session.Click(lamp.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        In(w, () =>
        {
            Assert.Equal(LIGHT_OK, Dm.num(lamp.status));
            Assert.True(Dm.truthy(lamp.on));
        });
        Assert.True(E().Light.On);
        Assert.Equal(baseState, IconOf(replica, E()));
        Assert.Equal(In(w, () => Dm.num(lamp.brightness)), E().Light.Range);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    /// <summary>A flare: lit with the use key, its red light is on the player in the replica, then on the floor where it is dropped, then gone once it burns out.</summary>
    [MetaServerFact]
    public void A_lit_flare_shows_its_light_in_the_hand_and_on_the_floor_and_burns_out()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 94, TimeoutSteps = 12000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        int z = In(w, () => (int)me.z);
        In(w, () => Waiting.NoSleep(me.forceMove(locate(46, 122, z)))); // the arrivals corridor
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        LightState Own() => replica.TryGet(replica.OwnEntity, out ReplicaEntity e) ? e.Light : LightState.None;

        w.Host.EnqueueConsoleLine("give Assistant /obj/item/flashlight/flare");
        w.Run(5);
        var flare = In(w, () => me.get_active_held_item() as obj_item_flashlight_flare);
        Assert.NotNull(flare);
        Assert.False(Own().On, "an unlit flare lights nothing");

        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        Assert.Equal(1f, In(w, () => flare.light_on));
        Assert.Equal("flare-on", In(w, () => flare.icon_state));
        Assert.Equal("lit flare", In(w, () => Dm.str(flare.name)));
        LightState inHand = Own();
        output.WriteLine($"in the hand: {inHand}");
        Assert.True(inHand.On);
        Assert.Equal(6f, inHand.Range);   // the overlay light's range is at most 6 (overlay_lighting.dm:401) of the flare's 7
        Assert.Equal(2f, inHand.Power);
        Assert.Equal(CellState.ParseColor(LIGHT_COLOR_FLARE), inHand.Rgba);
        Assert.Contains(heard, h => h.Kind == SoundKind.Play && h.File != null && (h.File ?? "").StartsWith("sound/items/match_strike", StringComparison.Ordinal));

        // dropped: the light is the flare's own, on the floor
        p.Session.Keybinding(KeybindingCode.DropItem);
        w.Run(Second);
        Assert.True(In(w, () => flare.loc is turf));
        Assert.False(Own().On, "a dropped flare leaves its holder");
        ReplicaEntity onFloor = replica.Entities.Single(e => e.Id == flare.dm_id);
        Assert.True(onFloor.Light.On);
        Assert.Equal(6f, onFloor.Light.Range);
        Assert.Equal("flare-on", IconOf(replica, onFloor));

        // burns out (SSobj's process, fuel in deciseconds): the flare is the burnt flare and the light is gone
        In(w, () => flare.fuel = 15);
        w.Run(6 * Second);
        Assert.True(In(w, () => QDELETED(flare)));
        obj_item_trash_flare trash = In(w, () => Dm.each(locate(46, 122, z).contents).OfType<obj_item_trash_flare>().SingleOrDefault());
        Assert.NotNull(trash);
        Assert.Equal("flare-empty", In(w, () => trash.icon_state));
        ReplicaEntity burnt = replica.Entities.Single(e => e.Id == trash.dm_id);
        Assert.True(Dark(burnt));
        Assert.Equal("flare-empty", IconOf(replica, burnt));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
