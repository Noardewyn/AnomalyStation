using Anomaly.Client.Shared;
using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>M2 P8: the play session over a feed (actions, tags, pose source, camera layer, statistics).</summary>
public class PlaySessionTests
{
    /// <summary>A feed that passes everything to a <see cref="FakeFeed"/> and keeps what the client sent.</summary>
    private sealed class RecordingFeed : IWorldFeed
    {
        private readonly FakeFeed _inner;
        public readonly List<ActionMessage> Actions = new();
        public readonly List<MotionInputMessage> Motions = new();

        public RecordingFeed(FakeFeed inner) => _inner = inner;

        public FeedPhase Phase => _inner.Phase;
        public string CloseReason => _inner.CloseReason;
        public bool Rejected => _inner.Rejected;
        public string Description => _inner.Description;
        public void Poll(double nowSeconds) => _inner.Poll(nowSeconds);
        public uint LastSentSeq => _inner.LastSentSeq;
        public uint LastAppliedSeq => _inner.LastAppliedSeq;
        public MapGrid Map => _inner.Map;
        public long Tick => _inner.Tick;
        public long DeltaCount => _inner.DeltaCount;
        public long OwnEntity => _inner.OwnEntity;
        public int Count => _inner.Count;
        public IEnumerable<ReplicaEntity> Entities => _inner.Entities;
        public event Action<ReplicaEntity> Spawned { add => _inner.Spawned += value; remove => _inner.Spawned -= value; }
        public event Action<ReplicaEntity> Despawned { add => _inner.Despawned += value; remove => _inner.Despawned -= value; }
        public event Action<ServerText> ServerTextReceived { add => _inner.ServerTextReceived += value; remove => _inner.ServerTextReceived -= value; }
        public int RoundTripMs => _inner.RoundTripMs;
        public long BytesSent => _inner.BytesSent;
        public long BytesReceived => _inner.BytesReceived;
        public void Disconnect() => _inner.Disconnect();
        public ReplicaWorld Replica => _inner.Replica;

        public void SendMotion(MotionInputMessage frames)
        {
            var copy = new MotionInputMessage();
            copy.Frames.AddRange(frames.Frames);
            Motions.Add(copy);
            _inner.SendMotion(frames);
        }

        public void SendAction(in ActionMessage action)
        {
            Actions.Add(action);
            _inner.SendAction(action);
        }
    }

    private static (RecordingFeed Feed, PlayRig Rig) Rig(int bots = 4, int seed = 3)
    {
        var feed = new RecordingFeed(new FakeFeed("Tester", seed: seed, bots: bots));
        return (feed, new PlayRig(feed, 90));
    }

    [Fact]
    public void ActionsAreTaggedWithTheNewestFrameAndCarryTheirFields()
    {
        (RecordingFeed feed, PlayRig rig) = Rig();
        rig.Run(1.0);
        uint tag = rig.Session.Prediction.NewestSeq;
        Assert.True(tag > 10);
        rig.Session.Rest();
        rig.Session.ToggleMoveIntent();
        rig.Session.ToggleCombatMode();
        Assert.True(rig.Session.Click(7, ClickButton.Right, ClickModifiers.Shift | ClickModifiers.Ctrl));
        Assert.True(rig.Session.Click(8, ClickButton.Left, ClickModifiers.None));
        rig.Session.DevCommand("heal");
        rig.Session.DevGasProbe(12, 30, 1);
        Assert.Equal(7, feed.Actions.Count);
        // a walk/run toggle belongs to the next frame, which the predictor steps at the toggled speed (protocol version 49)
        Assert.All(feed.Actions, a => Assert.Equal(a.Kind == ActionKind.ToggleMoveIntent ? tag + 1 : tag, a.FrameSeq));
        Assert.Equal(new[] { ActionKind.Rest, ActionKind.ToggleMoveIntent, ActionKind.ToggleCombatMode, ActionKind.Click, ActionKind.Click, ActionKind.DevCommand, ActionKind.DevGasProbe }, feed.Actions.Select(a => a.Kind));
        Assert.Equal(new ActionMessage(tag, ActionKind.Click, 7, ClickButton.Right, ClickModifiers.Shift | ClickModifiers.Ctrl, null), feed.Actions[3]);
        Assert.Equal("heal", feed.Actions[5].Text);
        Assert.Equal((TargetKind.Turf, 12, 30, 1, 0L), (feed.Actions[6].TargetKind, feed.Actions[6].TurfX, feed.Actions[6].TurfY, feed.Actions[6].TurfZ, feed.Actions[6].Target));
        Assert.True(Codec.TryDecode(Codec.Encode(feed.Actions[6]), out ActionMessage _, out _)); // a valid probe on the wire
        // A click on nothing, or with no button, sends nothing.
        Assert.False(rig.Session.Click(0, ClickButton.Right, ClickModifiers.None));
        Assert.False(rig.Session.Click(7, ClickButton.None, ClickModifiers.None));
        Assert.Equal(7, feed.Actions.Count);
    }

