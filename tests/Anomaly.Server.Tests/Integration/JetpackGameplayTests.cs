using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M4c slice 6 through the real server on MetaStation: a headless Godot client (<see cref="PlaySession"/>) suits up (the dev
/// console gives the EVA suit, helmet, breath mask, oxygen tank and a CO2 jetpack; the suit storage units come with slice 4,
/// the airlock cycling with slice 5), so the test puts the player in space just outside the Auxiliary Airlock (39,116). The
/// jetpack's toggle button lights it (the client sees jetpack-black-on on the button and on the back); a push east and the
/// key let go keep the player drifting (the stabilizer is off), the stabilization button stops the drift; with the
/// stabilizer on the player holds a direction toward the target tile (44,113) and steps there tile by tile (tg's client/Move
/// through the bridge's zero-gravity driver) and stays when the key is let go. The client sees the ion trail entities with
/// their ion_fade flick and hears the valve of the internals (tg's jetpack plays no sound of its own).
/// </summary>
public class JetpackGameplayTests
{
    private const int Second = 20; // ticks per second
    private const string On = "sound/items/internals/internals_on.ogg";
    private const string TrailPath = "/obj/effect/particle_effect/ion_trails";

    private readonly ITestOutputHelper _output;

    public JetpackGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void ClickScreen(RealHeadlessWorld.Player p, long id, ClickModifiers modifiers = ClickModifiers.None)
        => Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, id, 0, 0, 0, 16, 16), ClickButton.Left, modifiers));

    private static List<string> States(ReplicaWorld replica, long id, bool screen)
        => (screen ? CrewOutfitTests.Icons(replica, replica.Screen.Values.Single(e => e.Id == id).Appearance)
            : CrewOutfitTests.Icons(replica, replica.Entities.Single(e => e.Id == id).State.Appearance)).Select(i => i.State).ToList();

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

    private static long ButtonOf<T>(RealHeadlessWorld w, mob_living_carbon_human me, obj_item item) where T : datum_action
        => In(w, () => ((atom)Dm.each_cast<datum_action>(item.actions).Single(a => a is T).viewers[me.hud_used]).dm_id);

    private static (int X, int Y) Tile(RealHeadlessWorld w, mob me) => In(w, () => ((int)me.x, (int)me.y));

    // Godot's yaw for a world direction (MetaWalkTests: 0 faces north, +90 degrees west).
    private static double YawOf(int dx, int dy) => Math.Atan2(-dx, dy);

    [MetaServerFact]
    public void A_suited_player_lights_the_jetpack_drifts_stabilizes_and_flies_to_the_target_tile()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 13, TimeoutSteps = 6000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        int z = In(w, () => (int)me.z);
        int bootRuntimes = w.World.RuntimeErrorCount;

        // Suit up (SpaceSuitGameplayTests' way): the EVA suit, helmet and a breath mask on with E, the oxygen tank's valve open
        // with an alt-click in hand, the tank on the suit storage slot.
        var suit = Give<obj_item_clothing_suit_space_eva>(w, me, "/obj/item/clothing/suit/space/eva");
        QuickEquip(w, p);
        Assert.Same(suit, In(w, () => me.wear_suit));
        var helmet = Give<obj_item_clothing_head_helmet_space_eva>(w, me, "/obj/item/clothing/head/helmet/space/eva");
        QuickEquip(w, p);
        Assert.Same(helmet, In(w, () => me.head));
        var mask = Give<obj_item_clothing_mask_breath>(w, me, "/obj/item/clothing/mask/breath");
        QuickEquip(w, p);
        Assert.Same(mask, In(w, () => me.wear_mask));
        var tank = Give<obj_item_tank_internals_oxygen>(w, me, "/obj/item/tank/internals/oxygen");
        heard.Clear();
        ClickScreen(p, tank.dm_id, ClickModifiers.Alt);
        w.Run(Second);
        Assert.Same(tank, In(w, () => me.@internal));
        Assert.Contains(heard, e => e.File == On);
        ClickScreen(p, In(w, () => ((atom)me.hud_used.screen_objects[Dm.text("item_slot:[]", Defines.ITEM_SLOT_SUITSTORE)]).dm_id));
        w.Run(Second);
        Assert.Same(tank, In(w, () => me.s_store));

        // The backpack comes off the back (the test drops it: a click on a worn backpack opens it, tg takes it off by dragging,
        // which the client does not do yet) and the jetpack goes on with E.
        if (In(w, () => me.back) != null)
        {
            In(w, () => Waiting.NoSleep(me.dropItemToGround(me.back)));
            w.Run(5);
            Assert.Null(In(w, () => me.back));
        }
        var jet = Give<obj_item_tank_jetpack_carbondioxide>(w, me, "/obj/item/tank/jetpack/carbondioxide");
        QuickEquip(w, p);
        Assert.Same(jet, In(w, () => me.back));
        long toggle = ButtonOf<datum_action_item_action_toggle_jetpack>(w, me, jet);
        long stabilization = ButtonOf<datum_action_item_action_jetpack_stabilization>(w, me, jet);
        Assert.Contains(replica.Screen.Values, e => e.Id == toggle);

        // Outside: the dock in front of the Auxiliary Airlock's outer doors (38-39,118), open space (plan section 4).
        In(w, () => Waiting.NoSleep(me.forceMove(locate(39, 116, z))));
        w.Run(Second);
        Assert.Equal((39, 116), Tile(w, me));
        Assert.False(In(w, () => Dm.truthy(me.has_gravity())), "the dock is weightless");
        Assert.Equal(MotionDriver.ZeroG, w.ServerState(p).Driver);

        // Light it: the button and the back show the "-on" state.
        ClickScreen(p, toggle);
        w.Run(Second);
        Assert.Equal(1f, In(w, () => jet.on));
        Assert.Equal(0f, In(w, () => jet.stabilize));
        List<string> button = States(replica, toggle, screen: true);
        List<string> body = States(replica, me.dm_id, screen: false);
        _output.WriteLine("the toggle button: " + string.Join(", ", button) + "; the body: " + string.Join(", ", body));
        Assert.Contains("jetpack-black-on", button);
        Assert.Contains("jetpack-black-on", body); // back.dmi, the lit state (jetpack.dm:106) redrawn on the body

        // The trail entities the client sees, with their flick.
        var trails = new HashSet<long>();
        var fades = new HashSet<long>();
        // The space play test (2026-10-15): where each trail particle is drawn against the own eye (the drawn own position, OwnGlide): its
        // near-eye fade (DV46) and whether it lies ahead of the eye along the drawn motion while it shows.
        var shownAhead = new HashSet<long>();
        var shownBehind = new HashSet<long>();
        (float X, float Y) lastEye = default;
        var firstSeen = new Dictionary<long, (long Tick, (float X, float Y) Shift)>();
        void Watch(long _)
        {
            (float ex, float ey, _) = p.Session.Prediction.RenderPosition();
            (float mx, float my) = (ex - lastEye.X, ey - lastEye.Y);
            lastEye = (ex, ey);
            foreach (ReplicaEntity e in replica.Entities.Where(e => e.TypePath == TrailPath))
            {
                trails.Add(e.Id);
                Assert.False(StaticPropRule.IsCandidate(e.TypePath, e.HasPos)); // the entity layer draws it (its flick, blank, fade)
                if (!firstSeen.ContainsKey(e.Id))
                {
                    // the entity layer's rule: a particle of the own body is drawn from the drawn body, shifted by the view's lag when it appears
                    EntityState mine = replica.Entities.Single(o => o.Id == me.dm_id).State;
                    firstSeen[e.Id] = (w.Ticks, NearEyeFade.OwnShift((int)e.State.X, (int)e.State.Y, e.State.Z, (int)mine.X, (int)mine.Y, mine.Z, ex, ey));
                }
                (long since, (float sx, float sy)) = firstSeen[e.Id];
                bool flicking = w.Ticks - since < 18; // ion_fade: 3 frames of 3 ds; then tg's blank state
                if (flicking && p.Session.Poses.TryGetPose(e, out RenderPose pose))
                {
                    float px = pose.X + sx, py = pose.Y + sy;
                    if (NearEyeFade.Alpha(px - ex, py - ey) > 0.05f && mx * mx + my * my > 1e-6)
                        (((px - ex) * mx + (py - ey) * my) > 0 ? shownAhead : shownBehind).Add(e.Id);
                }
                if (!e.State.FlickAppearance.IsNone && replica.TryGetAppearance(e.State.FlickAppearance, out AppearanceEntry look)
                    && replica.ResolveString(look.IconState) == "ion_fade")
                    fades.Add(e.Id);
            }
        }

        // A push east, the key let go: the stabilizer is off, so the player keeps drifting east.
        p.Yaw = YawOf(1, 0);
        p.Keys = MoveKeys.Forward;
        w.Run(3, Watch);
        p.Keys = MoveKeys.None;
        w.Run(2, Watch);
        (int X, int Y) released = Tile(w, me);
        Assert.NotNull(In(w, () => me.drift_handler));
        w.Run(Second, Watch);
        (int X, int Y) drifted = Tile(w, me);
        _output.WriteLine($"released at {released}, drifted to {drifted}");
        Assert.True(drifted.X >= released.X + 2, $"the player drifted east: {released} -> {drifted}");
        Assert.Equal(released.Y, drifted.Y);

        // The stabilization button stops the drift.
        (int X, int Y) clicked = Tile(w, me);
        ClickScreen(p, stabilization);
        w.Run(Second / 2, Watch);
        Assert.Equal(1f, In(w, () => jet.stabilize));
        // the drift's next step halts it (after_move, drift_handler.dm:151); the handler lingers for the last glide (:212)
        w.Run(Second / 2, Watch);
        Assert.Null(In(w, () => me.drift_handler));
        (int X, int Y) halted = Tile(w, me);
        Assert.True(halted.X - clicked.X <= 2, $"the stabilizer stopped the drift: clicked at {clicked}, halted at {halted}");
        w.Run(Second, Watch);
        Assert.Equal(halted, Tile(w, me));
        _output.WriteLine($"halted at {halted}");

        // With the stabilizer on, steer to the target tile (44,113), tile by tile, and let go there.
        (int X, int Y) target = (44, 113);
        var path = new List<(int, int)> { halted };
        float moles = In(w, () => Dm.num(jet.air_contents.total_moles()));
        int still = 0;
        for (int t = 0; t < 30 * Second && still < Second; t++)
        {
            (int X, int Y) at = Tile(w, me);
            if (at != path[^1])
            {
                Assert.True(Math.Abs(at.X - path[^1].Item1) <= 1 && Math.Abs(at.Y - path[^1].Item2) <= 1, $"a step of one tile: {path[^1]} -> {at}");
                path.Add(at);
            }
            if (at == target)
            {
                p.Keys = MoveKeys.None;
                still++;
            }
            else
            {
                p.Yaw = YawOf(Math.Sign(target.X - at.X), Math.Sign(target.Y - at.Y));
                p.Keys = MoveKeys.Forward;
                still = 0;
            }
            w.Run(1, Watch);
        }
        _output.WriteLine("the flight: " + string.Join(" ", path) + $"; CO2 {moles} -> {In(w, () => Dm.num(jet.air_contents.total_moles()))}");
        Assert.Equal(target, Tile(w, me));
        w.Run(Second, Watch);
        Assert.Equal(target, Tile(w, me)); // the stabilizer holds the player there
        Assert.Null(In(w, () => me.drift_handler));
        Assert.True(In(w, () => Dm.num(jet.air_contents.total_moles())) < moles, "the flight used the CO2");
        MotionState state = w.ServerState(p);
        Assert.Equal(MotionDriver.ZeroG, state.Driver);
        Assert.Equal(target, (state.TileX, state.TileY));
        ReplicaEntity own = replica.Entities.Single(e => e.Id == me.dm_id);
        Assert.Equal(target, ((int)own.State.X, (int)own.State.Y));

        _output.WriteLine($"trail entities seen {trails.Count}, with the ion_fade flick {fades.Count}; drawn while the eye moved: behind it {shownBehind.Count}, ahead of it {shownAhead.Count}");
        Assert.True(shownBehind.Count >= 3, "the trail shows behind the flier");
        Assert.Empty(shownAhead); // never in front of the eye
        // The client's picture of a trail particle (with the converted icons): the ion_fade flick composes, then tg's blank state draws nothing
        // (no placeholder); its sparkles are see-through (alpha 200: blended).
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot() ?? IconRootFromEnvironment();
        if (root != null)
        {
            var compositor = new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(root)));
            var tables = new Tables(replica);
            ReplicaEntity particle = null;
            for (int i = 0; i < 3 * Second && particle == null; i++)
            {
                p.Yaw = YawOf(1, 0);
                p.Keys = MoveKeys.Forward;
                w.Run(1);
                particle = replica.Entities.FirstOrDefault(e => e.TypePath == TrailPath && !e.State.FlickAppearance.IsNone);
            }
            p.Keys = MoveKeys.None;
            Assert.NotNull(particle);
            Assert.True(compositor.IsBlank(tables, particle.State.Appearance, ByondDir.South), "the blanked particle draws nothing");
            Assert.True(compositor.TryCompose(tables, particle.State.FlickAppearance, ByondDir.East, 0, out ComposedRaster fade));
            Assert.True(fade.Image.IsTranslucent());
            Assert.Equal(ByondDir.East, particle.State.Dir); // the flight's direction (set_dir)
        }
        Assert.True(trails.Count >= 3, "the client saw the ion trail");
        Assert.True(fades.Count >= 3, "the client saw the trails' ion_fade flick");
        Assert.Equal(0f, In(w, () => Dm.num(me.get_brute_loss()))); // the suit holds in space
        Assert.Equal(0f, In(w, () => Dm.num(me.get_oxy_loss())));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount); // nothing the player did ran into a runtime
    }

    private static string IconRootFromEnvironment()
    {
        string root = Environment.GetEnvironmentVariable("ANOMALY_ICON_ROOT");
        return root != null && System.IO.Directory.Exists(System.IO.Path.Combine(root, "icons")) ? root : null;
    }

    // The client's ReplicaTables (godot/scripts/Render, not in this test project): the replica's tables for the compositor.
    private sealed class Tables(ReplicaWorld replica) : IAppearanceSource
    {
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => replica.TryGetAppearance(reference, out entry);
        public string ResolveString(TableRef reference) => replica.ResolveString(reference);
        public string ResolveResource(TableRef reference) => replica.ResolveResource(reference);
    }
}
