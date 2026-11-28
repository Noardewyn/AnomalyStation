using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M7x slice A (D177) through the real server and two real clients: Alice's clicks fire a test projectile (the dev hook <c>shoot</c>,
/// before the guns) along her camera's aim (protocol 34's aim, DV94). Aimed at Bob's head, the shot burns his head and nothing else; aimed
/// beside him, it flies past him to the wall behind; Alice's client gets the shot's line (NetField.Shot) at the height her eye and aim
/// gave it. And the server's body-part grid (the committed masks) is the zone grid the client finds on Bob's picture (risk R1).
/// </summary>
public class AimedShotGameplayTests(ITestOutputHelper output)
{
    private const string Hitscan = "/obj/projectile/beam/emitter/hitscan";

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

    // Alice's camera ray from her eye (her own predicted position, 1.3 m up) to the point (east, north, up) in tile units (a tile's centre at
    // its x) and metres up, as the client's pick ray holds it; the session turns it into the wire's aim.
    private static ShotAim AimAt(RealHeadlessWorld.Player p, double x, double y, double up)
    {
        (float ox, float oy, int z) = p.Session.Prediction.RenderPosition();
        double eastE = (ox - 0.5) * 2, northE = (oy - 0.5) * 2, upE = WorldScale.DefaultEyeHeight;
        double dx = (x - 0.5) * 2 - eastE, dy = (y - 0.5) * 2 - northE, dz = up - upE, length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        return PlaySession.AimOf(new PickRay(eastE, northE, upE, z, dx / length, dy / length, dz / length, 10));
    }

