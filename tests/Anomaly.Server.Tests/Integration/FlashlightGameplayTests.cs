using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Light;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The owner (2026-10-17): M4a's flashlight. A flashlight in a hand turns on with Z (tg's attack_self, flashlight.dm:111) and
/// its overlay light (overlay_lighting.dm) is shown on the player: the player's entity carries it on the wire (DV61), cast two
/// tiles ahead in the way the player faces, and the client's light grid lights the corridor ahead of the player. Dropped, the
/// flashlight lights from the floor; turned off, the light goes out.
/// </summary>
public class FlashlightGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const int X = 46, Y = 122; // the arrivals corridor, open floor east and west

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    [MetaServerFact]
    public void A_flashlight_in_a_hand_lights_the_corridor_ahead_of_the_player()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 17, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        int z = In(w, () => (int)me.z);
        In(w, () => Waiting.NoSleep(me.forceMove(locate(X, Y, z))));
        w.Run(Second);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);

        w.Host.EnqueueConsoleLine("give Assistant /obj/item/flashlight");
        w.Run(5);
        var flashlight = In(w, () => me.get_active_held_item() as obj_item_flashlight);
        Assert.NotNull(flashlight);
        Assert.Equal(0f, In(w, () => flashlight.light_on));
        Assert.False(Own().On, "an off flashlight lights nothing");

        p.Session.Keybinding(KeybindingCode.ActivateInhand); // Z: attack_self, toggle_light
        w.Run(5);
        Assert.Equal(1f, In(w, () => flashlight.light_on));
        Assert.Equal("flashlight-on", In(w, () => flashlight.icon_state)); // update_icon_state, flashlight.dm:83
        Assert.Contains(heard, h => h.Kind == SoundKind.Play && h.File != null && (h.File ?? "").StartsWith("sound/items/weapons/magin", StringComparison.Ordinal));

        foreach ((double yaw, int dx) in new[] { (-Math.PI / 2, 1), (Math.PI / 2, -1) }) // east, then west
        {
            p.Yaw = yaw;
            p.Keys = MoveKeys.Forward;
            w.Run(3);
            p.Keys = MoveKeys.None;
            w.Run(Second);
            LightState light = Own();
            output.WriteLine($"facing {(dx > 0 ? "east" : "west")}: {light}");
            Assert.True(light.On, "the player carries the flashlight's light");
            Assert.Equal((byte)3, light.System); // OVERLAY_LIGHT_DIRECTIONAL
            Assert.Equal(4f, light.Range);
            Assert.Equal(-2f * dx, light.OffsetX, 0.01f); // cast_range 2 ahead (an offset of +1 lights one tile west)
            Assert.Equal(0f, light.OffsetY, 0.01f);

            // The client's grid: the corridor two tiles ahead is lit, two tiles behind much less.
            ReplicaEntity mine = replica.Entities.Single(e => e.Id == p.Session.Replica.OwnEntity);
            var grid = new LightGrid(new ReplicaLightCells(replica, replica.Map.Width, replica.Map.Height), z);
            grid.SetSource(mine.Id, mine.State.X, mine.State.Y, mine.Light);
            grid.Flush(out _, out _, out _, out _);
            float ahead = Lum(grid, mine.State.X + 2 * dx, mine.State.Y);
            float behind = Lum(grid, mine.State.X - 2 * dx, mine.State.Y);
            output.WriteLine($"  the grid two tiles ahead {ahead:0.000}, behind {behind:0.000}");
            Assert.True(ahead > 0.3f, $"ahead {ahead}");
            Assert.True(ahead > behind * 1.5f, $"ahead {ahead}, behind {behind}");
        }

        // Dropped, the flashlight lights from the floor and the player carries nothing.
        p.Session.Keybinding(KeybindingCode.DropItem);
        w.Run(Second / 2);
        Assert.True(In(w, () => flashlight.loc is turf));
        Assert.False(Own().On, "a dropped flashlight leaves its holder");
        ReplicaEntity onFloor = replica.Entities.Single(e => e.Id == flashlight.dm_id);
        output.WriteLine($"on the floor: {onFloor.Light}");
        Assert.True(onFloor.Light.On);
        Assert.Equal((byte)3, onFloor.Light.System);

        // Picked up and turned off: the light goes out.
        In(w, () => Waiting.NoSleep(me.put_in_active_hand(flashlight)));
        w.Run(5);
        Assert.True(Own().On);
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(5);
        Assert.Equal(0f, In(w, () => flashlight.light_on));
        Assert.Equal("flashlight", In(w, () => flashlight.icon_state));
        Assert.False(Own().On, "turned off");
        Assert.Contains(heard, h => h.Kind == SoundKind.Play && h.File != null && (h.File ?? "").StartsWith("sound/items/weapons/magout", StringComparison.Ordinal));

        LightState Own() => replica.TryGet(replica.OwnEntity, out ReplicaEntity e) ? e.Light : LightState.None;
    }

    /// <summary>
    /// DV62: another player's flashlight is a cone too. Bob's replica has Alice's light as a flashlight beam and her yaw, so
    /// his client points the spot at her hand the way she looks; dropped, the flashlight on the floor points the way the
    /// server cast its light (the way she faced).
    /// </summary>
    [MetaServerFact]
    public void Another_players_flashlight_reaches_the_other_client_as_a_beam_the_way_she_looks()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 18, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var her = (mob_living_carbon_human)w.MobOf(alice);
        var him = w.MobOf(bob);
        int z = In(w, () => (int)her.z);
        In(w, () => Waiting.NoSleep(her.forceMove(locate(X, Y, z))));
        In(w, () => Waiting.NoSleep(him.forceMove(locate(X, Y + 1, z))));
        w.Run(Second);
        w.Host.EnqueueConsoleLine("give Alice /obj/item/flashlight");
        w.Run(5);
        var flashlight = In(w, () => her.get_active_held_item() as obj_item_flashlight);
        Assert.NotNull(flashlight);
        alice.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(5);
        ReplicaWorld seen = bob.Session.Replica;

        foreach ((double yaw, float expected) in new[] { (-Math.PI / 2, 64f), (Math.PI / 2, 192f) }) // east, then west
        {
            alice.Yaw = yaw;
            alice.Keys = MoveKeys.Forward;
            w.Run(3);
            alice.Keys = MoveKeys.None;
            w.Run(Second);
            ReplicaEntity she = seen.Entities.Single(e => e.Id == her.dm_id);
            output.WriteLine($"Bob sees Alice's light {she.Light}, yaw {she.State.Yaw}");
            Assert.True(FlashlightBeam.IsBeam(she.Light), "Bob's replica has Alice's flashlight beam");
            Assert.True(she.HasPos);
            float heading = FlashlightBeam.YawByte(she.HasPos, she.State.Yaw, she.Light, she.State.Dir);
            Assert.True(Math.Abs(Delta(heading, expected)) < 8f, $"heading {heading}, expected {expected}");
            // Without her yaw, the light's cast points the same way.
            float cast = FlashlightBeam.YawByte(false, 0f, she.Light, she.State.Dir);
            Assert.True(Math.Abs(Delta(cast, expected)) < 1f, $"cast {cast}, expected {expected}");
        }

        alice.Session.Keybinding(KeybindingCode.DropItem);
        w.Run(Second / 2);
        ReplicaEntity onFloor = seen.Entities.Single(e => e.Id == flashlight.dm_id);
        Assert.True(FlashlightBeam.IsBeam(onFloor.Light));
        float floorHeading = FlashlightBeam.YawByte(onFloor.HasPos, onFloor.State.Yaw, onFloor.Light, onFloor.State.Dir);
        output.WriteLine($"on the floor: {onFloor.Light}, heading {floorHeading}");
        Assert.True(Math.Abs(Delta(floorHeading, 192f)) < 1f, $"the dropped flashlight points west, heading {floorHeading}");
        (float ox, float oy, float h) = FlashlightBeam.Origin(onFloor.State.X, onFloor.State.Y, floorHeading, carried: false, lying: false);
        Assert.Equal(FlashlightBeam.FloorHeight, h);
    }

    /// <summary>
    /// DV62: a beam through gas. Plasma let out two tiles ahead of a lit flashlight reaches the client as a visible gas, every
    /// visible gas the server names has its own optics, and the beam the client draws from its replica is purple: the green
    /// is stopped most.
    /// </summary>
    [MetaServerFact]
    public void A_flashlight_beam_through_plasma_is_tinted_on_the_client()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 19, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        int z = In(w, () => (int)me.z);
        In(w, () => Waiting.NoSleep(me.forceMove(locate(X, Y, z))));
        w.Run(Second);
        ReplicaWorld replica = p.Session.Replica;
        foreach (string kind in replica.GasKinds)
            Assert.True(GasOptics.Known(kind), $"the visible gas {kind} has no optics of its own");
        Assert.Contains("plasma", replica.GasKinds);

        w.Host.EnqueueConsoleLine($"air add plasma 200 {X + 2} {Y} {z}");
        w.Run(Second);
        GasLevel[] gas = replica.GasAt(X + 2, Y, z);
        output.WriteLine($"the client sees at ({X + 2}, {Y}): {string.Join(", ", gas.Select(g => replica.GasKinds[g.Kind - 1] + " " + g.Level))}");
        Assert.Contains(gas, g => replica.GasKinds[g.Kind - 1] == "plasma");

        // The client's optics from its replica (a purple sprite colour, as the gas layer measures tg's plasma sprite).
        GasLook Look(int k) => new(replica.GasKinds[k - 1], 0.75f, 0.2f, 0.85f, 0.6f);
        CellOptics At(int x, int y) => GasOptics.Cell(replica.GasAt(x, y, z), Look, 2f);
        Assert.True(At(X + 2, Y).Any);
        (float r, float g, float b) = GasOptics.Transmittance(At, X, Y, 1f, 0f, 7f, 2f);
        output.WriteLine($"the beam east lets through r {r:0.000} g {g:0.000} b {b:0.000}");
        Assert.True(g < r && g < b, "plasma stops the green most");
        (float wr, float wg, float wb) = GasOptics.Transmittance(At, X, Y, -1f, 0f, 7f, 2f);
        Assert.True(wg > g, $"the beam west, away from the plasma, lets more through ({wg} against {g})");
    }

    private static float Delta(float a, float b) => ((a - b) % 256f + 384f) % 256f - 128f;

    // The grid's light at a tile: the mean of its four corners' largest channel.
    private static float Lum(LightGrid grid, int x, int y)
    {
        float sum = 0f;
        for (int i = x - 1; i <= x; i++)
            for (int j = y - 1; j <= y; j++)
            {
                (float r, float g, float b) = grid.LightAt(i, j, out _);
                sum += Math.Max(r, Math.Max(g, b));
            }
        return sum / 4f;
    }
}
