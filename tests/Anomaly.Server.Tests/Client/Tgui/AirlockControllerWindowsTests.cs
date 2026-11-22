using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Package E8: the client's AirlockController (the incinerators' consoles) and AirlockButtonController (the access consoles) windows,
/// tg's AirlockController.tsx and AirlockButtonController.tsx, without the engine: the size, the status rows with tg's capitalised
/// values and colours, the state's buttons and their acts, the spinning fan while cycling; the access console's door buttons with
/// their open, cycle and close texts and the door's REF in the act, disabled while busy, and the no-doors notice.
/// </summary>
public class AirlockControllerWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(TguiInterface render, string name, string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, name, "device", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private static string Data(string state, object pressure, string pump, string inner, string outer)
        => "{\"airlockState\":\"" + state + "\",\"sensorPressure\":" + (pressure is string s ? "\"" + s + "\"" : pressure) + ",\"pumpStatus\":\"" + pump
            + "\",\"interiorStatus\":\"" + inner + "\",\"exteriorStatus\":\"" + outer + "\"}";

    [Fact]
    public void TheIncineratorConsoleShowsItsStatusAndTheButtonsOfEachState()
    {
        var (context, tree, acts) = Render(AirlockController.Render, "AirlockController", Data("closed", 101.3, "release", "closed", "closed"));
        Assert.Equal((500.0, 190.0), (context.WindowWidth, context.WindowHeight));
        UiNode section = tree.Walk().Single(n => n.Kind == UiKind.Section);
        Assert.Equal("Airlock Status", section.Title);
        Assert.Equal(new[] { "Current Status", "Chamber Pressure", "Control Pump", "Interior Door", "Exterior Door" },
            tree.Walk().Where(n => n.Kind == UiKind.LabeledItem).Select(n => n.Label));
        Assert.Equal("Inactive", ByKey(tree, "status").Text);
        Assert.Equal(("101.3 kPa ", "white"), (ByKey(tree, "pressure").Text, ByKey(tree, "pressure").Color));
        Assert.DoesNotContain(tree.Walk(), n => n.Kind == UiKind.Icon);
        Assert.Equal("Release", ByKey(tree, "pump").Text);
        Assert.Equal(("Closed", null), (ByKey(tree, "interior").Text, ByKey(tree, "interior").Color));
        // closed: open either door
        List<UiNode> buttons = section.Buttons.Walk().Where(n => n.Kind == UiKind.Button).ToList();
        Assert.Equal(new[] { ("Open Interior Airlock", "lock-open"), ("Open Exterior Airlock", "lock-open") }, buttons.Select(b => (b.Text, b.Icon)));
        buttons[0].OnClick();
        buttons[1].OnClick();
        Assert.Equal(new (string, string)[] { ("cycleInterior", null), ("cycleExterior", null) }, acts);

        // cycling: the abort button and the fan that spins, amber
        var (_, cycling, cyclingActs) = Render(AirlockController.Render, "AirlockController", Data("depressurize", 40.5, "siphon", "closed", "closed"));
        Assert.Equal("Cycling to Exterior Airlock", ByKey(cycling, "status").Text);
        UiNode fan = ByKey(cycling, "pressure-icon");
        Assert.Equal(("fan", true, "average"), (fan.Icon, fan.Spin, fan.Color));
        Assert.Equal("Siphon", ByKey(cycling, "pump").Text);
        UiNode abort = cycling.Walk().Single(n => n.Kind == UiKind.Section).Buttons;
        Assert.Equal(("Abort", "stop-circle"), (abort.Text, abort.Icon));
        abort.OnClick();
        Assert.Equal(new (string, string)[] { ("abort", null) }, cyclingActs);
        Assert.Equal("Cycling to Interior Airlock", ByKey(Render(AirlockController.Render, "AirlockController", Data("pressurize", 3, "release", "closed", "closed")).Tree, "status").Text);

        // the inner door open: close it or cycle out; the door's "Open" in green
        var (_, inopen, inActs) = Render(AirlockController.Render, "AirlockController", Data("inopen", 101.3, "release", "open", "closed"));
        Assert.Equal(("Interior Airlock Open", "good"), (ByKey(inopen, "status").Text, ByKey(inopen, "pressure").Color));
        Assert.Equal(("Open", "good"), (ByKey(inopen, "interior").Text, ByKey(inopen, "interior").Color));
        List<UiNode> inButtons = inopen.Walk().Single(n => n.Kind == UiKind.Section).Buttons.Walk().Where(n => n.Kind == UiKind.Button).ToList();
        Assert.Equal(new[] { ("Close Interior Airlock", "lock"), ("Cycle to Exterior Airlock", "sync") }, inButtons.Select(b => (b.Text, b.Icon)));
        inButtons[0].OnClick();
        inButtons[1].OnClick();
        Assert.Equal(new (string, string)[] { ("cycleClosed", null), ("cycleExterior", null) }, inActs);

        // the outer door open: the warning icon in red; no sensor or doors: tg's dashes
        var (_, outopen, outActs) = Render(AirlockController.Render, "AirlockController", Data("outopen", 0, "siphon", "closed", "open"));
        Assert.Equal(("Exterior Airlock Open", "0 kPa "), (ByKey(outopen, "status").Text, ByKey(outopen, "pressure").Text));
        Assert.Equal(("exclamation-triangle", false, "bad"), (ByKey(outopen, "pressure-icon").Icon, ByKey(outopen, "pressure-icon").Spin, ByKey(outopen, "pressure-icon").Color));
        List<UiNode> outButtons = outopen.Walk().Single(n => n.Kind == UiKind.Section).Buttons.Walk().Where(n => n.Kind == UiKind.Button).ToList();
        Assert.Equal(new[] { "Close Exterior Airlock", "Cycle to Interior Airlock" }, outButtons.Select(b => b.Text));
        outButtons[1].OnClick();
        Assert.Equal(new (string, string)[] { ("cycleInterior", null) }, outActs);
        var (_, unlinked, _) = Render(AirlockController.Render, "AirlockController", Data("closed", "----", "----", "----", "----"));
        Assert.Equal(("---- kPa ", "----", "----"), (ByKey(unlinked, "pressure").Text, ByKey(unlinked, "pump").Text, ByKey(unlinked, "exterior").Text));
        Assert.Equal("Unknown", ByKey(Render(AirlockController.Render, "AirlockController", Data("weird", 1, "release", "closed", "closed")).Tree, "status").Text);
        Assert.NotNull(TguiInterfaces.Find("AirlockController"));
    }

    [Fact]
    public void TheAccessConsoleOffersEachDoorsOpenCycleOrCloseWithItsRef()
    {
        // both shut: open either
        var (context, tree, acts) = Render(AirlockButtonController.Render, "AirlockButtonController",
            "{\"interior_door\":\"[0x2001]\",\"exterior_door\":\"[0x2002]\",\"busy\":0,\"interior_door_closed\":1,\"exterior_door_closed\":1}");
        Assert.Equal((500.0, 130.0), (context.WindowWidth, context.WindowHeight));
        UiNode section = tree.Walk().Single(n => n.Kind == UiKind.Section);
        Assert.Equal(("Airlock Controller", "center"), (section.Title, section.TextAlign));
        UiNode inner = ByKey(tree, "interior"), outer = ByKey(tree, "exterior");
        Assert.Equal(("Open interior door", "lock-open", "green", false), (inner.Text, inner.Icon, inner.Color, inner.Disabled));
        Assert.Equal(1.5, outer.FontSize);
        Assert.Equal("Open exterior door", outer.Text);
        inner.OnClick();
        Assert.Equal(new (string, string)[] { ("open", "{\"requested_door\":\"[0x2001]\"}") }, acts);

        // the inner door open: close it, or cycle to the outer door
        var (_, open, openActs) = Render(AirlockButtonController.Render, "AirlockButtonController",
            "{\"interior_door\":\"[0x2001]\",\"exterior_door\":\"[0x2002]\",\"busy\":0,\"interior_door_closed\":0,\"exterior_door_closed\":1}");
        Assert.Equal(("Close interior door", "lock"), (ByKey(open, "interior").Text, ByKey(open, "interior").Icon));
        Assert.Equal(("Cycle to exterior door", "lock-open"), (ByKey(open, "exterior").Text, ByKey(open, "exterior").Icon));
        ByKey(open, "interior").OnClick();
        ByKey(open, "exterior").OnClick();
        Assert.Equal(new (string, string)[] { ("close", "{\"requested_door\":\"[0x2001]\"}"), ("open", "{\"requested_door\":\"[0x2002]\"}") }, openActs);

        // busy: both disabled; one door only: one button; none: tg's notice
        var (_, busy, _) = Render(AirlockButtonController.Render, "AirlockButtonController",
            "{\"interior_door\":\"[0x2001]\",\"exterior_door\":\"[0x2002]\",\"busy\":1,\"interior_door_closed\":1,\"exterior_door_closed\":1}");
        Assert.All(busy.Walk().Where(n => n.Kind == UiKind.Button), b => Assert.True(b.Disabled));
        var (_, single, _) = Render(AirlockButtonController.Render, "AirlockButtonController",
            "{\"interior_door\":null,\"exterior_door\":\"[0x2002]\",\"busy\":0,\"interior_door_closed\":null,\"exterior_door_closed\":1}");
        Assert.Equal("Open exterior door", Assert.Single(single.Walk(), n => n.Kind == UiKind.Button).Text);
        var (_, none, _) = Render(AirlockButtonController.Render, "AirlockButtonController", "{\"interior_door\":null,\"exterior_door\":null,\"busy\":0}");
        UiNode notice = ByKey(none, "no-doors");
        Assert.Equal((UiKind.NoticeBox, "danger", "No doors detected"), (notice.Kind, notice.Color, notice.Text));
        Assert.NotNull(TguiInterfaces.Find("AirlockButtonController"));
    }
}
