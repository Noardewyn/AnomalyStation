using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tests.Tg.Wave4.Mob;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tests.M4aContracts;

/// <summary>The M4a contracts step's screentip seam (Tg/Infrastructure/ScreentipSeam.cs, docs/ai/specs/interaction.md section 6).</summary>
public class ScreentipSeamContractTests
{
    private sealed class RecordingSink : IScreentipSink
    {
        public readonly List<(client Viewer, atom Target, string Name, IReadOnlyList<ScreentipHint> Hints)> Calls = new();

        public void Screentip(client viewer, atom target, string name, IReadOnlyList<ScreentipHint> hints) => Calls.Add((viewer, target, name, hints));
    }

    [Fact]
    public void EveryTgContextKeyNamesOneClick()
    {
        Assert.Equal(8, ScreentipSeam.ContextKeys.Count);
        var seen = new HashSet<(string, bool, bool, bool)>();
        foreach (string key in ScreentipSeam.ContextKeys)
        {
            Assert.True(ScreentipSeam.TryKey(key, out string button, out bool shift, out bool ctrl, out bool alt), key);
            Assert.True(button == LEFT_CLICK || button == RIGHT_CLICK);
            Assert.True(seen.Add((button, shift, ctrl, alt)), key);
        }
        Assert.True(ScreentipSeam.TryKey(SCREENTIP_CONTEXT_CTRL_SHIFT_LMB, out string b, out bool s, out bool c, out bool a));
        Assert.Equal((LEFT_CLICK, true, true, false), (b, s, c, a));
        Assert.True(ScreentipSeam.TryKey(SCREENTIP_CONTEXT_ALT_RMB, out b, out s, out c, out a));
        Assert.Equal((RIGHT_CLICK, false, false, true), (b, s, c, a));
        Assert.False(ScreentipSeam.TryKey("Middle", out _, out _, out _, out _));
        Assert.False(ScreentipSeam.TryKey(null, out _, out _, out _, out _));
    }

    [Fact]
    public void PublishGivesTheSinkTgsEntriesInTgsLineOrder()
    {
        var room = new MobRoom();
        var sink = new RecordingSink();
        room.In(() =>
        {
            ScreentipSeam.Install(World.Current, sink);
            var target = Dm.@new<obj>(MobRoom.T(3, 3));
            var viewer = Dm.@new<client_test_m1_client>();
            // tg's context list is filled in any order (add_context, the item signal, the elements); the lines are tg's.
            DmList context = Dm.list();
            context[SCREENTIP_CONTEXT_SHIFT_LMB] = "Examine";
            context[SCREENTIP_CONTEXT_RMB] = "Weld shut";
            context[SCREENTIP_CONTEXT_LMB] = "Open";
            context["Middle"] = "not a tg key";
            context[SCREENTIP_CONTEXT_ALT_LMB] = 5; // not text: left out, as build_context would print nothing useful
            ScreentipSeam.Publish(viewer, target, "airlock", context);
            ScreentipSeam.Publish(viewer, target, "airlock", null);
            ScreentipSeam.Publish(null, target, "x", context);
            ScreentipSeam.Install(World.Current, null);
            ScreentipSeam.Publish(viewer, target, "no sink", context);
        });
        Assert.Equal(2, sink.Calls.Count);
        var first = sink.Calls[0];
        Assert.Equal("airlock", first.Name);
        Assert.Equal(new[]
        {
            new ScreentipHint(LEFT_CLICK, false, false, false, "Open"),
            new ScreentipHint(RIGHT_CLICK, false, false, false, "Weld shut"),
            new ScreentipHint(LEFT_CLICK, true, false, false, "Examine"),
        }, first.Hints);
        Assert.Empty(sink.Calls[1].Hints);
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AWorldHasNoSinkUntilTheHostInstallsOne()
    {
        var room = new MobRoom();
        room.In(() => Assert.Null(ScreentipSeam.SinkOf(World.Current)));
    }
}
