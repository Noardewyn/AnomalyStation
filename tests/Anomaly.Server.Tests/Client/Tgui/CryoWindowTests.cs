using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Client.Shared.Tgui.Interfaces.Common;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// M8x slice B: the client's Cryo and BeakerDisplay windows (tg's Cryo.tsx and common/BeakerDisplay.tsx) without the engine; the
/// payloads follow cryo.dm:543-585. The window reads the keys the TSX reads and sends tg's acts. (The sleeper is not ported, D216.)
/// </summary>
public class CryoWindowTests
{
    // cryo.dm ui_data: a closed, running cell with an unconscious patient at 150 K and a cryoxadone beaker
    private const string RunningCell = "{\"T0C\":273.15,\"isOperating\":1,\"isOpen\":0,\"autoEject\":1,\"occupant\":{\"name\":\"Bob\",\"stat\":\"Unconscious\",\"bodyTemperature\":150.4,\"health\":45,\"maxHealth\":100,\"bruteLoss\":20,\"oxyLoss\":0,\"toxLoss\":0,\"fireLoss\":35.6},\"cellTemperature\":73.15,\"beaker\":{\"maxVolume\":60,\"currentVolume\":42.5,\"contents\":[{\"name\":\"Cryoxadone\",\"volume\":40},{\"name\":\"Water\",\"volume\":2.5}]}}";

