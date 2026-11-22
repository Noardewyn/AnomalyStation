using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Bots;
using Anomaly.Tools.Bots;

namespace Anomaly.Server.Tests.M4aServer.P6;

/// <summary>The bots of protocol version 4 (tools/Anomaly.Tools.Bots): the fiddle behaviour and the abuse payloads for the new fields, against the whole server.</summary>
public class BotsV4Tests
{
    // ---- names ----

    [Theory]
    [InlineData("fiddle", BotBehaviour.Fiddle, false)]
    [InlineData("abuse-forged-target", BotBehaviour.ForgedTarget, true)]
    [InlineData("abuse-hover-flood", BotBehaviour.HoverFlood, true)]
    [InlineData("abuse-bad-v4", BotBehaviour.BadV4, true)]
    public void TheVersionFourBehavioursHaveNames(string text, BotBehaviour expected, bool abuse)
    {
        Assert.True(BotBehaviourNames.TryParse(text, out BotBehaviour behaviour));
        Assert.Equal(expected, behaviour);
        Assert.Equal(text, BotBehaviourNames.ToName(expected));
        Assert.Equal(abuse, BotBehaviourNames.IsAbuse(expected));
        Assert.Contains(text, BotArgs.Usage);
        BotRunnerOptions options = BotArgs.Parse(new[] { "--behaviour=" + text }, out string error);
        Assert.Null(error);
        Assert.Equal(expected, options.Behaviour);
    }

    [Fact]
    public void TheReportNamesTheNewActionKinds()
    {
        Assert.Equal("keybinding", BotReport.ActionName((int)ActionKind.Keybinding));
        Assert.Equal("hover", BotReport.ActionName((int)ActionKind.Hover));
        Assert.True(BotStats.ActionKindCount > (int)ActionKind.Hover);
    }

    // ---- the payloads ----

    [Fact]
    public void TheForgedTargetsAreLegalMessagesOnTargetsNoWorldHas()
    {
        var random = new BotRandom(9);
        for (int i = 0; i < 300; i++)
        {
            Assert.True(Codec.TryDecode(AbusePayloads.ForgedTurfClick(5, random), out ActionMessage turf, out _));
            Assert.Equal((ActionKind.Click, TargetKind.Turf), (turf.Kind, turf.TargetKind));
            Assert.InRange(turf.TurfZ, 2, Limits.MaxMapLevels); // the test room has one level

            Assert.True(Codec.TryDecode(AbusePayloads.ForgedTurfHover(5, random), out ActionMessage turfHover, out _));
            Assert.Equal((ActionKind.Hover, TargetKind.Turf), (turfHover.Kind, turfHover.TargetKind));

            Assert.True(Codec.TryDecode(AbusePayloads.ForgedScreenClick(5, random), out ActionMessage screen, out _));
            Assert.Equal((ActionKind.Click, TargetKind.Screen), (screen.Kind, screen.TargetKind));
            Assert.True(screen.Target >= AbusePayloads.MissingEntityBase);

            Assert.True(Codec.TryDecode(AbusePayloads.ForgedHover(5, random), out ActionMessage hover, out _));
            Assert.Equal(ActionKind.Hover, hover.Kind);
            Assert.True(hover.TargetKind is TargetKind.Entity or TargetKind.Screen);
            Assert.True(hover.Target >= AbusePayloads.MissingEntityBase);
        }

        Assert.True(Codec.TryDecode(AbusePayloads.EntityClick(3, 77, ClickButton.Middle), out ActionMessage click, out _));
        Assert.Equal((TargetKind.Entity, 77L, ClickButton.Middle), (click.TargetKind, click.Target, click.Button));
        Assert.True(Codec.TryDecode(AbusePayloads.HoverOnEntity(3, 78), out ActionMessage own, out _));
        Assert.Equal((ActionKind.Hover, 78L), (own.Kind, own.Target));
    }

    [Fact]
    public void ABadVersionFourActionIsTheActionIdAndNothingTheCodecAccepts()
    {
        var random = new BotRandom(31);
        var errors = new HashSet<ProtocolError>();
        for (int i = 0; i < 2000; i++)
        {
            uint frame = (uint)random.Next(0, 1_000_000);
            byte[] bytes = AbusePayloads.BadV4Action(frame, random);
            Assert.InRange(bytes.Length, 1, Limits.MaxInboundMessageBytes);
            Assert.Equal((byte)MessageId.Action, bytes[0]);
            Assert.Equal(Channel.Control, AbusePayloads.ChannelFor(bytes));
            Assert.False(Codec.TryDecode(bytes, out ActionMessage _, out ProtocolError error), BitConverter.ToString(bytes));
            errors.Add(error);
        }

        Assert.NotEmpty(errors);
    }

