using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M7 lights slice (D142, light.dm:324-340, :516-540): on MetaStation a room's lighting channel is cut through the server
/// console (<c>areapower X Y light off</c>: the area's <c>power_light</c> and <c>power_change()</c>, as its APC does). The
/// client of a player in the room sees the fixtures change from the bulb's white to tg's emergency light (a red, quarter-range
/// light, <c>bulb_low_power_colour</c>) once the emergency cells take over, go dark when their cells run out, and return to the
/// normal light when the channel is switched on again.
/// </summary>
public class EmergencyLightGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const int AroundX = 46, AroundY = 122; // the arrivals corridor: the fixtures nearest to it are the room

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

    [MetaServerFact]
    public void A_room_whose_lighting_channel_is_cut_shows_the_emergency_light_then_dark_then_the_normal_light_again()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 71, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);

        // the room: the area of the lit fixture nearest to the corridor, and its fixtures in the working order
        obj_machinery_light anchor = In(w, () => Dm.each<obj_machinery_light>(Dm.world)
            .Where(l => Dm.eq(l.z, 1) && Dm.truthy(l.on) && l.loc is turf)
            .OrderBy(l => Math.Abs(Dm.num(l.x) - AroundX) + Math.Abs(Dm.num(l.y) - AroundY))
            .First());
        int ax = (int)In(w, () => Dm.num(anchor.x)), ay = (int)In(w, () => Dm.num(anchor.y));
        area room = In(w, () => (area)get_area(anchor));
        Assert.True(In(w, () => Dm.truthy(room.requires_power)), "a station room needs power");
        List<obj_machinery_light> fixtures = In(w, () => Dm.each<obj_machinery_light>(Dm.world)
            .Where(l => ReferenceEquals(get_area(l), room) && Dm.truthy(l.on) && Dm.eq(l.status, LIGHT_OK))
            .ToList());
        output.WriteLine($"{Dm.str(In(w, () => room.name))}: {fixtures.Count} lit fixtures, the anchor at {ax},{ay}");
        Assert.True(fixtures.Count >= 2, "the room has several fixtures");
        In(w, () => Waiting.NoSleep(me.forceMove(anchor.loc as turf)));
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        ReplicaWorld replica = alice.Session.Replica;
        List<ReplicaEntity> Seen() => fixtures.Select(f => replica.Entities.FirstOrDefault(e => e.Id == f.dm_id)).Where(e => e != null).ToList();
        string IconOf(ReplicaEntity e) => replica.TryGetAppearance(e.State.Appearance, out AppearanceEntry look) ? replica.ResolveString(look.IconState) : null;
        uint bulb = CellState.ParseColor(In(w, () => anchor.bulb_colour));
        uint emergency = CellState.ParseColor(COLOR_VIVID_RED); // bulb_low_power_colour

        // normal: the bulbs' own colour, range 8
        List<ReplicaEntity> normal = Seen();
        Assert.True(normal.Count >= 2, $"the client sees {normal.Count} fixtures of the room");
        float Brightness(ReplicaEntity e) => In(w, () => Dm.num(fixtures.Single(x => x.dm_id == e.Id).brightness));
        foreach (ReplicaEntity e in normal)
        {
            Assert.True(e.Light.On, "a lit fixture");
            Assert.Equal(Brightness(e), e.Light.Range);
            Assert.NotEqual(emergency, e.Light.Rgba);
        }
        output.WriteLine($"normal: {normal[0].Light.Rgba:X8} range {normal[0].Light.Range} icon {IconOf(normal[0])}");

        // the channel is cut (the server console); the emergency cells take over at the fixtures' next machine tick
        w.Host.EnqueueConsoleLine($"areapower {ax} {ay} light off");
        w.Run(5 * Second);
        Assert.False(In(w, () => Dm.truthy(room.power_light)));
        List<ReplicaEntity> lowPower = Seen();
        foreach (ReplicaEntity e in lowPower)
        {
            Assert.True(e.Light.On, "the emergency light is on");
            Assert.Equal(emergency, e.Light.Rgba);   // bulb_low_power_colour, light.dm:535
            // brightness * bulb_low_power_brightness_mul (a tube's 8 * 0.25), at least MINIMUM_USEFUL_LIGHT_RANGE (set_light)
            Assert.Equal(Math.Max(Brightness(e) * 0.25f, MINIMUM_USEFUL_LIGHT_RANGE), e.Light.Range);
            Assert.InRange(e.Light.Power, 0.5f, 0.75f);
            obj_machinery_light f = fixtures.Single(x => x.dm_id == e.Id);
            Assert.Equal(In(w, () => Dm.str(f.base_state)) + "_emergency", IconOf(e)); // update_icon_state, light.dm:177
        }
        output.WriteLine($"channel off: {lowPower[0].Light.Rgba:X8} range {lowPower[0].Light.Range} power {lowPower[0].Light.Power} icon {IconOf(lowPower[0])}");
        Assert.All(fixtures, f => Assert.True(In(w, () => Dm.truthy(f.low_power_mode))));
        Assert.All(fixtures, f => Assert.False(In(w, () => Dm.truthy(f.on))));

        // the cells run out: the next machine tick darkens the room
        In(w, () =>
        {
            foreach (obj_machinery_light f in fixtures)
                ((obj_item_stock_parts_power_store)f.get_cell()).charge = 1;
        });
        w.Run(5 * Second);
        foreach (ReplicaEntity e in Seen())
            Assert.True(e.Light.IsNone || !e.Light.On, "the fixture is dark once its cell is empty");
        Assert.All(fixtures, f => Assert.False(In(w, () => Dm.truthy(f.low_power_mode))));
        output.WriteLine("cells empty: " + string.Join(", ", Seen().Select(e => e.Light.IsNone ? "dark" : e.Light.Range.ToString())));

        // power returns: the normal light again
        w.Host.EnqueueConsoleLine($"areapower {ax} {ay} light on");
        w.Run(3 * Second);
        List<ReplicaEntity> back = Seen();
        Assert.Equal(normal.Count, back.Count);
        foreach (ReplicaEntity e in back)
        {
            Assert.True(e.Light.On);
            Assert.Equal(Brightness(e), e.Light.Range);
            Assert.NotEqual(emergency, e.Light.Rgba);
        }
        Assert.All(fixtures, f => Assert.True(In(w, () => Dm.truthy(f.on))));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount); // nothing the power cut ran into a runtime
    }
}
