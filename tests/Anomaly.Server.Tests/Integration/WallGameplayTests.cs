using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M5 slice 4 through the real server on MetaStation, with a headless client (<see cref="PlaySession"/>): in the Auxiliary Base
/// Construction zone (plating x 43-51, y 151-159, closed by walls; the plan's section 4) a player at (43,155) lights a welder
/// and clicks the west wall (42,155), which has space beyond it: tg's try_decon (walls.dm:233) slices it in 10 s, the turf
/// scrapes away to plating with a girder and two iron sheets, and the client's turf grid and entities show it. The zone vents
/// into space through the girder's tile; the player hears the space wind and the zone falls below 10 kPa. Two iron sheets on
/// the girder build the wall again (the girder wall recipe, 4 s); the player opens the zone's airlock with an all-access card
/// the room's air spreads into the zone and five air canisters refill both. Last, a room floor tile is pried up with a crowbar and tiled back.
/// </summary>
public class WallGameplayTests
{
    private const int Second = 20; // ticks per second
    private const int WallX = 42, WallY = 155; // the zone's west wall, space beyond
    private const int ZoneX = 47, ZoneY = 155; // the middle of the zone
    private const int DoorX = 47, DoorY = 150; // the zone's airlock ("Construction Zone", access aux_base)
    private const string SpaceWind = "sound/effects/space_wind.ogg";
    private const string Welder = "sound/items/tools/welder.ogg";

    private readonly ITestOutputHelper _output;

    public WallGameplayTests(ITestOutputHelper output) => _output = output;

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

    private static T Give<T>(RealHeadlessWorld w, mob_living_carbon_human me, string path) where T : obj_item
    {
        using (w.World.Enter())
        {
            foreach (object held in Dm.each(me.held_items.Copy()))
            {
                if (held is obj_item old)
                    GlobalProcs.qdel(old);
            }
            DevCommands.Give(me, path);
            return me.get_active_held_item() as T;
        }
    }

    private static float Kpa(RealHeadlessWorld w, int x, int y)
        => In(w, () => Builtins.locate(x, y, 1) is turf_open t && t.air != null ? Dm.num(t.air.return_pressure()) : -1f);

    private static List<T> Near<T>(RealHeadlessWorld w, int x, int y, int radius)
        => In(w, () => Enumerable.Range(x - radius, 2 * radius + 1).SelectMany(cx => Enumerable.Range(y - radius, 2 * radius + 1)
            .SelectMany(cy => Builtins.locate(cx, cy, 1) is turf t ? Dm.each_cast<atom>(t.contents) : Enumerable.Empty<atom>())).OfType<T>().ToList());

    private static bool ClickTurf(RealHeadlessWorld.Player p, int x, int y)
        => p.Session.Click(new ClickTarget(TargetKind.Turf, 0, x, y, 1), ClickButton.Left, ClickModifiers.None);

    [MetaServerFact]
    public void A_welded_wall_vents_the_aux_base_zone_iron_on_its_girder_rebuilds_it_and_a_floor_tile_is_pried_and_put_back()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 54, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        MetaServer.ClearAuxBase(w.World); // the test works in the empty construction zone
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<(long Tick, SoundEvent Sound)>();
        replica.SoundsReceived += events => heard.AddRange(events.Select(e => (w.Ticks, e)));
        // The player stays in the venting zone for minutes: tg's godmode keeps him from the vacuum's damage (the test's, not tg's).
        using (w.World.Enter())
            GlobalProcs.ADD_TRAIT(me, Defines.TRAIT_GODMODE, "test");
        Put(w, me, WallX + 1, WallY);
        Assert.IsType<turf_closed_wall>(In(w, () => Builtins.locate(WallX, WallY, 1)));
        Assert.IsAssignableFrom<turf_open_space>(In(w, () => Builtins.locate(WallX - 1, WallY, 1)));
        Assert.Equal(101.3f, Kpa(w, ZoneX, ZoneY), 0.5f);
        Assert.Equal("/turf/closed/wall", replica.TurfTypeAt(WallX, WallY, 1));

