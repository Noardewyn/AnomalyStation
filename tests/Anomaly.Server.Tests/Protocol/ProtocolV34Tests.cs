using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 34 (M7x slice A, D177, docs/ai/specs/protocol.md section 43): a click's and the mouse actions' aim, the MouseDown,
/// MouseDrag and MouseUp actions, the entity's Shot field and the CameraShake kind. Bytes, round trips, the Limits' refusals.
/// </summary>
public class ProtocolV34Tests
{
    internal static ShotAim RandomAim(Random rng) => new(rng.Next(0, Limits.MaxAimCoordinate + 1), rng.Next(0, Limits.MaxAimCoordinate + 1),
        (ushort)rng.Next(0, 65536), (ushort)rng.Next(0, 65536), (short)rng.Next(-ShotAim.MaxPitch, ShotAim.MaxPitch + 1));

    internal static ActionMessage RandomMouse(Random rng)
    {
        var kind = (TargetKind)rng.Next(0, 4);
        return new ActionMessage((uint)rng.Next(0, 100000), (ActionKind)rng.Next((int)ActionKind.MouseDown, (int)ActionKind.MouseUp + 1),
            kind is TargetKind.Entity or TargetKind.Screen ? rng.Next(1, 1_000_000) : 0, (ClickButton)rng.Next(1, 4), (ClickModifiers)rng.Next(0, 8), null)
        {
            TargetKind = kind,
            TurfX = kind == TargetKind.Turf ? rng.Next(1, 256) : 0,
            TurfY = kind == TargetKind.Turf ? rng.Next(1, 256) : 0,
            TurfZ = kind == TargetKind.Turf ? rng.Next(1, Limits.MaxMapLevels + 1) : 0,
            IconX = (byte)rng.Next(0, Limits.MaxIconPixel + 1),
            IconY = (byte)rng.Next(0, Limits.MaxIconPixel + 1),
            Aim = rng.Next(4) == 0 ? null : RandomAim(rng),
        };
    }

    internal static ShotState RandomShot(Random rng) => new(rng.Next(0, Limits.MaxShotCoordinate + 1), rng.Next(0, Limits.MaxShotCoordinate + 1),
        (short)rng.Next(short.MinValue, short.MaxValue + 1), (short)rng.Next(short.MinValue, short.MaxValue + 1), (ushort)rng.Next(0, 65536));

    [Fact]
    public void TheVersionIs34AndTheMouseKindsFollowTopic()
    {
        Assert.True(ProtocolVersion.Current >= 34); // version 35 (BuildInfoTests pins the current one)
        Assert.Equal(15, (int)ActionKind.MouseDown);
        Assert.Equal(16, (int)ActionKind.MouseDrag);
        Assert.Equal(17, (int)ActionKind.MouseUp);
        Assert.Equal(ActionKind.MouseUp + 1, ActionKind.Say); // version 38: Say follows them (ProtocolV38Tests)
    }

    [Fact]
    public void AnAimedClickHasItsBytesAndRoundTrips()
    {
        var click = new ActionMessage(3, ActionKind.Click, 9, ClickButton.Left, ClickModifiers.None, null)
        {
            TargetKind = TargetKind.Entity,
            IconX = 16,
            IconY = 27,
            AimZone = AimZone.Head,
            Aim = new ShotAim(256, 512, 1300, 16384, -2),
        };
        // id 10, seq 3, kind 4, entity 9, left, modifiers: head (1) << 4 | aim 0x80, icon 16 27, east 256 (zigzag 512: 0x80 0x04),
        // north 512 (zigzag 1024: 0x80 0x08), up 1300 (0x14 0x05 little end), yaw 16384 (0x00 0x40), pitch -2 (0xFE 0xFF), version 88's seen 0
        byte[] bytes = Codec.Encode(click);
        Assert.Equal(new byte[] { 10, 3, 4, 1, 9, 1, 0x90, 16, 27, 0x80, 0x04, 0x80, 0x08, 0x14, 0x05, 0x00, 0x40, 0xFE, 0xFF, 0x00 }, bytes);
        Assert.True(Codec.TryDecode(bytes, out ActionMessage back, out ProtocolError error), error.ToString());
        Assert.Equal(click.Aim, back.Aim);
        Assert.Equal(AimZone.Head, back.AimZone);
        Assert.Equal(bytes, Codec.Encode(back));

        // a click without an aim keeps version 4's layout (bit 7 clear)
        byte[] plain = Codec.Encode(click with { Aim = null });
        Assert.Equal(9, plain.Length);
        Assert.True(Codec.TryDecode(plain, out ActionMessage plainBack, out _));
        Assert.Null(plainBack.Aim);
    }

