using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Replication;
using Anomaly.Server.Tests.Server;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.M4b;

/// <summary>
/// M4b slice 3, the server's half of the gas visuals (protocol version 5): the replicator sends the gas overlays tg's
/// update_visuals puts on a turf as the cell's gas levels (a Delta's change, a late joiner's MapGrid, the cell cleared), and
/// the host answers a dev gas probe with the air around it when DEV_COMMANDS is on and says once that it is off otherwise.
/// </summary>
public class GasVisualsServerTests
{
    private static void SetAir(V2Run run, int x, int y, string mix) => run.In(() =>
    {
        var t = (turf_open)locate(x, y, 1);
        t.air = SSair.parse_gas_string(mix) as datum_gas_mixture;
        t.update_visuals();
    });

    [Fact]
    public void TheGasOfATurfReachesTheClientsAsItsCellsGasLevels()
    {
        V2Run run = V2Run.Quiet(size: 14, cellSweep: 100000);
        run.Step();
        SessionView early = run.Connect("Early", run.In(() => Dm.@new<mob>(run.Turf(2, 2))));
        run.Step();
        Assert.Contains("plasma", early.Replica.GasKinds);
        byte plasma = (byte)(early.Replica.GasKinds.ToList().IndexOf("plasma") + 1);
        Assert.Empty(early.Replica.GasAt(6, 6, 1));

        // 5 moles of plasma: tg's visible state ceil(5 / 0.25) = 20
        SetAir(run, 6, 6, "o2=22;n2=82;plasma=5;TEMP=293.15");
        run.Step();
        Delta delta = Wire.DecodeDelta(run.LastFrame);
        CellGasChange change = Assert.Single(delta.CellGasChanged, c => c is { X: 6, Y: 6 });
        Assert.Equal(new[] { new GasLevel(plasma, 20) }, change.Gases);
        Assert.Equal(new[] { new GasLevel(plasma, 20) }, early.Replica.GasAt(6, 6, 1));

        // a late joiner has it in its MapGrid
        SessionView late = run.Connect("Late", run.In(() => Dm.@new<mob>(run.Turf(3, 3))));
        run.Step();
        Assert.Equal(new[] { new GasLevel(plasma, 20) }, late.Replica.GasAt(6, 6, 1));

        // the plasma gone: the cell is cleared
        SetAir(run, 6, 6, "o2=22;n2=82;TEMP=293.15");
        run.Step();
        Assert.Empty(early.Replica.GasAt(6, 6, 1));
        Assert.Empty(late.Replica.GasAt(6, 6, 1));
    }

    [Fact]
    public void ADevGasProbeIsAnsweredWithTheAirAroundIt()
    {
        using TestServer server = TestServer.Start(TestServer.DefaultConfig(devCommands: true));
        FakeRemote alice = server.Join("Alice");
        alice.Motion(0, 0);
        server.Tick(2);
        using (server.World.Enter())
        {
            var t = (turf_open)locate(5, 5, 1);
            t.air = SSair.parse_gas_string("o2=22;n2=82;plasma=5;TEMP=500") as datum_gas_mixture;
        }
        alice.ActionV4(new ActionMessage(0, ActionKind.DevGasProbe, 0, ClickButton.None, ClickModifiers.None, null) { TargetKind = TargetKind.Turf, TurfX = 4, TurfY = 4, TurfZ = 1 });
        server.Tick(2);
        byte[] sent = Assert.Single(alice.Messages(MessageId.DevGasReport));
        Assert.True(Codec.TryDecode(sent, out DevGasReport report, out ProtocolError error), error.ToString());
        // (4, 4) +- 7, clipped to the map
        Assert.Equal((1, 1, 1, 11, 11), (report.X0, report.Y0, report.Z, report.Width, report.Height));
        DevGasCell cell = report.At(5, 5);
        Assert.Equal(DevCellKind.Open, cell.Kind);
        Assert.Equal(500f, cell.Temperature, 2);
        var moles = cell.Moles.ToDictionary(m => report.Gases[m.Gas].Id, m => m.Moles);
        Assert.Equal(22f, moles["o2"], 3);
        Assert.Equal(82f, moles["n2"], 3);
        Assert.Equal(5f, moles["plasma"], 3);
        Assert.True(cell.Pressure > 101.325f, "hotter than the room: more pressure");
        Assert.Contains(report.Gases, g => g is { Id: "plasma", Name: "Plasma" });
        Assert.Equal(0, server.Host.SessionOf(1).Strikes);
    }

    [Fact]
    public void WithoutDevCommandsAProbeIsRefusedOnce()
    {
        using TestServer server = TestServer.Start(TestServer.DefaultConfig(devCommands: false));
        FakeRemote alice = server.Join("Alice");
        alice.Motion(0, 0);
        server.Tick(2);
        var probe = new ActionMessage(0, ActionKind.DevGasProbe, 0, ClickButton.None, ClickModifiers.None, null) { TargetKind = TargetKind.Turf, TurfX = 4, TurfY = 4, TurfZ = 1 };
        alice.ActionV4(probe);
        server.Tick();
        alice.ActionV4(probe);
        server.Tick(2);
        Assert.Empty(alice.Messages(MessageId.DevGasReport));
        string[] texts = alice.Messages(MessageId.ServerText).Select(m => FakeCodec.DecodeServerText(m).Html).ToArray();
        Assert.Single(texts, t => t.Contains("no gas overlay", StringComparison.Ordinal));
        Assert.Equal(0, server.Host.SessionOf(1).Strikes);
    }
}