    private static (RealHeadlessWorld W, RealHeadlessWorld.Player Alice, RealHeadlessWorld.Player Bob, mob_living_carbon_human BobMob) Range(ulong seed)
    {
        var w = new RealHeadlessWorld(new LoopbackOptions { Seed = seed, TimeoutSteps = 2000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        bob.Yaw = Math.PI / 2; // Bob faces west, toward Alice (Godot's yaw turns left)
        In(w, () =>
        {
            turf start = get_turf(aliceMob);
            Waiting.NoSleep(bobMob.forceMove(locate(Dm.num(start.x) + 4, Dm.num(start.y), Dm.num(start.z))));
        });
        w.Run(10);
        alice.Session.DevCommand("shoot me " + Hitscan);
        w.Run(5);
        Assert.Equal(typeof(obj_projectile_beam_emitter_hitscan), In(w, () => aliceMob.client.dm_dev_shot));
        return (w, alice, bob, bobMob);
    }

    private static float Burn(RealHeadlessWorld w, mob_living_carbon_human mob, string zone)
        => In(w, () => Dm.num(((obj_item_bodypart)mob.get_bodypart(zone)).burn_dam));

    [Fact]
    public void A_shot_aimed_at_the_head_burns_the_head_and_the_client_draws_it_at_the_eyes_height()
    {
        (RealHeadlessWorld w, RealHeadlessWorld.Player alice, _, mob_living_carbon_human bobMob) = Range(31);
        using (w)
        {
            (double bx, double by) = In(w, () => LocomotionSeam.TryGetState(w.World, bobMob.dm_id, out Anomaly.Core.Motion.MotionState s, out _)
                ? (s.X / 256.0, s.Y / 256.0) : (Dm.num(bobMob.x), Dm.num(bobMob.y)));
            // the head's middle row (tg's icon-y 27 of the mob-scaled picture: 26.5 x 0.05 m)
            ShotAim aim = AimAt(alice, bx, by, 26.5 * 0.05);
            var shots = new Dictionary<long, (string Type, ShotState Shot, int X, int Y)>();
            void Watch(long _)
            {
                foreach (ReplicaEntity e in alice.Session.Replica.Entities)
                    if (e.Shot is ShotState shot)
                        shots[e.Id] = (e.TypePath ?? "", shot, e.State.X, e.State.Y);
            }
            Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 27, AimZone.Head), ClickButton.Left, ClickModifiers.None, aim));
            w.Run(10, Watch);
            Assert.True(Burn(w, bobMob, BODY_ZONE_HEAD) > 0, "the head burns");
            foreach (string zone in new[] { BODY_ZONE_CHEST, BODY_ZONE_L_ARM, BODY_ZONE_R_ARM, BODY_ZONE_L_LEG, BODY_ZONE_R_LEG })
                Assert.Equal(0f, Burn(w, bobMob, zone));
            // the client's line: the tracer and the muzzle start at the eye's 1.3 m (20.8 px), the impact at the head's height
            foreach (var s in shots.Values)
                output.WriteLine($"{s.Type} at {s.X},{s.Y}: from {s.Shot.OriginX},{s.Shot.OriginY} h {s.Shot.HeightPixels:0.00} slope {s.Shot.SlopeValue:0.0000} angle {s.Shot.AngleDegrees:0.0}");
            var tracer = shots.Values.First(s => s.Type.StartsWith("/obj/effect/projectile/tracer", StringComparison.Ordinal));
            Assert.Equal(20.8, tracer.Shot.HeightPixels, 1);
            Assert.InRange(tracer.Shot.AngleDegrees, 80, 100); // east
            var impact = shots.Values.First(s => s.Type.StartsWith("/obj/effect/projectile/impact", StringComparison.Ordinal));
            Assert.InRange(impact.Shot.HeightPixels, 18.8, 22.8); // the head's rows, 0.94 m to 1.14 m at the mob scale
            Assert.Equal(0, impact.Shot.Slope);
            Assert.Equal(0, w.Host.Sessions.Single(s => s.Key == "Alice").AimsCorrected);
        }
    }

    /// <summary>
    /// Package T, slice T4a: a click aimed at the eyes selects tg's <c>BODY_ZONE_PRECISE_EYES</c>, which the shot's first <c>def_zone</c> takes (tg's
    /// <c>zone_selected</c>); the line crosses Bob's head on the server's six-zone mirror (DV95, ShotGeometry.Nearest puts that zone in its place, and
    /// tg's check_zone would read the eyes as the head too): the head burns and nothing else.
    /// </summary>
    [Fact]
    public void A_shot_aimed_at_the_eyes_burns_the_head()
    {
        (RealHeadlessWorld w, RealHeadlessWorld.Player alice, _, mob_living_carbon_human bobMob) = Range(35);
        using (w)
        {
            var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
            (double bx, double by) = In(w, () => LocomotionSeam.TryGetState(w.World, bobMob.dm_id, out Anomaly.Core.Motion.MotionState s, out _)
                ? (s.X / 256.0, s.Y / 256.0) : (Dm.num(bobMob.x), Dm.num(bobMob.y)));
            ShotAim aim = AimAt(alice, bx, by, 26.5 * 0.05); // the eyes' middle row, tg's icon-y 26
            Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 26, AimZone.Eyes), ClickButton.Left, ClickModifiers.None, aim));
            w.Run(10);
            Assert.Equal(BODY_ZONE_PRECISE_EYES, In(w, () => aliceMob.zone_selected));
            Assert.True(Burn(w, bobMob, BODY_ZONE_HEAD) > 0, "the head burns");
            foreach (string zone in new[] { BODY_ZONE_CHEST, BODY_ZONE_L_ARM, BODY_ZONE_R_ARM, BODY_ZONE_L_LEG, BODY_ZONE_R_LEG })
                Assert.Equal(0f, Burn(w, bobMob, zone));
        }
    }

    [Fact]
    public void A_shot_aimed_beside_the_body_flies_past_it_to_the_wall_behind()
    {
        (RealHeadlessWorld w, RealHeadlessWorld.Player alice, _, mob_living_carbon_human bobMob) = Range(32);
        using (w)
        {
            (double bx, double by) = In(w, () => LocomotionSeam.TryGetState(w.World, bobMob.dm_id, out Anomaly.Core.Motion.MotionState s, out _)
                ? (s.X / 256.0, s.Y / 256.0) : (Dm.num(bobMob.x), Dm.num(bobMob.y)));
            turf wall = In(w, () =>
            {
                turf t = get_turf(bobMob);
                while (t != null && t is not turf_closed)
                    t = get_step(t, Dm.EAST) as turf;
                return t;
            });
            Assert.NotNull(wall);
            // past his shoulder: 0.6 tile (19 px, his picture reaches 7 px either way) north of his centre at 1.2 m, and on to the wall
            ShotAim aim = AimAt(alice, bx, by + 0.6, 1.2);
            var impacts = new HashSet<(int, int)>();
            void Watch(long _)
            {
                foreach (ReplicaEntity e in alice.Session.Replica.Entities)
                    if (e.Shot.HasValue && (e.TypePath ?? "").StartsWith("/obj/effect/projectile/impact", StringComparison.Ordinal))
                        impacts.Add((e.State.X, e.State.Y));
            }
            float before = In(w, () => Dm.num(bobMob.get_fire_loss()));
            Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 1, 18), ClickButton.Left, ClickModifiers.None, aim));
            w.Run(10, Watch);
            Assert.Equal(before, In(w, () => Dm.num(bobMob.get_fire_loss())));
            output.WriteLine("impacts at " + string.Join(" ", impacts));
            Assert.Contains(impacts, i => i.Item1 == (int)In(w, () => Dm.num(wall.x)));
        }
    }

    [Fact]
    public void The_mouse_actions_reach_tg_and_an_eye_far_from_the_body_is_replaced_by_the_servers()
    {
        (RealHeadlessWorld w, RealHeadlessWorld.Player alice, _, mob_living_carbon_human bobMob) = Range(34);
        using (w)
        {
            Session session = w.Host.Sessions.Single(s => s.Key == "Alice");
            var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
            var target = new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 18);
            ShotAim aim = AimAt(alice, Dm.num(bobMob.x), Dm.num(bobMob.y), 0.9);
            alice.Session.MouseDown(target, ClickButton.Left, ClickModifiers.None, aim, w.Now);
            w.Run(3);
            Assert.Same(bobMob, In(w, () => aliceMob.client.dm_mouse_down));
            alice.Session.MouseDrag(default, ClickModifiers.None, aim, w.Now + 1); // over nothing
            alice.Session.MouseUp(default, ClickModifiers.None, aim);
            w.Run(3);
            Assert.Null(In(w, () => aliceMob.client.dm_mouse_down));
            Assert.Equal(3, session.MouseActionsApplied);
            Assert.Equal(0, session.AimsCorrected);
            // an eye three tiles from her body is not taken: the server's own eye fires the shot, from her tile
            float before = In(w, () => Dm.num(bobMob.get_fire_loss()));
            ShotAim far = aim with { East = aim.East + 3 * 256 };
            Assert.True(alice.Session.Click(target, ClickButton.Left, ClickModifiers.None, far));
            w.Run(5);
            Assert.Equal(1, session.AimsCorrected);
            Assert.True(In(w, () => Dm.num(bobMob.get_fire_loss())) > before, "the corrected shot still flies from her eye toward the chest");
        }
    }

    [Fact]
    public void The_outfit_and_shoot_dev_verbs_parse()
    {
        Assert.True(DevCommandLine.TryParse("outfit /datum/outfit/job/assistant", out DevCommand outfit, out string error), error);
        Assert.Equal((DevVerb.Outfit, "me", "/datum/outfit/job/assistant"), (outfit.Verb, outfit.Who, outfit.TypePath));
        Assert.True(DevCommandLine.TryParse("outfit Bob /datum/outfit/job/security", out outfit, out error), error);
        Assert.Equal("Bob", outfit.Who);
        Assert.False(DevCommandLine.TryParse("outfit me /obj/item/crowbar", out _, out _));
        Assert.True(DevCommandLine.TryParse("shoot me " + Hitscan, out DevCommand shoot, out error), error);
        Assert.Equal((DevVerb.Shoot, Hitscan), (shoot.Verb, shoot.TypePath));
        Assert.True(DevCommandLine.TryParse("shoot off", out shoot, out error), error);
        Assert.Null(shoot.TypePath);
        Assert.False(DevCommandLine.TryParse("shoot me /obj/item/crowbar", out _, out _));
    }

    [Fact]
    public void The_servers_body_part_grid_is_the_zone_grid_the_client_finds_on_the_picture()
    {
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return; // no tg icons here
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 33, TimeoutSteps = 400 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        w.Run(10);
        ReplicaEntity seen = alice.Session.Replica.Entities.Single(e => e.Id == bobMob.dm_id);
        var compositor = new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(root)));
        var tables = new Tables(alice.Session.Replica);
        int compared = 0;
        foreach (byte dir in new byte[] { 1, 2, 4, 8 })
            for (int y = 1; y <= 32; y++)
                for (int x = 1; x <= 32; x++)
                {
                    // slice T4a: the client's eyes, mouth and groin are the head's and the chest's (tg's deprecise_zone); the server's shot reads limbs (DV95)
                    AimZone client = BodyZoneMap.LimbOf(BodyZoneAim.ZoneAt(tables, compositor, seen.State.Appearance, dir, (byte)x, (byte)y));
                    string server = In(w, () => ShotGeometry.ZoneAt(bobMob, dir, x, y));
                    Assert.True(Same(client, server), $"dir {dir} at {x},{y}: the client finds {client}, the server {server ?? "none"}");
                    compared++;
                }
        Assert.Equal(4 * 32 * 32, compared);
        // slice T2: where the legs overlap seen from the side, both name the near leg tg draws on top (handle_masking's leg masks): in W the left
        // leg over the right one in columns 16 to 18 (15 is the right leg alone), in E the mirror (columns 15 to 17 the right leg, 18 the left)
        foreach ((byte dir, int x, int y, AimZone near) in new (byte, int, int, AimZone)[]
        {
            (8, 15, 5, AimZone.RightLeg), (8, 16, 5, AimZone.LeftLeg), (8, 17, 5, AimZone.LeftLeg), (8, 18, 2, AimZone.LeftLeg),
            (4, 15, 5, AimZone.RightLeg), (4, 16, 5, AimZone.RightLeg), (4, 17, 2, AimZone.RightLeg), (4, 18, 5, AimZone.LeftLeg),
        })
        {
            Assert.Equal(near, BodyZoneProbeTests.TopPart(tables, seen.State.Appearance, dir)(x, y));
            Assert.Equal(near, BodyZoneAim.ZoneAt(tables, compositor, seen.State.Appearance, dir, (byte)x, (byte)y));
            Assert.True(Same(near, In(w, () => ShotGeometry.ZoneAt(bobMob, dir, x, y))), $"dir {dir} at {x},{y}: the server reads {In(w, () => ShotGeometry.ZoneAt(bobMob, dir, x, y))}");
        }
    }

    internal static bool Same(AimZone client, string server) => client switch
    {
        AimZone.Head => server == BODY_ZONE_HEAD,
        AimZone.Chest => server == BODY_ZONE_CHEST,
        AimZone.LeftArm => server == BODY_ZONE_L_ARM,
        AimZone.RightArm => server == BODY_ZONE_R_ARM,
        AimZone.LeftLeg => server == BODY_ZONE_L_LEG,
        AimZone.RightLeg => server == BODY_ZONE_R_LEG,
        _ => server == null,
    };

    private sealed class Tables(ReplicaWorld replica) : IAppearanceSource
    {
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => replica.TryGetAppearance(reference, out entry);
        public string ResolveString(TableRef reference) => replica.ResolveString(reference);
        public string ResolveResource(TableRef reference) => replica.ResolveResource(reference);
    }
    /// <summary>
    /// DV527 (protocol 88) through a network with 4 frames of latency each way: Bob runs north past Alice; she aims at the head of the Bob her
    /// client draws, which is behind the server's Bob by the render delay and the latency, and the shot burns his head where she saw it.
    /// </summary>
    [Fact]
    public void A_shot_at_a_running_mob_hits_where_the_shooters_client_drew_it()
    {
        var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 35, TimeoutSteps = 2000, MinLatencySteps = 4, MaxLatencySteps = 4 });
        using (w)
        {
            RealHeadlessWorld.Player alice = w.Join("Alice");
            RealHeadlessWorld.Player bob = w.Join("Bob");
            var bobMob = (mob_living_carbon_human)w.MobOf(bob);
            In(w, () =>
            {
                turf start = get_turf(w.MobOf(alice));
                Waiting.NoSleep(bobMob.forceMove(locate(Dm.num(start.x) + 4, Dm.num(start.y) - 2, Dm.num(start.z))));
            });
            w.Run(10);
            alice.Session.DevCommand("shoot me " + Hitscan);
            w.Run(10);
            bob.Yaw = 0; // north
            bob.Keys = MoveKeys.Forward;
            w.Run(16); // a run of 0.33 tile a tick
            Assert.True(alice.Session.Replica.TryGet(bobMob.dm_id, out ReplicaEntity seen));
            Assert.True(alice.Session.Poses.TryGetPose(seen, out RenderPose drawn));
            (double sx, double sy) = In(w, () => LocomotionSeam.TryGetState(w.World, bobMob.dm_id, out Anomaly.Core.Motion.MotionState s, out _)
                ? (s.X / 256.0, s.Y / 256.0) : (Dm.num(bobMob.x), Dm.num(bobMob.y)));
            output.WriteLine($"drawn Bob {drawn.X:0.00},{drawn.Y:0.00} at the render tick {alice.Session.RenderTick:0.00}; the server's {sx:0.00},{sy:0.00} at {w.World.Tick}");
            Assert.True(sy - drawn.Y > 0.5, "the drawn Bob is well behind the server's");
            ShotAim aim = AimAt(alice, drawn.X, drawn.Y, 26.5 * 0.05);
            Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 27, AimZone.Head), ClickButton.Left, ClickModifiers.None, aim));
            w.Run(10);
            Assert.True(Burn(w, bobMob, BODY_ZONE_HEAD) > 0, "the head burns where Alice saw it");
        }
    }
}