    // ... open and empty, no beaker
    private const string EmptyCell = "{\"T0C\":273.15,\"isOperating\":0,\"isOpen\":1,\"autoEject\":0,\"occupant\":null,\"cellTemperature\":293.15,\"beaker\":null}";

    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(TguiInterface render, string name, string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, name, "machine", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private static UiNode Item(UiNode tree, string label) => tree.Walk().Single(n => n.Kind == UiKind.LabeledItem && n.Label == label);

    [Fact]
    public void TheCryoWindowShowsTheOccupantTheCellAndTheBeaker()
    {
        var (context, tree, acts) = Render(Cryo.Render, "Cryo", RunningCell);
        Assert.Equal((400.0, 550.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(new[] { "Occupant", "Cell", "Beaker" }, tree.Walk().Where(n => n.Kind == UiKind.Section).Select(s => s.Title));
        Assert.Equal("Bob", ByKey(tree, "name").Text);
        // the state is bad/good by tg's stat_to_color; the temperature is rounded and good while it can heal (below T0C)
        Assert.Equal(("Unconscious", "good"), (ByKey(tree, "stat").Text, ByKey(tree, "stat").Color));
        Assert.Equal(("150 K", "good"), (ByKey(tree, "body-temperature").Text, ByKey(tree, "body-temperature").Color));
        UiNode health = ByKey(tree, "health");
        Assert.Equal((0.45, "good", "45"), (health.Value, health.Color, health.Text));
        Assert.Equal(new[] { "Occupant", "State", "Temperature", "Health", "Brute", "Respiratory", "Toxin", "Burn", "Power", "Temperature", "Door" },
            tree.Walk().Where(n => n.Kind == UiKind.LabeledItem).Select(n => n.Label));
        Assert.Equal((0.2, "20"), (ByKey(tree, "bruteLoss").Value, ByKey(tree, "bruteLoss").Text));
        Assert.Equal((0.36, "36"), (ByKey(tree, "fireLoss").Value, ByKey(tree, "fireLoss").Text));
        // the cell: power is green and enabled while the door is closed, the temperature is rounded, door and autoeject say their state
        UiNode power = ByKey(tree, "power");
        Assert.Equal(("power-off", "On", "green", false), (power.Icon, power.Text, power.Color, power.Disabled));
        Assert.Equal("73 K", ByKey(tree, "cell-temperature").Text);
        Assert.Equal(("lock", "Closed"), (ByKey(tree, "door").Icon, ByKey(tree, "door").Text));
        Assert.Equal(("sign-out-alt", "Auto"), (ByKey(tree, "autoeject").Icon, ByKey(tree, "autoeject").Text));
        // the beaker section: the volume and the contents, and the eject button (no pH: showpH is false)
        UiNode beaker = tree.Walk().Single(n => n.Kind == UiKind.Section && n.Title == "Beaker");
        Assert.Equal("42.5 / 60 units ", beaker.Buttons.Children.Single(n => n.Key == "volume").Text);
        Assert.Equal(new[] { "40 units of Cryoxadone", "2.5 units of Water" }, beaker.Children.Where(n => n.Key?.StartsWith("chemical:") == true).Select(n => n.Text));
        Assert.DoesNotContain(beaker.Walk(), n => n.Key == "ph");

        power.OnClick();
        ByKey(tree, "door").OnClick();
        ByKey(tree, "autoeject").OnClick();
        ByKey(tree, "eject").OnClick();
        Assert.Equal(new (string, string)[] { ("power", null), ("door", null), ("autoeject", null), ("eject", null) }, acts);
    }

    [Fact]
    public void TheCryoWindowOfAnOpenEmptyCellHasNoOccupantRowsAndNoBeaker()
    {
        var (_, tree, _) = Render(Cryo.Render, "Cryo", EmptyCell);
        Assert.Equal("No Occupant", ByKey(tree, "name").Text);
        Assert.Equal(new[] { "Occupant", "Power", "Temperature", "Door" }, tree.Walk().Where(n => n.Kind == UiKind.LabeledItem).Select(n => n.Label));
        UiNode power = ByKey(tree, "power");
        Assert.Equal(("times", "Off", null, true), (power.Icon, power.Text, power.Color, power.Disabled)); // disabled while the door is open
        Assert.Equal(("unlock", "Open"), (ByKey(tree, "door").Icon, ByKey(tree, "door").Text));
        Assert.Equal(("sign-in-alt", "Manual"), (ByKey(tree, "autoeject").Icon, ByKey(tree, "autoeject").Text));
        Assert.Equal("293 K", ByKey(tree, "cell-temperature").Text);
        UiNode beaker = tree.Walk().Single(n => n.Kind == UiKind.Section && n.Title == "Beaker");
        Assert.Null(beaker.Buttons); // no beaker: no eject, and the insert button is off by default
        Assert.Equal("No beaker loaded", ByKey(beaker, "note").Text);
        // a warm patient is not healed by cryoxadone: the temperature is bad and the health bar is "average" at zero or less
        var (_, warm, _) = Render(Cryo.Render, "Cryo", RunningCell.Replace("\"bodyTemperature\":150.4", "\"bodyTemperature\":310").Replace("\"health\":45", "\"health\":-5").Replace("\"stat\":\"Unconscious\"", "\"stat\":\"Conscious\""));
        Assert.Equal("bad", ByKey(warm, "body-temperature").Color);
        Assert.Equal("average", ByKey(warm, "health").Color);
        Assert.Equal("bad", ByKey(warm, "stat").Color);
    }

    [Fact]
    public void TheBeakerDisplaysFollowTgsBothVariants()
    {
        var context = new TguiContext(1, "x", "x", 2, UiValue.FromJson("{}"), (a, p) => { }, new TguiLocalState());
        UiValue beaker = UiValue.FromJson("{\"maxVolume\":30,\"currentVolume\":10,\"pH\":6.5,\"contents\":[{\"name\":\"Water\",\"volume\":10}]}");
        // the list variant: the beaker row (with the eject button), the contents, and the pH when asked
        UiNode list = BeakerDisplay.Display(context, beaker, showPH: true);
        Assert.Equal("10/30 units", ByKey(list, "heading").Text);
        Assert.Equal("eject", Item(list, "Beaker").Buttons.Icon);
        Assert.Equal("10 units of Water", ByKey(list, "chemical:Water").Text);
        Assert.Equal("pH:6.5", ByKey(list, "ph").Text);
        Assert.Equal("", ByKey(list, "note").Text);
        // a title label replaces the volume; replaced contents replace the beaker's; no beaker is N/A and has no eject
        UiNode replaced = BeakerDisplay.Display(context, beaker, UiValue.FromJson("[]"), titleLabel: "Dispensing");
        Assert.Equal(("Dispensing", "Nothing"), (ByKey(replaced, "heading").Text, ByKey(replaced, "note").Text));
        UiNode none = BeakerDisplay.Display(context, null);
        Assert.Equal(("No beaker", "N/A"), (ByKey(none, "heading").Text, ByKey(none, "note").Text));
        Assert.Null(Item(none, "Beaker").Buttons);
        // the section variant: an empty beaker says Nothing; no beaker offers Insert when asked, with the hint when no container is held
        UiNode empty = BeakerDisplay.SectionDisplay(context, UiValue.FromJson("{\"maxVolume\":30,\"currentVolume\":0,\"contents\":[]}"));
        Assert.Equal("Nothing", ByKey(empty, "note").Text);
        UiNode insert = BeakerDisplay.SectionDisplay(context, null, showInsertButton: true);
        Assert.Equal(("Insert", "You need to hold a container in your hand"), (insert.Buttons.Text, insert.Buttons.Tooltip));
        Assert.Null(BeakerDisplay.SectionDisplay(context, null, showInsertButton: true, hasBeakerInHand: true).Buttons.Tooltip);
    }

    [Fact]
    public void TheCryoWindowIsRegisteredUnderItsInterfaceName()
    {
        Assert.Equal((TguiInterface)Cryo.Render, TguiInterfaces.Find("Cryo"));
    }
}
