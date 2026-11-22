using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps package G1: the client's IVDrip (IVDrip.tsx), Safe (Safe.jsx) and LootPanel (LootPanel/) windows without the engine. Each reads tg's
/// ui_data of its mechanic (the drip's mode, rate, container and patient; the safe's dial, flags and contents; the loot panel's items) the
/// way the TSX does and sends tg's acts (changeRate, changeMode, eject, detach; turnright, turnleft, open, retrieve; grab).
/// </summary>
public class GapsWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts, TguiLocalState Local) Render(TguiInterface view, string name, string json, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        local ??= new TguiLocalState();
        var context = new TguiContext(1, name, name, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local);
        return (context, view(context), acts, local);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    // ---- the IV drip ----

    private const string Drip = "{\"hasInternalStorage\":0,\"hasContainer\":1,\"canRemoveContainer\":1,\"mode\":1,\"canDraw\":1,\"transferStep\":0.01,"
        + "\"maxTransferRate\":5,\"minTransferRate\":0,\"transferRate\":2.5,\"hasObjectAttached\":1,\"objectName\":\"Bob\","
        + "\"containerReagentColor\":\"#AA1010\",\"containerCurrentVolume\":150,\"containerMaxVolume\":200}";

    [Fact]
    public void ADripWithAContainerAndAPatientShowsTheRateTheDirectionTheVolumeAndTheObject()
    {
        var (context, tree, acts, _) = Render(IVDrip.Render, "IVDrip", Drip);
        Assert.Equal((400.0, 220.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(new[] { "Flow Rate", "Direction", "Container", "Object" }, tree.Walk().Where(n => n.Kind == UiKind.LabeledItem).Select(n => n.Label));

        // the rate: a slider over the limits (units a second) and the two end buttons
        UiNode slider = ByKey(tree, "rate-slider");
        Assert.Equal((2.5, 0.0, 5.0, 0.01, "2.5 units/sec."), (slider.Value, slider.MinValue, slider.MaxValue, slider.Step, slider.Format(2.5)));
        slider.OnChange(3.25);
        ByKey(tree, "rate-min").OnClick();
        ByKey(tree, "rate-max").OnClick();
        Assert.Equal(("angles-left", "angles-right"), (ByKey(tree, "rate-min").Icon, ByKey(tree, "rate-max").Icon));

        // the direction: injecting, from the container
        UiNode mode = ByKey(tree, "mode");
        Assert.Equal(("Injecting", "syringe", false, null), (mode.Text, mode.Icon, mode.Disabled, mode.Color));
        Assert.Equal(("Reagents from container", null), (ByKey(tree, "mode-text").Text, ByKey(tree, "mode-text").Color));
        mode.OnClick();

        // the container: the volume bar coloured by the reagents and the eject button
        UiNode bar = ByKey(tree, "volume");
        Assert.Equal((150.0, 0.0, 200.0, "#AA1010", "150 of 200 units"), (bar.Value, bar.MinValue, bar.MaxValue, bar.Color, bar.Text));
        ByKey(tree, "eject").OnClick();

        // the object: its name and the disconnect button
        Assert.Equal("Bob", ByKey(tree, "object-name").Text);
        ByKey(tree, "detach").OnClick();

        Assert.Equal(new (string, string)[]
        {
            ("changeRate", "{\"rate\":3.25}"), ("changeRate", "{\"rate\":0}"), ("changeRate", "{\"rate\":5}"),
            ("changeMode", null), ("eject", null), ("detach", null),
        }, acts);
    }

    [Fact]
    public void ADrippingDripDrainsAndABareOneAsksForAContainerAndAnObject()
    {
        var (_, drain, _, _) = Render(IVDrip.Render, "IVDrip", "{\"hasInternalStorage\":0,\"hasContainer\":1,\"canRemoveContainer\":1,\"mode\":0,\"canDraw\":0,\"transferRate\":5,"
            + "\"transferStep\":0.01,\"maxTransferRate\":5,\"minTransferRate\":0,\"hasObjectAttached\":0,\"containerReagentColor\":\"#ffffff\",\"containerCurrentVolume\":0,\"containerMaxVolume\":200}");
        UiNode mode = ByKey(drain, "mode");
        Assert.Equal(("Draining", "droplet", true, "bad"), (mode.Text, mode.Icon, mode.Disabled, mode.Color)); // no patient to draw from: disabled
        Assert.Equal(("Blood into container", "bad"), (ByKey(drain, "mode-text").Text, ByKey(drain, "mode-text").Color));
        UiNode none = ByKey(drain, "no-object");
        Assert.Equal(("No object attached.", "Drag the cursor from the drip and drop it on an object to connect."), (none.Text, none.Tooltip));
        Assert.DoesNotContain(drain.Walk(), n => n.Key == "detach");

        var (_, bare, _, _) = Render(IVDrip.Render, "IVDrip", "{\"hasInternalStorage\":0,\"hasContainer\":0,\"canRemoveContainer\":1,\"mode\":1,\"canDraw\":1,\"transferRate\":5,"
            + "\"transferStep\":0.01,\"maxTransferRate\":5,\"minTransferRate\":0,\"hasObjectAttached\":0}");
        UiNode empty = ByKey(bare, "no-container");
        Assert.Equal(("No container attached.", "Click the drip with a container in hand to attach."), (empty.Text, empty.Tooltip));
        Assert.DoesNotContain(bare.Walk(), n => n.Key == "volume" || n.Key == "eject");
    }

    [Fact]
    public void ASalineDripTakesItsReagentsFromTheNetworkAndHasNoEject()
    {
        var (_, tree, _, _) = Render(IVDrip.Render, "IVDrip", "{\"hasInternalStorage\":1,\"hasContainer\":0,\"canRemoveContainer\":0,\"mode\":1,\"canDraw\":0,\"transferRate\":5,"
            + "\"transferStep\":0.01,\"maxTransferRate\":5,\"minTransferRate\":0,\"hasObjectAttached\":0,\"containerReagentColor\":\"#c8c8dc\",\"containerCurrentVolume\":500,\"containerMaxVolume\":500}");
        Assert.Equal("Reagents from network", ByKey(tree, "mode-text").Text);
        Assert.Equal("500 of 500 units", ByKey(tree, "volume").Text);
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "eject");
    }

    // ---- the safe ----

    private const string Locked = "{\"dial\":25,\"open\":0,\"locked\":1,\"broken\":0}";

    [Fact]
    public void AShutSafeShowsTheDialButtonsAndTheHelpAndTurnsTheDialsPictureByTheNumber()
    {
        var (context, tree, acts, _) = Render(Safe.Render, "Safe", Locked);
        Assert.Equal((625.0, 800.0, "ntos"), (context.WindowWidth, context.WindowHeight, context.WindowTheme));

        // the dial: tg's picture turned 3.6 degrees a number, counter-clockwise
        UiNode dial = ByKey(tree, "dial-picture");
        Assert.Equal(("icons/ui/safe/safe_dial.png", -90.0), (dial.DmIcon, dial.Rotation));
        Assert.Equal("25", ByKey(tree, "dial-number").Text);

        // Open is disabled while the lock is engaged; every dial button works (tg disables the right ones only for an unlocked safe)
        UiNode open = ByKey(tree, "open");
        Assert.Equal(("Open", "lock-open", true), (open.Text, open.Icon, open.Disabled));
        Assert.Equal(new[] { "Left 50", "Left 10", "Left 1" }, new[] { "left:50", "left:10", "left:1" }.Select(k => ByKey(tree, k).Text));
        Assert.Equal(new[] { "Right 1", "Right 10", "Right 50" }, new[] { "right:1", "right:10", "right:50" }.Select(k => ByKey(tree, k).Text));
        Assert.All(new[] { "left:50", "left:10", "left:1", "right:1", "right:10", "right:50" }, k => Assert.False(ByKey(tree, k).Disabled));
        Assert.Equal(("arrow-left", "arrow-right"), (ByKey(tree, "left:10").Icon, ByKey(tree, "right:10").Icon));

        // tg's quirk: the "Left" buttons send turnright (the number goes down) and the "Right" ones turnleft
        ByKey(tree, "left:50").OnClick();
        ByKey(tree, "left:1").OnClick();
        Assert.Equal(new (string, string)[] { ("turnright", "{\"num\":50}"), ("turnright", "{\"num\":1}") }, acts);
        Assert.Equal("Safe opening instructions (because you all keep forgetting)", ByKey(tree, "help").Title);
    }

    [Fact]
    public void AnUnlockedSafeOpensAndTgDisablesItsRightButtons()
    {
        var (_, tree, acts, _) = Render(Safe.Render, "Safe", "{\"dial\":60,\"open\":0,\"locked\":0,\"broken\":0}");
        Assert.False(ByKey(tree, "open").Disabled);
        Assert.All(new[] { "right:1", "right:10", "right:50" }, k => Assert.True(ByKey(tree, k).Disabled)); // Safe.jsx: right && !locked
        Assert.All(new[] { "left:1", "left:10", "left:50" }, k => Assert.False(ByKey(tree, k).Disabled));
        ByKey(tree, "open").OnClick();
        Assert.Equal(new (string, string)[] { ("open", null) }, acts);

        // a locked safe turns up ('turnleft') with its right buttons
        var (_, shut, shutActs, _) = Render(Safe.Render, "Safe", Locked);
        ByKey(shut, "right:10").OnClick();
        Assert.Equal(new (string, string)[] { ("turnleft", "{\"num\":10}") }, shutActs);
    }

    [Fact]
    public void ABrokenSafeOpensButItsDialDoesNotTurn()
    {
        var (_, tree, _, _) = Render(Safe.Render, "Safe", "{\"dial\":0,\"open\":0,\"locked\":1,\"broken\":1}");
        Assert.False(ByKey(tree, "open").Disabled);
        Assert.All(new[] { "left:50", "left:10", "left:1", "right:1", "right:10", "right:50" }, k => Assert.True(ByKey(tree, k).Disabled));
    }

    [Fact]
    public void AnOpenSafeListsItsContentsInPlaceOfTheDialAndHandsAnItemOver()
    {
        var (_, tree, acts, _) = Render(Safe.Render, "Safe", "{\"dial\":60,\"open\":1,\"locked\":0,\"broken\":0,\"contents\":["
            + "{\"name\":\"pocket crowbar\",\"sprite\":\"crowbar\",\"icon\":\"icons/obj/tools.dmi\"},{\"name\":\"wrench\",\"sprite\":\"wrench\",\"icon\":\"icons/obj/tools.dmi\"}]}");
        UiNode close = ByKey(tree, "open");
        Assert.Equal(("Close", "lock", false), (close.Text, close.Icon, close.Disabled));
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "dial-picture" || n.Key == "help");
        Assert.All(new[] { "left:50", "right:50" }, k => Assert.True(ByKey(tree, k).Disabled));

        UiNode second = ByKey(tree, "item:2");
        Assert.Equal((UiKind.ImageButton, "icons/obj/tools.dmi", "wrench", "wrench"), (second.Kind, second.DmIcon, second.DmIconState, second.AllText()));
        Assert.Equal("pocket crowbar", ByKey(tree, "item:1").AllText());
        second.OnClick();
        ByKey(tree, "item:1").OnClick();
        Assert.Equal(new (string, string)[] { ("retrieve", "{\"index\":2}"), ("retrieve", "{\"index\":1}") }, acts); // the place in tg's contents, counted from 1
    }

    // ---- the loot panel ----

    private static string Item(string name, string path, string reference, string icon = "icons/obj/tools.dmi", string state = "crowbar")
        => "{\"name\":\"" + name + "\",\"path\":" + (path == null ? "null" : "\"" + path + "\"") + ",\"ref\":\"" + reference + "\",\"icon\":" + (icon == null ? "null" : "\"" + icon + "\"")
            + ",\"icon_state\":" + (state == null ? "null" : "\"" + state + "\"") + "}";

    private static string Loot(params string[] items) => "{\"contents\":[" + string.Join(",", items) + "],\"searching\":0}";

    private static readonly string[] ThreeThings =
    {
        Item("floor", null, "[0x1]", "icons/turf/floors.dmi", "floor"),
        Item("pocket crowbar", "/obj/item/crowbar", "[0x2]"),
        Item("pocket crowbar", "/obj/item/crowbar", "[0x3]"),
        Item("wrench", "/obj/item/wrench", "[0x4]", state: "wrench"),
    };

    [Fact]
    public void ThePanelGroupsThingsOfOneTypeAndNameAndCountsThem()
    {
        var (context, tree, _, _) = Render(LootPanel.Render, "LootPanel", Loot(ThreeThings));
        // the turf (no path) stays its own entry; the two crowbars are one with an amount
        List<UiNode> boxes = tree.Walk().Where(n => n.Key?.StartsWith("box:") == true).ToList();
        Assert.Equal(3, boxes.Count);
        Assert.Equal(new[] { "Floor", "Pocket crowbar", "Wrench" }, boxes.Select(b => ByKey(b, "name").Text));
        Assert.Equal(new[] { "", "x2", "" }, boxes.Select(b => ByKey(b, "amount").Text));
        Assert.Equal(300.0, context.WindowWidth);
        // 3 rows: the header (38) + 3 x 38, and the search row that stands where tg's title bar buttons are
        Assert.Equal(38 + 3 * 38 + 34.0, context.WindowHeight);
    }

    [Fact]
    public void TheGroupingToggleListsEveryThingAndTheSearchFiltersByName()
    {
        var local = new TguiLocalState();
        var (_, tree, _, _) = Render(LootPanel.Render, "LootPanel", Loot(ThreeThings), local);
        ByKey(tree, "grouping").OnClick();
        Assert.False(local.Get("grouping", true));

        var (context, ungrouped, _, _) = Render(LootPanel.Render, "LootPanel", Loot(ThreeThings), local);
        Assert.Equal(4, ungrouped.Walk().Count(n => n.Key?.StartsWith("box:") == true));
        Assert.Equal(38 + 4 * 38 + 34.0, context.WindowHeight);
        Assert.False(ByKey(ungrouped, "grouping").Selected);
        Assert.Equal("object-ungroup", ByKey(ungrouped, "grouping").Icon);

        ByKey(ungrouped, "search").OnInput("WREN");
        var (_, found, _, _) = Render(LootPanel.Render, "LootPanel", Loot(ThreeThings), local);
        UiNode only = Assert.Single(found.Walk(), n => n.Key?.StartsWith("box:") == true);
        Assert.Equal("Wrench", ByKey(only, "name").Text);
    }

    [Fact]
    public void TheWindowGrowsWithItsRowsBetweenOneAndTenAndScrollsBeyond()
    {
        var (one, _, _, _) = Render(LootPanel.Render, "LootPanel", Loot());
        Assert.Equal(76 + 34.0, one.WindowHeight); // the minimum: the header and one row
        string[] many = Enumerable.Range(1, 25).Select(i => Item("thing " + i, "/obj/item/thing" + i, "[0x" + i + "]")).ToArray();
        var (twenty, tree, _, _) = Render(LootPanel.Render, "LootPanel", Loot(many));
        Assert.Equal(38 + 10 * 38 + 34.0, twenty.WindowHeight);
        Assert.True(ByKey(tree, "contents").Scrollable);
    }

    [Fact]
    public void AClickGrabsWithTheKeysHeldAndARightClickGrabsAlone()
    {
        var (_, tree, acts, _) = Render(LootPanel.Render, "LootPanel", Loot(ThreeThings));
        UiNode wrench = tree.Walk().Single(n => n.Key?.StartsWith("box:") == true && ByKey(n, "name").Text == "Wrench");
        wrench.OnClickWith(new UiClick(false, false, false, false));
        wrench.OnClickWith(new UiClick(true, false, true, false));
        wrench.OnClickWith(new UiClick(false, false, false, true));
        Assert.Equal(new (string, string)[]
        {
            ("grab", "{\"alt\":false,\"ctrl\":false,\"ref\":\"[0x4]\",\"shift\":false}"),
            ("grab", "{\"alt\":true,\"ctrl\":false,\"ref\":\"[0x4]\",\"shift\":true}"),
            ("grab", "{\"right\":true,\"ref\":\"[0x4]\"}"),
        }, acts);
    }

    [Fact]
    public void TheIconIsASpinnerWhileItIsMadeADumpsterFireWhenItCannotBeAndTheItemsOwnPictureOtherwise()
    {
        var (_, tree, _, _) = Render(LootPanel.Render, "LootPanel", Loot(
            Item("mob", null, "[0x1]", null, null), Item("odd", "/obj/odd", "[0x2]", "n/a", null), Item("crowbar", "/obj/item/crowbar", "[0x3]"), Item("sheet", "/obj/item/sheet", "[0x4]", "icons/obj/sheet.dmi", null)));
        List<UiNode> icons = tree.Walk().Where(n => n.Key == "icon").ToList();
        Assert.Equal((UiKind.Icon, "spinner", true), (icons[0].Kind, icons[0].Icon, icons[0].Spin));
        Assert.Equal((UiKind.Icon, "dumpster-fire"), (icons[1].Kind, icons[1].Icon));
        Assert.Equal((UiKind.Box, "icons/obj/tools.dmi", "crowbar", "36px"), (icons[2].Kind, icons[2].DmIcon, icons[2].DmIconState, (string)icons[2].Width));
        Assert.Equal((UiKind.Box, "icons/obj/sheet.dmi", null), (icons[3].Kind, icons[3].DmIcon, icons[3].DmIconState)); // no state: tg's plain image
    }

    [Fact]
    public void ANamelessThingIsThreeQuestionMarks()
    {
        var (_, tree, _, _) = Render(LootPanel.Render, "LootPanel", Loot(Item("", "/obj/x", "[0x9]")));
        Assert.Equal("???", ByKey(tree, "name").Text);
    }

    [Fact]
    public void TheThreeWindowsAreRegisteredUnderTheirInterfaceNames()
    {
        Assert.Equal((TguiInterface)IVDrip.Render, TguiInterfaces.Find("IVDrip"));
        Assert.Equal((TguiInterface)Safe.Render, TguiInterfaces.Find("Safe"));
        Assert.Equal((TguiInterface)LootPanel.Render, TguiInterfaces.Find("LootPanel"));
    }
}