    [Fact]
    public void EveryFrameIsSentOnceWithTheNewestFrameLast()
    {
        (RecordingFeed feed, PlayRig rig) = Rig();
        rig.Keys = MoveKeys.Forward;
        rig.Run(2.0);
        Assert.InRange(feed.Motions.Count, 38, 42);
        for (int i = 0; i < feed.Motions.Count; i++)
            Assert.Equal((uint)(i + 1), feed.Motions[i].Frames[^1].Seq);
        Assert.Equal(feed.Motions[^1].Frames[^1].Seq, feed.LastSentSeq);
    }

    [Fact]
    public void TheOwnPoseIsThePredictedOneAndOthersAreFromTheirSamples()
    {
        (RecordingFeed feed, PlayRig rig) = Rig(bots: 3);
        rig.Keys = MoveKeys.Forward;
        rig.Run(1.5);
        ReplicaEntity own = rig.Own;
        Assert.True(rig.Session.Poses.TryGetPose(own, out RenderPose pose));
        (float x, float y, int z) = rig.Session.Prediction.RenderPosition();
        Assert.Equal(x, pose.X, 4);
        Assert.Equal(y, pose.Y, 4);
        Assert.Equal(z, pose.Z);
        // The predicted capsule is ahead of the replica's copy, which is a round trip behind.
        Assert.True(pose.Y > own.State.PosY / 256f + 0.2f, $"{pose.Y} vs {own.State.PosY / 256f}");
        // Asking twice in a frame gives the same pose (one evaluation per entity per frame).
        Assert.True(rig.Session.Poses.TryGetPose(own, out RenderPose again));
        Assert.Equal(pose, again);

        ReplicaEntity bot = feed.Entities.First(e => e.IsMob() && e.Id != feed.OwnEntity);
        Assert.True(rig.Session.Poses.TryGetPose(bot, out RenderPose botPose));
        Assert.InRange(botPose.X, 1, feed.Replica.Map.Width);
        Assert.Equal(bot.State.Z, botPose.Z);

        ReplicaEntity item = feed.Entities.First(e => !e.IsMob());
        Assert.True(rig.Session.Poses.TryGetPose(item, out RenderPose itemPose));
        Assert.Equal(item.State.X, itemPose.X, 3);
        Assert.Equal(item.State.Y, itemPose.Y, 3);
        Assert.Equal(0, itemPose.Posture);
    }

    [Fact]
    public void MobsAreGatheredForTheCrosshairWithTheirPostures()
    {
        (RecordingFeed feed, PlayRig rig) = Rig(bots: 5);
        rig.Run(1.0);
        var candidates = new List<PickCandidate>();
        rig.Session.Poses.GatherMobs(candidates);
        Assert.Equal(1 + 5, candidates.Count);
        Assert.Contains(candidates, c => c.Id == feed.OwnEntity);
        Assert.All(candidates, c => Assert.False(c.Lying));
    }

