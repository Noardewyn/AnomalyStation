using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;
using TimerWindow = Anomaly.Client.Shared.Tgui.Interfaces.Timer; // System.Threading.Timer is in scope too

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps AS1: the client's Timer window (tg's Timer.tsx) shows the set time as mm:ss, its repeat and start buttons and the four
/// adjust buttons, locked while it counts; the HealthSensor window (HealthSensor.tsx) its scan and target toggles and the holder's
/// health bar; the InfraredEmitter window (InfraredEmitter.tsx) its power and visibility toggles. All three are registered under their
/// names and send tg's acts.
/// </summary>
public class AssembliesAS1WindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string iface, Func<TguiContext, UiNode> render, string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, iface, "w", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private static bool Has(UiNode tree, string key) => tree.Walk().Any(n => n.Key == key);

    [Fact]
    public void TheTimerWindowShowsTheSetTimeAndAdjustsIt()
    {
        Assert.NotNull(TguiInterfaces.Find("Timer"));
        // a stopped timer set to 65 seconds
        var (context, stopped, acts) = Render("Timer", TimerWindow.Render, "{\"seconds\":5,\"minutes\":1,\"timing\":0,\"loop\":0}");
        Assert.Equal((275.0, 115.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("01:05 ", ByKey(stopped, "clock").Text);
        Assert.Equal("Repeat", ByKey(stopped, "repeat").Text);
        Assert.False(ByKey(stopped, "repeat").Selected);
        Assert.Equal("Start", ByKey(stopped, "time").Text);
        foreach (string key in new[] { "adjust-30", "adjust-1", "adjust+1", "adjust+30" })
            Assert.False(ByKey(stopped, key).Disabled);
        ByKey(stopped, "adjust-30").OnClick();
        ByKey(stopped, "adjust-1").OnClick();
        ByKey(stopped, "adjust+1").OnClick();
        ByKey(stopped, "adjust+30").OnClick();
        ByKey(stopped, "repeat").OnClick();
        ByKey(stopped, "time").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("input", "{\"adjust\":-30}"), ("input", "{\"adjust\":-1}"), ("input", "{\"adjust\":1}"), ("input", "{\"adjust\":30}"),
            ("repeat", null), ("time", null),
        }, acts);

        // a repeating timer counting down: the adjust buttons are locked
        var (_, timing, _) = Render("Timer", TimerWindow.Render, "{\"seconds\":42,\"minutes\":0,\"timing\":1,\"loop\":1}");
        Assert.Equal("00:42 ", ByKey(timing, "clock").Text);
        Assert.Equal("Repeating", ByKey(timing, "repeat").Text);
        Assert.True(ByKey(timing, "repeat").Selected);
        Assert.Equal("Stop", ByKey(timing, "time").Text);
        Assert.True(ByKey(timing, "time").Selected);
        foreach (string key in new[] { "adjust-30", "adjust-1", "adjust+1", "adjust+30" })
            Assert.True(ByKey(timing, key).Disabled);
    }

    [Fact]
    public void TheHealthSensorWindowShowsItsTogglesAndTheHoldersHealth()
    {
        Assert.NotNull(TguiInterfaces.Find("HealthSensor"));
        // off, never scanned: tg sends a null health, which is defined, so the bar shows "Off"; with no health field there is none
        var (context, off, acts) = Render("HealthSensor", HealthSensor.Render, "{\"health\":null,\"scanning\":0,\"target\":0}");
        Assert.Equal((360.0, 115.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Off", ByKey(off, "scanning").Text);
        Assert.False(ByKey(off, "scanning").Selected);
        Assert.Equal("Checking for Crit", ByKey(off, "target").Text);
        Assert.Equal("Off", ByKey(off, "health").Text);
        Assert.False(Has(Render("HealthSensor", HealthSensor.Render, "{\"scanning\":0,\"target\":0}").Tree, "health"));
        ByKey(off, "scanning").OnClick();
        ByKey(off, "target").OnClick();
        Assert.Equal(new (string, string)[] { ("scanning", null), ("target", null) }, acts);

        // scanning a holder at 35% for death: the bar in the average range
        var (_, scanning, _) = Render("HealthSensor", HealthSensor.Render, "{\"health\":35,\"scanning\":1,\"target\":1}");
        Assert.Equal("On", ByKey(scanning, "scanning").Text);
        Assert.True(ByKey(scanning, "scanning").Selected);
        Assert.Equal("Checking for Death", ByKey(scanning, "target").Text);
        UiNode bar = ByKey(scanning, "health");
        Assert.Equal(UiKind.ProgressBar, bar.Kind);
        Assert.Equal(0.35, bar.Value, 6);
        Assert.Equal("35", bar.Text);
        Assert.Equal(3, bar.Ranges.Count);

        // switched off after a scan: the bar stays, empty and "Off"
        var (_, stopped, _) = Render("HealthSensor", HealthSensor.Render, "{\"health\":80,\"scanning\":0,\"target\":0}");
        Assert.Equal(0, ByKey(stopped, "health").Value);
        Assert.Equal("Off", ByKey(stopped, "health").Text);
    }

    [Fact]
    public void TheInfraredEmitterWindowSwitchesItsPowerAndVisibility()
    {
        Assert.NotNull(TguiInterfaces.Find("InfraredEmitter"));
        var (context, off, acts) = Render("InfraredEmitter", InfraredEmitter.Render, "{\"on\":0,\"visible\":0}");
        Assert.Equal((225.0, 110.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Off", ByKey(off, "power").Text);
        Assert.Equal("Invisible", ByKey(off, "visibility").Text);
        Assert.Contains("Status", off.AllText());
        Assert.Contains("Visibility", off.AllText());
        ByKey(off, "power").OnClick();
        ByKey(off, "visibility").OnClick();
        Assert.Equal(new (string, string)[] { ("power", null), ("visibility", null) }, acts);

        var (_, on, _) = Render("InfraredEmitter", InfraredEmitter.Render, "{\"on\":1,\"visible\":1}");
        Assert.Equal("On", ByKey(on, "power").Text);
        Assert.True(ByKey(on, "power").Selected);
        Assert.Equal("Visible", ByKey(on, "visibility").Text);
        Assert.True(ByKey(on, "visibility").Selected);
    }
}
