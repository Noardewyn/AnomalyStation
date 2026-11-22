using System.Reflection;
using System.Text;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Replication;

namespace Anomaly.Server.Tests.Replication;

/// <summary>D40: the wire carries exactly the whitelisted fields (<see cref="NetField"/>); <c>real_name</c> and every other tg var never leave the server.</summary>
public class WhitelistTests
{
    [Fact]
    public void TheReplicatorsFieldTableIsExactlyNetField()
    {
        NetField seen = NetField.None;
        foreach (FieldSource source in ReplicatedFields.All)
        {
            int bits = (int)source.Field;
            Assert.True(bits != 0 && (bits & (bits - 1)) == 0, $"{source.Field} is one field");
            Assert.True((seen & source.Field) == 0, $"{source.Field} is listed once");
            Assert.False(string.IsNullOrWhiteSpace(source.Source));
            seen |= source.Field;
        }
        Assert.Equal(NetField.All, seen);
        Assert.Equal(NetField.All, ReplicatedFields.Union);
        Assert.Equal(NetField.None, ReplicatedFields.SweepOnly); // every changeable field has a hint (name since M2) or is polled
    }

    [Fact]
    public void TheEntityRecordsHoldTheWhitelistAndNothingElse()
    {
        // An entity record is every field but OwnMob (a header value); a change record adds its mask. Pos is PosX, PosY and
        // HasPos (the record's flag) in the state, PosX and PosY in a change; Flick is FlickAppearance and FlickTick in both; VisHost is IsVisual and VisHost in the state (version 10).
        string[] expected = Enum.GetValues<NetField>()
            .Where(f => f != NetField.None && f != NetField.OwnMob && f != NetField.Changeable && f != NetField.All && (f & (f - 1)) == 0)
            .Select(f => f.ToString()).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        string[] state = typeof(EntityState).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name).Select(n => n is "PosX" or "PosY" or "HasPos" ? "Pos" : n is "FlickAppearance" or "FlickTick" ? "Flick" : n is "IsVisual" ? "VisHost" : n).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToArray();
        // GlideSize / LastMoveTick are spelled the same in the records and the enum.
        Assert.Equal(expected, state);
        string[] change = typeof(EntityChange).GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Select(f => f.Name).Where(n => n != nameof(EntityChange.Mask)).Select(n => n is "PosX" or "PosY" ? "Pos" : n is "FlickAppearance" or "FlickTick" ? "Flick" : n).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(expected.Where(n => n != nameof(NetField.Type)), change);
    }

    [Fact]
    public void RealNameKeyDescAndOtherVarsNeverReachTheWire()
    {
        var world = ReplicationWorld.Boot(size: 16, movers: 12, seed: 31);
        const string Secret = "SECRET-";
        using (world.World.Enter())
        {
            int n = 0;
            world.World.Objects.ForEachLiveInIdOrder((id, obj) =>
            {
                if (obj is not atom a) return;
                a.desc = Secret + "desc-" + n++;
                // filters and render_target reach the wire since version 25 (D155): MessagesV25.cs; maptext since version 74 (gaps W1: BYOND
                // shows an atom's maptext to everyone who sees it)
                if (a is mob m)
                {
                    m.real_name = Secret + "real-" + n;
                    m.key = Secret + "key-" + n;
                    m.ckey = Secret + "ckey-" + n;
                }
            });
        }
        var bytes = new List<byte[]>();
        var replicator = new Replicator();
        replicator.Attach(world.World);
        JoinMessages join = replicator.BuildJoin(world.World.Tick, world.Movers[0].dm_id, 0);
        bytes.Add(join.MapGrid);
        bytes.Add(join.Snapshot);
        var names = new HashSet<string>();
        foreach (EntityState e in Wire.DecodeSnapshot(join.Snapshot).Entities) names.Add(e.Name);
        for (int i = 0; i < 400; i++)
        {
            world.Step();
            DeltaFrame frame = replicator.BuildDelta(world.World.Tick);
            bytes.Add(frame.Body.ToArray());
            Delta delta = Wire.DecodeDelta(frame);
            foreach (EntityState e in delta.Spawned) names.Add(e.Name);
            foreach (EntityChange c in delta.Changed)
            {
                Assert.Equal(NetField.None, c.Mask & ~NetField.Changeable);
                if ((c.Mask & NetField.Name) != 0) names.Add(c.Name);
            }
        }
        byte[] marker = Encoding.UTF8.GetBytes(Secret);
        foreach (byte[] message in bytes) Assert.True(message.AsSpan().IndexOf(marker) < 0, "a hidden var reached the wire");
        Assert.All(names, name => Assert.DoesNotContain(Secret, name ?? string.Empty));
        Assert.Contains("mover 0", names);
        Assert.Empty(world.Runtimes);
    }

    [Fact]
    public void AbstractInvisibleMovablesAreNotEntities()
    {
        var world = ReplicationWorld.Boot(size: 12, movers: 2, seed: 32, mix: ScenarioMix.MoversOnly);
        var run = new ReplicationRun(world, sweepDivisor: 64, minSweep: 0);
        mob hidden = world.Movers[0];
        Assert.True(run.Client.TryGet(hidden.dm_id, out _));
        using (world.World.Enter()) hidden.invisibility = Defines.INVISIBILITY_ABSTRACT;
        run.Step();
        Assert.False(run.Client.TryGet(hidden.dm_id, out _));
        using (world.World.Enter()) hidden.invisibility = 0;
        run.Step();
        Assert.True(run.Client.TryGet(hidden.dm_id, out _));
    }
}