    [Fact]
    public void TheCameraLayerFollowsTheOptionsAndThePosture()
    {
        (RecordingFeed feed, PlayRig rig) = Rig(bots: 0);
        rig.Run(0.5);
        Assert.Equal(CameraLayer.StandingEye, rig.Session.CameraFrame(1.0 / 90).Up, 3);
        // The options' off switches reach the layer.
        rig.Session.Options.HeadBob = false;
        rig.Session.Options.CameraShake = false;
        rig.Session.CameraFrame(1.0 / 90);
        Assert.False(rig.Session.Camera.HeadBob);
        Assert.False(rig.Session.Camera.Shake);
        rig.Session.Options.HeadBob = true;
        rig.Session.Options.CameraShake = true;
        // Lying: the server tells the posture and the eye sinks to the lying eye (0.4 m, D157).
        rig.Session.Rest();
        rig.Run(1.0);
        CameraOffsets lying = default;
        for (int i = 0; i < 90; i++)
            lying = rig.Session.CameraFrame(1.0 / 90);
        Assert.Equal(Posture.Lying, rig.Session.Prediction.Predicted.Posture);
        Assert.InRange(lying.Up, 0.35f, 0.5f);
    }

    [Fact]
    public void AFreeCameraDoesNotMoveTheAvatarButStillSendsFrames()
    {
        (RecordingFeed feed, PlayRig rig) = Rig(bots: 0);
        rig.Run(0.5);
        long before = rig.Session.Prediction.Predicted.Y;
        rig.Keys = MoveKeys.Forward;
        rig.Controlling = false;
        rig.Run(1.0);
        Assert.Equal(before, rig.Session.Prediction.Predicted.Y);
        Assert.InRange(feed.Motions.Count, 28, 32);
        rig.Controlling = true;
        rig.Run(1.0);
        Assert.True(rig.Session.Prediction.Predicted.Y > before + 256 * 5);
    }

    [Fact]
    public void TheStatisticsCountOthersSeenAndMoved()
    {
        (RecordingFeed feed, PlayRig rig) = Rig(bots: 6, seed: 5);
        rig.Run(10.0);
        Assert.Equal(6, rig.Session.OthersSeen);
        Assert.True(rig.Session.OthersMoved >= 4);
        Assert.True(rig.Session.OthersSpeed.Count > 6 * 300);
        Assert.True(rig.Session.OwnSpeed.Count > 500);
        Assert.InRange(rig.Session.OthersSpeed.P99, 12.0, 14.5);
    }

    [Fact]
    public void AWorldThingKeptUnderTheCrosshairIsHoveredAgainTwiceASecondAndAScreenEntryIsNot()
    {
        // DV687: the repeat brings the server's answer after a walk into or out of reach
        (RecordingFeed feed, PlayRig rig) = Rig(bots: 0);
        rig.Run(0.5);
        int Hovers() => feed.Actions.Count(a => a.Kind == ActionKind.Hover);
        var thing = new ClickTarget(TargetKind.Entity, 7, 0, 0, 0, 16, 16, AimZone.Head);
        const double t = 100;
        Assert.True(rig.Session.Hover(thing, t));
        Assert.False(rig.Session.Hover(thing, t + 0.2));
        Assert.False(rig.Session.Hover(thing, t + 0.49));
        Assert.True(rig.Session.Hover(thing, t + PlaySession.RepeatHoverInterval));
        Assert.Equal(2, Hovers());
        Assert.Equal(feed.Actions[^2], feed.Actions[^1]); // the same hover: target, zone and all (the frame tag aside, none moved)

        // a new zone right after a repeat still goes out on the zone's own pace, not the repeat's
        double r = t + PlaySession.RepeatHoverInterval;
        Assert.False(rig.Session.Hover(thing with { Aim = AimZone.Chest }, r + 0.1));
        Assert.True(rig.Session.Hover(thing with { Aim = AimZone.Chest }, r + PlaySession.ZoneHoverInterval));
        Assert.Equal(AimZone.Chest, feed.Actions[^1].AimZone);

        // held for ten seconds, a call every 1/60 s: two a second, within the server's bucket (ActionLimits.Hover: 10 a second)
        int before = Hovers();
        double start = r + 1;
        for (int frame = 0; frame < 600; frame++)
            rig.Session.Hover(thing with { Aim = AimZone.Chest }, start + frame / 60.0);
        Assert.InRange(Hovers() - before, 19, 21);
        Assert.True(1 / PlaySession.RepeatHoverInterval < ActionLimits.Hover.PerSecond);

        // a turf and a ceiling cell repeat too; a screen entry (the hand slot under the free pointer) does not
        double u = start + 20;
        var cell = new ClickTarget(TargetKind.Turf, 0, 3, 4, 1);
        Assert.True(rig.Session.Hover(cell, u));
        Assert.True(rig.Session.Hover(cell, u + 0.5));
        var slot = new ClickTarget(TargetKind.Screen, 42, 0, 0, 0);
        Assert.True(rig.Session.Hover(slot, u + 1));
        before = Hovers();
        for (int frame = 0; frame < 300; frame++)
            rig.Session.Hover(slot, u + 1 + frame / 60.0);
        Assert.Equal(before, Hovers());

        // nothing under the crosshair: one unhover, no repeats
        Assert.True(rig.Session.Hover(ClickTarget.None, u + 7));
        for (int frame = 0; frame < 120; frame++)
            Assert.False(rig.Session.Hover(ClickTarget.None, u + 7 + frame / 60.0));
        Assert.Equal(before, Hovers());
    }

