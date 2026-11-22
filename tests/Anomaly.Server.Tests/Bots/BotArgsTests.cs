using Anomaly.Tools.Bots;

namespace Anomaly.Server.Tests.Bots;

public class BotArgsTests
{
    [Fact]
    public void WithoutArgumentsItIsTenWanderersOnLocalhost()
    {
        BotRunnerOptions o = BotArgs.Parse(Array.Empty<string>(), out string error);
        Assert.Null(error);
        Assert.NotNull(o);
        Assert.Equal("127.0.0.1", o.Host);
        Assert.Equal(7777, o.Port);
        Assert.Equal(10, o.Bots);
        Assert.Equal(1.0, o.Minutes);
        Assert.Equal(BotBehaviour.Wander, o.Behaviour);
        Assert.Equal(1UL, o.Seed);
        Assert.Equal("Bot", o.NamePrefix);
        Assert.Null(o.ReportPath);
        Assert.False(o.Reconnect);
    }

    [Fact]
    public void PredictIsAFlagAndOffByDefault()
    {
        Assert.False(BotArgs.Parse(Array.Empty<string>(), out _).Predict);
        BotRunnerOptions o = BotArgs.Parse(new[] { "--predict", "--behaviour", "mixed" }, out string error);
        Assert.Null(error);
        Assert.True(o.Predict);
        Assert.Equal(BotBehaviour.Mixed, o.Behaviour);
        Assert.Null(BotArgs.Parse(new[] { "--predict=yes" }, out string bad));
        Assert.Contains("--predict takes no value", bad);
        Assert.Contains("--predict", BotArgs.Usage);
    }

    [Fact]
    public void ItReadsTheSoakCommandLine()
    {
        BotRunnerOptions o = BotArgs.Parse(
            new[] { "--host", "10.0.0.5", "--port=7788", "--bots", "50", "--minutes", "30", "--behaviour", "wander", "--seed", "42", "--name-prefix", "Soak", "--report", "/tmp/bots.json", "--reconnect", "--join-timeout", "90", "--ramp", "10", "--status-every=5", "--no-per-bot" },
            out string error);
        Assert.Null(error);
        Assert.Equal("10.0.0.5", o.Host);
        Assert.Equal(7788, o.Port);
        Assert.Equal(50, o.Bots);
        Assert.Equal(30.0, o.Minutes);
        Assert.Equal(42UL, o.Seed);
        Assert.Equal("Soak", o.NamePrefix);
        Assert.Equal("/tmp/bots.json", o.ReportPath);
        Assert.True(o.Reconnect);
        Assert.Equal(90.0, o.JoinTimeoutSeconds);
        Assert.Equal(10, o.RampPerSecond);
        Assert.Equal(5, o.StatusEverySeconds);
        Assert.True(o.NoPerBot);
    }

    [Theory]
    [InlineData("wander", BotBehaviour.Wander)]
    [InlineData("idle", BotBehaviour.Idle)]
    [InlineData("churn", BotBehaviour.Churn)]
    [InlineData("abuse-flood", BotBehaviour.Flood)]
    [InlineData("abuse-malformed", BotBehaviour.Malformed)]
    [InlineData("abuse-garbage", BotBehaviour.Malformed)]
    [InlineData("abuse-oversized", BotBehaviour.Oversized)]
    [InlineData("abuse-early-command", BotBehaviour.PreWelcomeCommand)]
    [InlineData("abuse-wrong-version", BotBehaviour.WrongVersion)]
    [InlineData("rest", BotBehaviour.Rest)]
    [InlineData("shove", BotBehaviour.Shove)]
    [InlineData("mixed", BotBehaviour.Mixed)]
    [InlineData("abuse-action-flood", BotBehaviour.ActionFlood)]
    [InlineData("abuse-forged-seq", BotBehaviour.ForgedSeq)]
    [InlineData("abuse-bad-action", BotBehaviour.BadAction)]
    [InlineData("abuse-dev", BotBehaviour.DevAbuse)]
    [InlineData("Churn", BotBehaviour.Churn)]
    public void EveryBehaviourHasAName(string text, BotBehaviour expected)
    {
        Assert.True(BotBehaviourNames.TryParse(text, out BotBehaviour behaviour));
        Assert.Equal(expected, behaviour);
        BotRunnerOptions o = BotArgs.Parse(new[] { "--behaviour=" + text }, out string error);
        Assert.Null(error);
        Assert.Equal(expected, o.Behaviour);
    }

