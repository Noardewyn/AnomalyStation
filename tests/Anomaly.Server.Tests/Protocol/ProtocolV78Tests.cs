using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 78 (silicons SI4, D326): the private block's camera static (DV386), popup windows and view areas (DV387) and command
/// verbs, each the server's last word on the replica; the Verb and PopupClose actions; a play's wait (the sound head's top bit).
/// </summary>
public class ProtocolV78Tests
{
    private static OwnBlock Full() => new()
    {
        StaticChunks = new List<StaticChunk> { new(1, 1, 2, 0x8000_0000_0000_0001UL), new(8, 16, 2, 0) },
        Popups = new List<PopupWindow> { new("spyglass", "Spy view", 320, 320) },
        Verbs = new List<VerbEntry> { new("AI Commands", "Show Laws", "/mob/living/silicon/ai/proc/show_laws_verb") },
        ViewAreas = new List<ViewArea> { new(0, "spyglass_map", 3, 4, 9, 10, 2), new(57, null, 1, 1, 5, 5, 1) },
    };

    [Fact]
    public void TheKindsFollowVersion77()
    {
        Assert.True(ProtocolVersion.Current >= 78);
        Assert.Equal(22, (int)ActionKind.Verb);
        Assert.Equal(23, (int)ActionKind.PopupClose);
        Assert.True(ActionKinds.Last >= ActionKind.PopupClose);
    }

    [Fact]
    public void TheOwnBlocksNewListsRoundTripAndAreChecked()
    {
        Delta d = Msg.Delta(9);
        d.Own = Full();
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta back, out ProtocolError error), error.ToString());
        Msg.AssertEqual(d.Own, back.Own);

        // an empty list is a change to none, distinct from no change
        d.Own = new OwnBlock { StaticChunks = new(), Popups = new(), Verbs = new(), ViewAreas = new() };
        Assert.True(Codec.TryDecode(Codec.Encode(d), out back, out _));
        Msg.AssertEqual(d.Own, back.Own);
        d.Own = new OwnBlock();
        Assert.True(Codec.TryDecode(Codec.Encode(d), out back, out _));
        Assert.Null(back.Own.StaticChunks);
        Assert.Null(back.Own.Verbs);

        foreach (OwnBlock bad in new[]
        {
            new OwnBlock { StaticChunks = new() { new(0, 1, 1, 1) } },
            new OwnBlock { Popups = new() { new("", "t", 10, 10) } },
            new OwnBlock { Popups = new() { new("p", "t", 0, 10) } },
            new OwnBlock { Verbs = new() { new("AI Commands", "Show Laws", "") } },
            new OwnBlock { ViewAreas = new() { new(5, "m", 1, 1, 2, 2, 1) } }, // a screen object and a map
            new OwnBlock { ViewAreas = new() { new(0, null, 1, 1, 2, 2, 1) } }, // neither
            new OwnBlock { ViewAreas = new() { new(5, null, 3, 1, 2, 2, 1) } }, // an empty rectangle
        })
            Assert.Throws<ProtocolException>(() => Codec.Encode(new Delta { Tick = 4, Own = bad }));
    }

    [Fact]
    public void TheReplicaKeepsTheServersLastWordAndReadsATilesStatic()
    {
        var world = new ReplicaWorld();
        world.Apply(Msg.FlatMap(24, 24));
        world.Apply(Msg.Snapshot(10, 1, 0, new[] { "/mob" }, Msg.State(1)));
        Assert.Empty(world.StaticChunks);
        Assert.False(world.IsObscured(9, 17, 1));

        // the chunk from (8, 16): tile (9, 17) is bit 1 * 8 + 1
        Delta d = Msg.Delta(11, own: 1);
        d.Own.StaticChunks = new() { new StaticChunk(8, 16, 1, 1UL << 9) };
        d.Own.Verbs = Full().Verbs;
        int version = world.StaticVersion;
        world.Apply(d);
        Assert.True(world.IsObscured(9, 17, 1));
        Assert.False(world.IsObscured(10, 17, 1));
        Assert.False(world.IsObscured(9, 17, 2));
        Assert.NotEqual(version, world.StaticVersion);
        Assert.Equal("Show Laws", Assert.Single(world.Verbs).Name);

        // no change keeps them; an empty list clears them
        version = world.StaticVersion;
        world.Apply(Msg.Delta(12, own: 1));
        Assert.True(world.IsObscured(9, 17, 1));
        Assert.Equal(version, world.StaticVersion);
        d = Msg.Delta(13, own: 1);
        d.Own.StaticChunks = new();
        world.Apply(d);
        Assert.False(world.IsObscured(9, 17, 1));

        // tg's chunks start at 1, 8, 16 (GET_CHUNK_COORD): one from 9 is refused
        Assert.Equal(1, ReplicaWorld.ChunkCoord(7));
        Assert.Equal(8, ReplicaWorld.ChunkCoord(8));
        Assert.Equal(16, ReplicaWorld.ChunkCoord(23));
        d = Msg.Delta(14, own: 1);
        d.Own.StaticChunks = new() { new StaticChunk(9, 1, 1, 1) };
        Assert.ThrowsAny<Exception>(() => world.Apply(d));
    }

    [Fact]
    public void AVerbAndAPopupCloseCarryTheirTextAndNeedIt()
    {
        foreach (ActionKind kind in new[] { ActionKind.Verb, ActionKind.PopupClose })
        {
            string text = kind == ActionKind.Verb ? "/mob/living/silicon/ai/verb/toggle_anchor" : "spyglass";
            var action = new ActionMessage(7, kind, 0, ClickButton.None, ClickModifiers.None, text);
            Assert.True(Codec.TryDecode(Codec.Encode(action), out ActionMessage back, out ProtocolError error), error.ToString());
            Assert.Equal(kind, back.Kind);
            Assert.Equal(text, back.Text);
            Assert.Throws<ProtocolException>(() => Codec.Encode(action with { Text = null }));
        }
    }

    [Fact]
    public void APlaysWaitRoundTripsAndOnlyAPlayWaits()
    {
        var play = new SoundEvent { Kind = SoundKind.Play, Channel = 1022, Path = 1, Volume = 28, Wait = true };
        var m = new Sounds { FirstPath = 1 };
        m.NewPaths.Add("sound/announcer/vox_fem/alert.ogg");
        m.Events.Add(play);
        Assert.True(Codec.TryDecode(Codec.Encode(m), out Sounds back, out ProtocolError error), error.ToString());
        Assert.True(back.Events[0].Wait);
        m.Events[0] = new SoundEvent { Kind = SoundKind.Stop, Channel = 1022, Wait = true };
        Assert.Throws<ProtocolException>(() => Codec.Encode(m));
    }
}