    [Fact]
    public void WhileThePetCommandsKeyIsHeldNoRepeatGoesOutButANewThingOrZoneDoes()
    {
        // DV687: tg's pets listen to MouseEntered while the key is held (obeys_commands.dm); a repeat would be a new entering and open the radial again
        (RecordingFeed feed, PlayRig rig) = Rig(bots: 0);
        rig.Run(0.5);
        int Hovers() => feed.Actions.Count(a => a.Kind == ActionKind.Hover);
        var pet = new ClickTarget(TargetKind.Entity, 7, 0, 0, 0, 16, 16);
        const double t = 100;
        Assert.True(rig.Session.Hover(pet, t));
        rig.Session.Keybinding(KeybindingCode.ViewPetCommands);
        int before = Hovers();
        for (int frame = 0; frame < 300; frame++) // five seconds on the pet
            Assert.False(rig.Session.Hover(pet, t + 0.1 + frame / 60.0));
        Assert.Equal(before, Hovers());

        // a zone change and a new thing still go out
        Assert.True(rig.Session.Hover(pet with { Aim = AimZone.Head }, t + 6));
        var other = new ClickTarget(TargetKind.Entity, 8, 0, 0, 0, 16, 16);
        Assert.True(rig.Session.Hover(other, t + 6.2));
        Assert.Equal(before + 2, Hovers());
        Assert.False(rig.Session.Hover(other, t + 7)); // and no repeat of it either

        // the key let go: the repeat is back
        rig.Session.Keybinding(KeybindingCode.ViewPetCommands, up: true);
        Assert.True(rig.Session.Hover(other, t + 7.5));
        Assert.True(rig.Session.Hover(other, t + 8.0));
        Assert.Equal(before + 4, Hovers());
    }

    [Fact]
    public void AHeldKeybindingWhoseKeyIsNoLongerDownIsReleasedOnce()
    {
        // the window lost the focus or a panel took the key as it was let go: its own release never came
        (RecordingFeed feed, PlayRig rig) = Rig(bots: 0);
        rig.Run(0.5);
        List<ActionMessage> Sent(KeybindingCode code) => feed.Actions.Where(a => a.Kind == ActionKind.Keybinding && a.Keybinding == code).ToList();
        rig.Session.Keybinding(KeybindingCode.ViewPetCommands);
        rig.Session.Keybinding(KeybindingCode.HoldThrowMode);
        rig.Session.Keybinding(KeybindingCode.SwapHands); // no release of its own: never released
        rig.Session.ReleaseLetGo(_ => true);
        Assert.Single(Sent(KeybindingCode.ViewPetCommands));

        // the pet key is up now, the throw key still down
        rig.Session.ReleaseLetGo(code => code == KeybindingCode.HoldThrowMode);
        Assert.Equal(new[] { false, true }, Sent(KeybindingCode.ViewPetCommands).Select(a => a.KeyUp));
        Assert.Single(Sent(KeybindingCode.HoldThrowMode));
        Assert.Single(Sent(KeybindingCode.SwapHands));
        rig.Session.ReleaseLetGo(_ => false);
        rig.Session.ReleaseLetGo(_ => false);
        Assert.Equal(2, Sent(KeybindingCode.ViewPetCommands).Count); // once
        Assert.Equal(new[] { false, true }, Sent(KeybindingCode.HoldThrowMode).Select(a => a.KeyUp));
        Assert.Single(Sent(KeybindingCode.SwapHands));

        // a release the scene sent itself is not sent again
        rig.Session.Keybinding(KeybindingCode.Resist);
        rig.Session.Keybinding(KeybindingCode.Resist, up: true);
        rig.Session.ReleaseLetGo(_ => false);
        Assert.Equal(2, Sent(KeybindingCode.Resist).Count);

        // and the repeat of the thing under the crosshair is back once the pet key is released
        var thing = new ClickTarget(TargetKind.Entity, 7, 0, 0, 0, 16, 16);
        Assert.True(rig.Session.Hover(thing, 100));
        Assert.True(rig.Session.Hover(thing, 100.5));
    }