    [Fact]
    public void MouseActionsRoundTripWithAndWithoutATarget()
    {
        var up = new ActionMessage(8, ActionKind.MouseUp, 0, ClickButton.Left, ClickModifiers.None, null);
        Assert.Equal(new byte[] { 10, 8, 17, 0, 1, 0, 0, 0 }, Codec.Encode(up));
        var rng = new Random(34);
        for (int i = 0; i < 200; i++)
        {
            ActionMessage message = RandomMouse(rng);
            byte[] bytes = Codec.Encode(message);
            Assert.True(bytes.Length <= Limits.MaxActionMessageBytes);
            Assert.True(Codec.TryDecode(bytes, out ActionMessage back, out ProtocolError error), error.ToString());
            Assert.Equal(message.Kind, back.Kind);
            Assert.Equal(message.EffectiveTargetKind, back.EffectiveTargetKind);
            Assert.Equal(message.Aim, back.Aim);
            Assert.Equal(bytes, Codec.Encode(back));
        }
    }

    [Fact]
    public void AimsOutOfRangeAndAimsOnOtherKindsAreRefused()
    {
        var click = new ActionMessage(1, ActionKind.Click, 9, ClickButton.Left, ClickModifiers.None, null) { TargetKind = TargetKind.Entity };
        Assert.Throws<ProtocolException>(() => Codec.Encode(click with { Aim = new ShotAim(0, 0, 0, 0, ShotAim.MaxPitch + 1) }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(click with { Aim = new ShotAim(Limits.MaxAimCoordinate + 1, 0, 0, 0, 0) }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(click with { Aim = new ShotAim(-1, 0, 0, 0, 0) }));
        var hover = new ActionMessage(1, ActionKind.Hover, 9, ClickButton.None, ClickModifiers.None, null) { TargetKind = TargetKind.Entity };
        Assert.Throws<ProtocolException>(() => Codec.Encode(hover with { Aim = new ShotAim(0, 0, 0, 0, 0) }));
        var mouse = new ActionMessage(1, ActionKind.MouseDown, 0, ClickButton.Left, ClickModifiers.None, null);
        Assert.Throws<ProtocolException>(() => Codec.Encode(mouse with { AimZone = AimZone.Head }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(mouse with { Button = ClickButton.None }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(mouse with { Target = 5 })); // over nothing with an id

        // a pitch past a quarter turn on the wire (0x01 0x40 = 16385), and an unknown modifier bit (bit 3) on a mouse action
        Assert.False(Codec.TryDecode(new byte[] { 10, 1, 15, 0, 1, 0x80, 0, 0, 0, 0, 0, 0, 0, 0, 0x01, 0x40 }, out ActionMessage _, out ProtocolError pitch));
        Assert.Equal(ProtocolError.BadValue, pitch);
        Assert.False(Codec.TryDecode(new byte[] { 10, 1, 15, 0, 1, 0x08, 0, 0 }, out ActionMessage _, out _));
        Assert.False(Codec.TryDecode(new byte[] { 10, 1, 18, 0, 1, 0, 0, 0 }, out ActionMessage _, out _)); // kind 18 is unknown
        Assert.False(Codec.TryDecode(new byte[] { 10, 1, 15, 0, 1, 0x80, 0, 0, 0, 0 }, out ActionMessage _, out ProtocolError truncated)); // an aim cut short
        Assert.Equal(ProtocolError.Truncated, truncated);
    }

    [Fact]
    public void TheShotFieldRoundTripsInRecordsAndChangesAndTheReplicaKeepsIt()
    {
        var shot = new ShotState(1000, 2000, 333, -410, 16384);
        EntityState bolt = Msg.State(7, 0, 31, 62, 1, 0, null, 0f, 10) with { Shot = shot };
        EntityState plain = Msg.State(9, 0, 4, 4, 1, 0, null, 0f, 10);
        Snapshot s = Msg.Snapshot(100, 9, 0, new[] { "/obj/projectile" }, bolt, plain);
        Assert.True(Codec.TryDecode(Codec.Encode(s), out Snapshot snapshot, out ProtocolError error), error.ToString());
        Assert.Equal(shot, snapshot.Entities[0].Shot);
        Assert.Null(snapshot.Entities[1].Shot);
        Assert.Equal(Codec.Encode(s), Codec.Encode(snapshot));

        var rng = new Random(340);
        for (int i = 0; i < 100; i++)
        {
            ShotState random = RandomShot(rng);
            Delta d = Msg.Delta(200, changed: new[] { new EntityChange { Id = 7, Mask = NetField.Shot | NetField.X, X = 4, Shot = random } });
            Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta decoded, out error), error.ToString());
            Assert.Equal(random, decoded.Changed[0].Shot);
        }
        Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Snapshot(100, 9, 0, new[] { "/obj" }, bolt with { Shot = shot with { OriginX = Limits.MaxShotCoordinate + 1 } })));

        var replica = new ReplicaWorld();
        var map = new MapGrid { Tick = 5, Width = 64, Height = 64, Levels = 1 };
        map.TurfTypes.Add("/turf/open/floor");
        map.Runs.Add(new TurfRun(map.CellCount, 0));
        replica.Apply(map);
        replica.Apply(Msg.Snapshot(100, 9, 0, new[] { "/obj/projectile" }, bolt, plain));
        Assert.Equal(shot, replica.Entities.Single(e => e.Id == 7).Shot);
        var turned = shot with { Angle = 0 };
        replica.Apply(Msg.Delta(101, changed: new[] { new EntityChange { Id = 7, Mask = NetField.Shot, Shot = turned } }));
        Assert.Equal(turned, replica.Entities.Single(e => e.Id == 7).Shot);
    }