    // ---- against the server ----

    private static Bot Joined(BotServerRig rig, string name, BotBehaviour behaviour, Func<BotOptions, BotOptions> options = null)
    {
        Bot bot = rig.AddBot(name, behaviour, options: options);
        rig.UntilJoined(bot);
        return bot;
    }

    [Fact]
    public void AFiddlingBotHoversClicksAndPressesKeysAndStaysJoined()
    {
        using var rig = new BotServerRig(strict: false);
        Bot fiddler = Joined(rig, "Fiddler", BotBehaviour.Fiddle);
        Bot other = Joined(rig, "Other", BotBehaviour.Wander);
        rig.Tick(600); // 30 seconds
        Session s = rig.SessionOf("Fiddler");
        Assert.Equal(BotState.Joined, fiddler.State);
        Assert.Equal(BotState.Joined, other.State);
        Assert.Equal(0, s.Strikes);
        Assert.Equal(0, s.RateLimited);
        Assert.True(fiddler.Stats.FiddleActions >= 50, $"{fiddler.Stats.FiddleActions} fiddles");
        long[] byKind = fiddler.Stats.ActionsByKind;
        Assert.True(byKind[(int)ActionKind.Hover] > 0, "no hovers");
        Assert.True(byKind[(int)ActionKind.Keybinding] > 0, "no keybindings");
        Assert.True(byKind[(int)ActionKind.Click] > 0, "no clicks");
        Assert.True(s.HoversApplied > 0, "the server ran no hover");
        Assert.True(s.KeybindingsApplied > 0, "the server ran no keybinding");
        // The bot sees only legal targets: nothing it did was rejected for being forged except what raced a despawn.
        Assert.True(s.ClicksRejected <= fiddler.Stats.FiddleActions / 4, $"{s.ClicksRejected} rejected of {fiddler.Stats.FiddleActions}");
        Assert.Equal(0, rig.SessionOf("Other").Strikes);
    }

    [Fact]
    public void ForgedTargetsAreDroppedSilentlyAndTheSessionStays()
    {
        using var rig = new BotServerRig(strict: false);
        Bot forger = Joined(rig, "Forger", BotBehaviour.ForgedTarget);
        rig.Tick(400);
        Session s = rig.SessionOf("Forger");
        Assert.Equal(BotState.Joined, forger.State);
        Assert.Equal(0, s.Strikes);
        Assert.Equal(0, s.RateLimited);
        Assert.True(s.ClicksRejected >= 40, $"{s.ClicksRejected} rejected");
        Assert.Equal(0, s.HoversApplied);
        Assert.Equal(0, s.KeybindingsApplied);
        Assert.DoesNotContain(rig.Host.Log.Commands, c => c.Kind == CommandKind.Action && c.Action == ActionKind.Keybinding);
    }

    [Fact]
    public void AHoverFloodOverTheHoverRateIsDroppedSilently()
    {
        using var rig = new BotServerRig(strict: false);
        Bot flood = Joined(rig, "HoverFlood", BotBehaviour.HoverFlood);
        rig.Tick(400);
        Session s = rig.SessionOf("HoverFlood");
        Assert.Equal(BotState.Joined, flood.State);
        Assert.Equal(0, s.Strikes);
        Assert.Equal(0, s.RateLimited);
        Assert.True(s.HoversApplied > 0);
        Assert.True(s.HoversDropped > 0);
        Assert.True(flood.Stats.ActionsByKind[(int)ActionKind.Hover] > s.HoversApplied, "the surplus was not dropped");
    }

    [Fact]
    public void ABadVersionFourActionIsAStrikeAndThreeClose()
    {
        using var rig = new BotServerRig(strict: false);
        Bot bad = rig.AddBot("BadV4", BotBehaviour.BadV4);
        for (int i = 0; i < 400 && bad.State != BotState.Disconnected; i++) rig.Tick();
        Assert.Equal(DisconnectReason.ProtocolViolation, bad.LastDisconnect.Reason);
        Assert.Equal(Limits.MaxStrikes, rig.SessionOf("BadV4").Strikes);
        Assert.DoesNotContain(rig.Host.Log.Commands, c => c.Kind == CommandKind.Action);
    }
}