        // ---- 1. The welder (Z lights it), a click on the wall: 10 s of slicing (walls.dm:233), then plating, a girder, two iron.
        var welder = Give<obj_item_weldingtool>(w, me, "/obj/item/weldingtool");
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(welder.welding)), "the welder is lit");
        long cut = w.Ticks;
        Assert.True(ClickTurf(p, WallX, WallY));
        w.Run(9 * Second);
        Assert.IsType<turf_closed_wall>(In(w, () => Builtins.locate(WallX, WallY, 1)));
        w.Run(2 * Second);
        Assert.IsType<turf_open_floor_plating>(In(w, () => Builtins.locate(WallX, WallY, 1)));
        var girder = In(w, () => Dm.each_cast<atom>(Builtins.locate(WallX, WallY, 1).contents).OfType<obj_structure_girder>().SingleOrDefault());
        Assert.NotNull(girder);
        var sheets = Near<obj_item_stack_sheet_iron>(w, WallX - 3, WallY, 4);
        Assert.Equal(2f, sheets.Sum(s => In(w, () => Dm.num(s.amount))));
        Assert.Contains(heard, h => h.Tick >= cut && h.Sound.Kind == SoundKind.Play && h.Sound.File == Welder);
        // the client: the wall cell became plating (the turf replicates, MapRenderer.OnCellsChanged redraws its chunk) and the girder
        // is an entity with tg's smoothed girder picture
        w.Run(Second);
        Assert.Equal("/turf/open/floor/plating", replica.TurfTypeAt(WallX, WallY, 1));
        ReplicaEntity seenGirder = Assert.Single(replica.Entities, e => e.Id == girder.dm_id);
        Assert.True(replica.TryGetAppearance(seenGirder.State.Appearance, out AppearanceEntry girderLook));
        string girderState = replica.ResolveString(girderLook.IconState);
        _output.WriteLine($"the girder's picture: {replica.ResolveResource(girderLook.Icon)} {girderState}");
        Assert.StartsWith("girder-", girderState, StringComparison.Ordinal);

        // ---- 2. The zone vents into space through the girder's tile; the player hears the space wind (living.dm:1112).
        float zone = Kpa(w, ZoneX, ZoneY);
        long vented = w.Ticks;
        while (zone >= 10f && w.Ticks - vented < 240 * Second)
        {
            w.Run(Second);
            zone = Kpa(w, ZoneX, ZoneY);
        }
        _output.WriteLine($"the zone at {zone:0.0} kPa {(w.Ticks - vented) / (float)Second:0} s after the cut; space wind heard {heard.Count(h => h.Sound.File == SpaceWind)} times");
        Assert.True(zone < 10f, "the zone vents below 10 kPa: " + zone);
        Assert.Contains(heard, h => h.Tick >= cut && h.Sound.File == SpaceWind);
        Assert.Equal(101.3f, Kpa(w, 47, 148), 5f); // the closed airlock and the windows keep the room's air

        // ---- 3. Two iron on the girder: the girder wall recipe (4 s) builds the wall again.
        var iron = Give<obj_item_stack_sheet_iron>(w, me, "/obj/item/stack/sheet/iron/fifty");
        Put(w, me, WallX + 1, WallY);
        long rebuilt = w.Ticks;
        Assert.True(p.Session.Click(girder.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(3 * Second);
        Assert.IsType<turf_open_floor_plating>(In(w, () => Builtins.locate(WallX, WallY, 1)));
        w.Run(2 * Second);
        Assert.IsType<turf_closed_wall>(In(w, () => Builtins.locate(WallX, WallY, 1)));
        Assert.Equal(48f, In(w, () => Dm.num(iron.amount)));
        Assert.True(In(w, () => GlobalProcs.QDELETED(girder)));
        w.Run(Second);
        Assert.Equal("/turf/closed/wall", replica.TurfTypeAt(WallX, WallY, 1));
        Assert.DoesNotContain(replica.Entities, e => e.Id == girder.dm_id);
        _output.WriteLine($"rebuilt in {(w.Ticks - rebuilt) / (float)Second:0} s");

        // ---- 4. The airlock opened with an all-access card: the room's air spreads into the zone; air canisters refill both.
        Give<obj_item_card_id>(w, me, "/obj/item/card/id/advanced/gold/captains_spare");
        Put(w, me, DoorX, DoorY + 1);
        var door = In(w, () => Dm.each_cast<atom>(Builtins.locate(DoorX, DoorY, 1).contents).OfType<obj_machinery_door_airlock>().Single());
        Assert.True(p.Session.Click(door.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(2 * Second);
        Assert.False(In(w, () => Dm.truthy(door.density)), "the airlock opened");
        w.Run(10 * Second);
        float shared = Kpa(w, ZoneX, ZoneY);
        _output.WriteLine($"the room's air spread into the zone: {shared:0.0} kPa");
        Assert.InRange(shared, 15f, 40f); // the room's 27 tiles at 101 kPa over the room and the zone's 108

        // The room's one vent refills 108 tiles slowly (tg's pump rate: 24.6 kPa after 10 minutes in a trial run), so five air
        // canisters are brought in and their valves opened (the dev command standing for the canister window's button, as the M4b
        // demo does): tg's canister/air holds 4.5 MPa in 1000 L at a release pressure of one atmosphere.
        for (int i = 0; i < 5; i++)
            w.Host.EnqueueConsoleLine($"spawn /obj/machinery/portable_atmospherics/canister/air {44 + i} 157 1");
        w.Run(5);
        Put(w, me, 46, 156);
        for (int i = 0; i < 5; i++)
            p.Session.DevCommand($"valve {44 + i} 157 1");
        long opened = w.Ticks;
        while (zone < 90f && w.Ticks - opened < 120 * Second)
        {
            w.Run(5 * Second);
            zone = Kpa(w, ZoneX, ZoneY);
        }
        _output.WriteLine($"the zone at {zone:0.0} kPa {(w.Ticks - opened) / (float)Second:0} s after the valves opened");
        Assert.True(zone >= 90f, "the canisters refill the zone and the room: " + zone);

        // ---- 5. A room floor tile pried up (floor.dm:144) and put back (plating.dm:73).
        const int FloorX = 45, FloorY = 149;
        Put(w, me, FloorX + 1, FloorY);
        Assert.IsType<turf_open_floor_iron>(In(w, () => Builtins.locate(FloorX, FloorY, 1)));
        Give<obj_item_crowbar>(w, me, "/obj/item/crowbar");
        Assert.True(ClickTurf(p, FloorX, FloorY));
        w.Run(Second);
        Assert.IsType<turf_open_floor_plating>(In(w, () => Builtins.locate(FloorX, FloorY, 1)));
        var tile = In(w, () => Dm.each_cast<atom>(Builtins.locate(FloorX, FloorY, 1).contents).OfType<obj_item_stack_tile_iron>().Single());
        w.Run(Second);
        Assert.Equal("/turf/open/floor/plating", replica.TurfTypeAt(FloorX, FloorY, 1));
        Assert.Contains(replica.Entities, e => e.Id == tile.dm_id);
        Give<obj_item_stack_tile_iron>(w, me, "/obj/item/stack/tile/iron/four");
        Assert.True(ClickTurf(p, FloorX, FloorY));
        w.Run(Second);
        Assert.IsType<turf_open_floor_iron>(In(w, () => Builtins.locate(FloorX, FloorY, 1)));
        w.Run(Second);
        Assert.Equal("/turf/open/floor/iron", replica.TurfTypeAt(FloorX, FloorY, 1));
    }
}