    [Fact]
    public void TheAnswerInHandCountsOnlyForTheLookItArrivedIn()
    {
        // DV687: an answer from an earlier look at a thing may have been in reach then; a new look waits for its own
        (RecordingFeed _, PlayRig rig) = Rig(bots: 0);
        rig.Run(0.5);
        var pen = new ClickTarget(TargetKind.Entity, 7, 0, 0, 0, 16, 16);
        var other = new ClickTarget(TargetKind.Entity, 8, 0, 0, 0, 16, 16);
        var answer = new ContextHintsMessage { TargetKind = TargetKind.Entity, Target = 7, Name = "pen" };
        const double t = 100;
        rig.Session.Hover(pen, t);
        rig.Session.HintsArrived(answer);
        Assert.Same(answer, KeyChips.AnswerFor(rig.Session.Hints, pen));

        // the same look, a zone change and the repeats: kept
        rig.Session.Hover(pen with { Aim = AimZone.Chest }, t + 0.3);
        rig.Session.Hover(pen with { Aim = AimZone.Chest }, t + 0.9);
        Assert.Same(answer, KeyChips.AnswerFor(rig.Session.Hints, pen));

        // pen, away, pen: the old answer no longer counts
        rig.Session.Hover(ClickTarget.None, t + 1);
        rig.Session.Hover(pen, t + 1.2);
        Assert.Null(KeyChips.AnswerFor(rig.Session.Hints, pen));
        rig.Session.HintsArrived(answer);
        rig.Session.Hover(other, t + 1.4);
        rig.Session.Hover(pen, t + 1.6);
        Assert.Null(KeyChips.AnswerFor(rig.Session.Hints, pen));

        // the answer for this look counts again, and the open radial's rebuild finds it for its target (OnHints)
        var fresh = new ContextHintsMessage { TargetKind = TargetKind.Entity, Target = 7, Name = "pen" };
        rig.Session.HintsArrived(fresh);
        Assert.Same(fresh, KeyChips.AnswerFor(rig.Session.Hints, pen));
    }

    [Fact]
    public void TheKeyToMotionProbeSeesTheFirstFrameAfterTheKey()
    {
        (RecordingFeed feed, PlayRig rig) = Rig(bots: 0);
        rig.Run(1.0);
        rig.Keys = MoveKeys.Forward;
        rig.Run(1.0);
        (double last, double mean, int count) = rig.Session.Probe.Motion;
        Assert.Equal(1, count);
        // The player's own capsule moves on the very next frame or two (no round trip: prediction).
        Assert.InRange(last, 0, 60);
        Assert.InRange(rig.Session.Probe.Ack.Count, 1, 10);
    }

    [Fact]
    public void ADespawnedMobStopsBeingDrawnButStaysInTheCount()
    {
        var feed = new FakeFeed("Tester", seed: 3, bots: 4);
        var rig = new PlayRig(feed, 60);
        rig.Run(1.0);
        int seen = rig.Session.OthersSeen;
        Assert.Equal(4, seen);
        rig.Run(14.0); // the scripted leaver goes at tick 200 (10 s), a joiner arrives at tick 240
        Assert.Equal(5, rig.Session.OthersSeen); // the one that left and the one that came
        Assert.DoesNotContain(feed.Entities, e => e.Id == 3);
        Assert.False(feed.Replica.TryGet(3, out _));
    }
}