    [Fact]
    public void EveryBehaviourNameRoundTrips()
    {
        foreach (BotBehaviour behaviour in Enum.GetValues<BotBehaviour>())
        {
            string name = BotBehaviourNames.ToName(behaviour);
            Assert.True(BotBehaviourNames.TryParse(name, out BotBehaviour back), name);
            Assert.Equal(behaviour, back);
        }
        Assert.Equal(Enum.GetValues<BotBehaviour>().Length, BotBehaviourNames.All.Count());
    }

    [Fact]
    public void OnlyTheAbuseNamesAreAbuse()
    {
        Assert.False(BotBehaviourNames.IsAbuse(BotBehaviour.Wander));
        Assert.False(BotBehaviourNames.IsAbuse(BotBehaviour.Idle));
        Assert.False(BotBehaviourNames.IsAbuse(BotBehaviour.Churn));
        Assert.False(BotBehaviourNames.IsAbuse(BotBehaviour.Rest));
        Assert.False(BotBehaviourNames.IsAbuse(BotBehaviour.Shove));
        Assert.False(BotBehaviourNames.IsAbuse(BotBehaviour.Mixed));
        Assert.True(BotBehaviourNames.IsAbuse(BotBehaviour.ActionFlood));
        Assert.True(BotBehaviourNames.IsAbuse(BotBehaviour.ForgedSeq));
        Assert.True(BotBehaviourNames.IsAbuse(BotBehaviour.BadAction));
        Assert.True(BotBehaviourNames.IsAbuse(BotBehaviour.DevAbuse));
        Assert.True(BotBehaviourNames.IsAbuse(BotBehaviour.Flood));
        Assert.True(BotBehaviourNames.IsAbuse(BotBehaviour.Malformed));
        Assert.True(BotBehaviourNames.IsAbuse(BotBehaviour.Oversized));
        Assert.True(BotBehaviourNames.IsAbuse(BotBehaviour.PreWelcomeCommand));
        Assert.True(BotBehaviourNames.IsAbuse(BotBehaviour.WrongVersion));
    }

    [Theory]
    [InlineData("--bots", "0")]
    [InlineData("--bots", "5000")]
    [InlineData("--bots", "x")]
    [InlineData("--port", "0")]
    [InlineData("--port", "70000")]
    [InlineData("--minutes", "-1")]
    [InlineData("--minutes", "soon")]
    [InlineData("--behaviour", "dance")]
    [InlineData("--behaviour", "abuse-")]
    [InlineData("--seed", "-4")]
    [InlineData("--name-prefix", "")]
    [InlineData("--name-prefix", "ThisPrefixIsMuchTooLongForAPlayerNameAtAll")]
    [InlineData("--join-timeout", "-3")]
    [InlineData("--ramp", "-1")]
    [InlineData("--report", "")]
    public void ABadValueIsAnError(string name, string value)
    {
        Assert.Null(BotArgs.Parse(new[] { name, value }, out string error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void AControlCharacterInThePrefixIsAnError()
    {
        Assert.Null(BotArgs.Parse(new[] { "--name-prefix", "Bad\nName" }, out string error));
        Assert.Contains("control", error);
    }

    [Fact]
    public void AnUnknownArgumentOrAMissingValueIsAnError()
    {
        Assert.Null(BotArgs.Parse(new[] { "--frobnicate" }, out string error));
        Assert.Contains("--frobnicate", error);
        Assert.Null(BotArgs.Parse(new[] { "--bots" }, out error));
        Assert.Contains("needs a value", error);
        Assert.Null(BotArgs.Parse(new[] { "--reconnect=yes" }, out error));
    }

    [Fact]
    public void HelpIsNotAnError()
    {
        Assert.Null(BotArgs.Parse(new[] { "--help" }, out string error));
        Assert.Null(error);
        Assert.Contains("--behaviour", BotArgs.Usage);
        Assert.Contains("abuse-flood", BotArgs.Usage);
    }
}