    [Fact]
    public void AShotsHeightRisesWithItsSlopeAndItsPointProjectsOntoItsLine()
    {
        // from (320, 320) at 20.8 px heading east (90 degrees), rising 1/8 px a pixel
        var shot = new ShotState(320, 320, (short)Math.Round(20.8 * 16), 512, 16384);
        Assert.Equal(20.8, shot.HeightAt(320, 320), 1);
        Assert.Equal(24.8, shot.HeightAt(352, 320), 1);
        (double x, double y) = shot.Project(352, 330);
        Assert.Equal(352, x, 3);
        Assert.Equal(320, y, 3);
    }

    [Fact]
    public void ARecoilIsAShakeOfItsOwnKind()
    {
        var recoil = new CameraShakeMessage { Kind = CameraShakeKind.Recoil };
        recoil.Steps.Add(new CameraShakeStep(0, -32, 2));
        recoil.Steps.Add(new CameraShakeStep(0, 0, 3));
        byte[] bytes = Codec.Encode(recoil);
        Assert.Equal(new byte[] { 18, 1, 2, 0, 63, 2, 0, 0, 3 }, bytes);
        Assert.True(Codec.TryDecode(bytes, out CameraShakeMessage back, out ProtocolError error), error.ToString());
        Assert.Equal(CameraShakeKind.Recoil, back.Kind);
        Assert.Equal(recoil.Steps, back.Steps);
        Assert.False(Codec.TryDecode(new byte[] { 18, 2, 1, 0, 0, 1 }, out CameraShakeMessage _, out _)); // kind 2 is unknown
        Assert.Throws<ProtocolException>(() => Codec.Encode(new CameraShakeMessage { Kind = (CameraShakeKind)2, Steps = { new CameraShakeStep(0, 0, 1) } }));
    }
}
