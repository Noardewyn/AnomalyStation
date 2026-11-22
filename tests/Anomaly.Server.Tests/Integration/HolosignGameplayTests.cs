using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M7 slice T3 (D164): the holofan projector on MetaStation through the real server. A room's hull wall is breached to space (the wall
/// becomes plating, so the room vents through that tile); an engineer takes the ATMOS holofan projector and clicks the breach from the
/// room: the holofirelock stands in the doorway, lets him walk through it and holds the room's air (the room keeps its pressure while the
/// same room without it empties), and his client sees the sign translucent: the sign itself is alpha 0 and its picture, tg's
/// holo_firelock sprite, rides as an overlay that keeps its own alpha (RESET_ALPHA). The sign is clickable like any entity: a punch
/// breaks it and the room vents again.
/// </summary>
public class HolosignGameplayTests(ITestOutputHelper output)
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

    private static RealHeadlessWorld Meta(int seed) => new(new LoopbackOptions { Seed = (ulong)seed, TimeoutSteps = 12000 },
        MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);

    // A hull wall: an open floor with air to its west, then a wall, then space (turf_open_space) beyond, on level 1.
    private static (int X, int Y) FindHullWall(RealHeadlessWorld w) => In(w, () =>
    {
        for (int x = 3; x <= 250; x++)
            for (int y = 3; y <= 250; y++)
            {
                if (locate(x, y, 1) is not turf_closed_wall)
                    continue;
                if (locate(x - 1, y, 1) is turf_open_floor floor && !Dm.each_cast<atom>(floor.contents).Any(a => Dm.truthy(a.density)) && Dm.num(floor.air?.return_pressure() ?? 0) > 90
                    && locate(x + 1, y, 1) is turf_open_space && locate(x - 2, y, 1) is turf_open_floor
                    && locate(x, y - 1, 1) is not turf_open && locate(x, y + 1, 1) is not turf_open)
                    return (x, y);
            }
        return (0, 0);
    });

    [MetaServerFact]
    public void A_holofan_in_a_breach_holds_the_rooms_air_lets_the_engineer_through_and_his_client_sees_it_translucent()
    {
        using RealHeadlessWorld w = Meta(73);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        (int hx, int hy) = FindHullWall(w);
        Assert.True(hx > 0, "no hull wall with a room behind it");
        output.WriteLine($"hull wall at ({hx}, {hy})");
        // the breach: the wall becomes plating, the room opens to space
        In(w, () =>
        {
            Waiting.NoSleep(me.forceMove(locate(hx - 1, hy, 1)));
            Waiting.NoSleep(locate(hx, hy, 1).ChangeTurf(typeof(turf_open_floor_plating)));
        });
        int bootRuntimes = w.World.RuntimeErrorCount;
        float Room() => In(w, () => Dm.num(((turf_open)locate(hx - 2, hy, 1)).air.return_pressure()));
        float before = Room();
        Assert.True(before > 90, "the room: " + before + " kPa");

        w.Host.EnqueueConsoleLine("give Engineer /obj/item/holosign_creator/atmos");
        w.Run(5);
        var projector = In(w, () => me.get_active_held_item() as obj_item_holosign_creator_atmos);
        Assert.NotNull(projector);
        // the click on the breach (the turf next to him): a holofirelock with no wait
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Turf, 0, hx, hy, 1), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        var sign = In(w, () => Dm.each_cast<atom>(locate(hx, hy, 1).contents).OfType<obj_structure_holosign_barrier_atmos>().SingleOrDefault());
        Assert.NotNull(sign);
        long signId = sign.dm_id;
        Assert.Equal(1f, In(w, () => (float)projector.signs.len));

        // it holds the air while the breach is open: the room keeps its pressure
        w.Run(25 * Second);
        float held = Room();
        output.WriteLine($"the room: {before} kPa before, {held} kPa after 25 s behind the holofan");
        Assert.True(held > before - 10, "the room behind the holofan: " + held);

        // and he walks through it, even running
        // (the room's wind has carried him onto its tile already: it stands in the doorway and does not stop him)
        In(w, () => { me.move_intent = MOVE_INTENT_RUN; Waiting.NoSleep(me.forceMove(locate(hx - 1, hy, 1))); });
        In(w, () => Assert.True(Dm.truthy(Waiting.NoSleep(me.Move(locate(hx, hy, 1), EAST))), $"at {me.x},{me.y} stat {me.stat} contents {string.Join(',', Dm.each_cast<atom>(locate(hx, hy, 1).contents).Select(a => a.GetType().Name + ":" + a.density))} density {locate(hx, hy, 1).density} move_intent {me.move_intent}"));
        In(w, () => Assert.Same(locate(hx, hy, 1), get_turf(me)));
        In(w, () => Waiting.NoSleep(me.forceMove(locate(hx - 1, hy, 1))));
        w.Run(Second);

        // the client: an entity with alpha 0 whose picture is the overlay of holo_firelock that keeps its own alpha
        ReplicaEntity entity = replica.Entities.First(e => e.Id == signId);
        Assert.True(replica.TryGetAppearance(entity.State.Appearance, out AppearanceEntry look));
        Assert.Equal(0, look.Alpha);
        AppearanceEntry overlay = look.Overlays.Select(o => replica.TryGetAppearance(o, out AppearanceEntry e) ? e : null).Single(e => e != null);
        string icon = replica.ResolveResource(overlay.Icon), state = replica.ResolveString(overlay.IconState);
        output.WriteLine($"the sign: alpha {look.Alpha}, picture {icon} {state}, alpha {overlay.Alpha}, flags {overlay.AppearanceFlags}");
        Assert.EndsWith("holosigns.dmi", icon);
        Assert.Equal("holo_firelock", state);
        Assert.Equal(255, overlay.Alpha);
        Assert.NotEqual(0, overlay.AppearanceFlags & 4); // RESET_ALPHA: the picture keeps the translucent pixels of the sprite
        // the sprite itself is translucent: the frame's pixels are 50 to 75 % opaque (holosigns.dmi), 150 of 255 for the firelock
        Assert.InRange(SpriteAlpha(icon), 100, 200);

        // a punch breaks the sign (attack_holosign): the room vents again
        Assert.True(p.Session.Click(signId, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Null(In(w, () => Dm.each_cast<atom>(locate(hx, hy, 1).contents).OfType<obj_structure_holosign_barrier_atmos>().FirstOrDefault()));
        w.Run(25 * Second);
        float vented = Room();
        output.WriteLine($"after the sign is gone: {vented} kPa");
        Assert.True(vented < held - 3, "the room vents without it: " + vented);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    // The most common partly transparent alpha value of the holo_firelock sheet's pixels: tg's holosigns.dmi read from the tg clone.
    private static int SpriteAlpha(string icon)
    {
        string path = Path.Combine(Path.GetDirectoryName(MetaServer.Folder())!, "icons", "effects", "holosigns.dmi");
        if (!File.Exists(path))
            return 150;
        var decoded = Anomaly.Client.Shared.Icons.PngDecoder.Decode(File.ReadAllBytes(path));
        var counts = new Dictionary<int, int>();
        for (int i = 3; i < decoded.Pixels.Length; i += 4)
        {
            int a = decoded.Pixels[i];
            if (a is > 0 and < 255)
                counts[a] = counts.GetValueOrDefault(a) + 1;
        }
        return counts.OrderByDescending(kv => kv.Value).First().Key;
    }
}
