using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// M8z slice B: the client's StripMenu window (tg's StripMenu.tsx) without the engine; the payloads follow strippable.dm:349-398. The
/// grid is 5 rows of 6 spots (426 x 387), a slot is a button that sends tg's "use", an item is its own icon and state with its
/// alternate actions as small buttons that send "alt", a hidden slot shows the eye, a completely obscured one the ban.
/// </summary>
public class StripMenuTests
{
    // a downed man: a hat with nothing else, the shoes (tied, with the knot action), a jumpsuit (three actions), a pocket's item (hidden),
    // a suit hiding the gloves (completely obscured), an inaccessible mask, and the right hand being used
    private const string Payload = "{\"name\":\"Bob\",\"items\":{"
        + "\"head\":{\"obscured\":0,\"icon\":\"icons/obj/clothing/hats.dmi\",\"icon_state\":\"tophat\",\"name\":\"top hat\",\"alternate\":[]},"
        + "\"eyes\":{\"obscured\":0},"
        + "\"jumpsuit\":{\"obscured\":0,\"icon\":\"icons/obj/clothing/under/color.dmi\",\"icon_state\":\"grey\",\"name\":\"grey jumpsuit\",\"alternate\":[\"adjust_sensor\",\"adjust_jumpsuit\",\"strip_accessory\"]},"
        + "\"shoes\":{\"obscured\":0,\"icon\":\"icons/obj/clothing/shoes.dmi\",\"icon_state\":\"black\",\"name\":\"black shoes\",\"alternate\":[\"untie\"]},"
        + "\"gloves\":{\"obscured\":1},"
        + "\"mask\":{\"obscured\":3,\"icon\":\"icons/obj/clothing/masks.dmi\",\"icon_state\":\"breath\",\"name\":\"breath mask\",\"alternate\":[\"knot\"]},"
        + "\"left_pocket\":{\"obscured\":2},"
        + "\"right_hand\":{\"obscured\":0,\"interacting\":1}}}";

    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, "StripMenu", "", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, StripMenu.Render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    [Fact]
    public void TheWindowIsAGridOfFiveRowsOfSixSpotsNamedForTheMan()
    {
        var (context, tree, _) = Render(Payload);
        Assert.Equal((426.0 + 4, 387.0 + 25, "Stripping Bob"), (context.WindowWidth, context.WindowHeight, context.WindowTitle));
        Assert.Equal(5, tree.Walk().Count(n => n.Key != null && n.Key.StartsWith("row", StringComparison.Ordinal)));
        Assert.Equal(30, tree.Walk().Count(n => n.Kind == UiKind.StackItem && n.Width as string == "64px"));
        Assert.Equal(8, tree.Walk().Count(n => n.Key != null && n.Key.StartsWith("slot:", StringComparison.Ordinal)));
    }

    [Fact]
    public void AnItemIsItsOwnIconAndStateWithTheNameAsTheTooltip()
    {
        var (_, tree, acts) = Render(Payload);
        UiNode hat = ByKey(tree, "slot:head");
        Assert.Equal("top hat", hat.Tooltip);
        UiNode picture = hat.Children.Single(n => n.Key == "item");
        Assert.Equal(("icons/obj/clothing/hats.dmi", "tophat", 2.0), (picture.DmIcon, picture.DmIconState, picture.Size)); // 64 px
        Assert.DoesNotContain(hat.Children, n => n.Key == "placeholder"); // the slot's own picture shows only while it is empty
        hat.OnClick();
        Assert.Equal(("use", "{\"key\":\"head\"}"), acts.Single());
    }

    [Fact]
    public void AnEmptySlotShowsTgsPlaceholderPictureAtSeventyPercent()
    {
        var (_, tree, _) = Render(Payload);
        UiNode eyes = ByKey(tree, "slot:eyes");
        Assert.Null(eyes.Tooltip); // tg sends {obscured: 0} for an empty slot, not null: no tooltip
        UiNode placeholder = eyes.Children.Single();
        Assert.Equal(("icons/ui/inventory/glasses.png", 0.7, 2.0), (placeholder.DmIcon, placeholder.Opacity, placeholder.Size));
    }

    [Fact]
    public void AnItemsAlternateActionsAreSmallButtonsInTheCornersThatSendAlt()
    {
        var (_, tree, acts) = Render(Payload);
        UiNode[] alt = tree.Walk().Where(n => n.Key != null && n.Key.StartsWith("alt:jumpsuit:", StringComparison.Ordinal)).ToArray();
        Assert.Equal(new[] { "bottom-left", "bottom-right", "top-left" }, alt.Select(a => a.Anchor));
        Assert.Equal(new[] { "Adjust sensors", "Adjust jumpsuit", "Strip accessory" }, alt.Select(a => a.Tooltip));
        Assert.All(alt, a => Assert.Equal(("20px", "20px"), (a.Width as string, a.Height as string)));
        alt[0].OnClick();
        Assert.Equal(("alt", "{\"key\":\"jumpsuit\",\"alternate_action\":\"adjust_sensor\"}"), acts.Single());
        // an inaccessible item's alternate actions are disabled and dimmed
        UiNode knot = ByKey(tree, "alt:mask:knot");
        Assert.True(knot.Disabled);
        Assert.Equal(0.7, knot.Opacity);
        Assert.False(ByKey(tree, "alt:shoes:untie").Disabled);
    }

    [Fact]
    public void AHiddenSlotShowsTheEyeACompletelyObscuredOneTheBanAndTheUsedSlotIsHighlighted()
    {
        var (_, tree, _) = Render(Payload);
        UiNode pocket = ByKey(tree, "slot:left_pocket");
        Assert.Equal(("obscured left pocket", "eye-slash"), (pocket.Tooltip, pocket.Children.Single(n => n.Kind == UiKind.Icon).Icon));
        UiNode gloves = ByKey(tree, "slot:gloves");
        Assert.Equal(("obscured gloves", "ban"), (gloves.Tooltip, gloves.Children.Single(n => n.Kind == UiKind.Icon).Icon));
        UiNode hand = ByKey(tree, "slot:right_hand");
        Assert.Equal("#845d15", hand.Background); // hsl(39, 73%, 30%)
        Assert.Null(ByKey(tree, "slot:head").Background);
        // the hands carry their letter in a corner
        Assert.Equal(("R", "top-left"), (hand.Children.Single(n => n.Key == "corner").Text, hand.Children.Single(n => n.Key == "corner").Anchor));
    }

    [Fact]
    public void AnUnknownSlotKeyIsIgnoredAndTheWindowIsRegistered()
    {
        var (_, tree, _) = Render("{\"name\":\"a thing\",\"items\":{\"nonsense\":{\"obscured\":0}}}");
        Assert.DoesNotContain(tree.Walk(), n => n.Key != null && n.Key.StartsWith("slot:", StringComparison.Ordinal));
        Assert.NotNull(TguiInterfaces.Find("StripMenu"));
    }
}
