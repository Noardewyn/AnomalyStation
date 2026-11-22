using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces.Common;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/common/BeakerDisplay.tsx (M8x slice B, the Cryo window).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
//
// tgui-core's AnimatedNumber renders as its text (a number is drawn at once, as the other interfaces do); Box's margin props (mr) and
// Button's style opacity are not in the kit: the volume text keeps a space after it, the Insert button's missing-container look is its
// tooltip alone.
// DEVIATION(DV120): see docs/ai/DEVIATIONS.md.

/// <summary>tg's <c>BeakerReagent</c> and <c>Beaker</c> types are the payload's <c>{name, volume}</c> and <c>{maxVolume, pH, currentVolume, contents}</c>.</summary>
public static class BeakerDisplay
{
    // tg: BeakerDisplay.tsx:30  const beakerContents = replace_contents || beaker?.contents || []   (an empty array is truthy in JS)
    private static IReadOnlyList<UiValue> ContentsOf(UiValue beaker, UiValue replaceContents) =>
        replaceContents is { Kind: UiValueKind.Array } r ? r.Items
        : beaker?.Get("contents") is { Kind: UiValueKind.Array } c ? c.Items
        : Array.Empty<UiValue>();

    // tg: BeakerDisplay.tsx:81  {beakerContents.map((chemical) => <Box key={chemical.name} color="label"><AnimatedNumber initial={0} value={chemical.volume} /> units of{' '}{chemical.name}</Box>)}
    private static IEnumerable<UiNode> Chemicals(IReadOnlyList<UiValue> contents) =>
        contents.Select(chemical => new UiNode(UiKind.Box)
        {
            Key = "chemical:" + chemical.Str("name"),
            Color = "label",
            Text = TguiFormat.JsNumber(chemical.Num("volume")) + " units of " + chemical.Str("name"),
        });

    // tg: BeakerDisplay.tsx:27
    public static UiNode Display(TguiContext c, UiValue beaker, UiValue replaceContents = null, string titleLabel = null, bool showPH = false)
    {
        bool loaded = beaker?.Truthy ?? false;
        IReadOnlyList<UiValue> contents = ContentsOf(beaker, replaceContents);
        string heading = !string.IsNullOrEmpty(titleLabel) ? titleLabel
            : loaded ? TguiFormat.JsNumber(beaker.Num("currentVolume")) + "/" + TguiFormat.JsNumber(beaker.Num("maxVolume")) + " units"
            : "No beaker";
        // tg: BeakerDisplay.tsx:44  (!title_label && !beaker && 'N/A') || (beakerContents.length === 0 && 'Nothing')
        string note = string.IsNullOrEmpty(titleLabel) && !loaded ? "N/A" : contents.Count == 0 ? "Nothing" : "";

        // tg: BeakerDisplay.tsx:34
        return new UiNode(UiKind.LabeledList).With(
            new UiNode(UiKind.LabeledItem)
            {
                Label = "Beaker",
                Buttons = loaded
                    ? new UiNode(UiKind.Button) { Key = "eject", Icon = "eject", Text = "Eject", OnClick = () => c.Act("eject") }
                    : null,
            }.With(new UiNode(UiKind.Box) { Key = "heading", Text = heading }),
            // tg: BeakerDisplay.tsx:51
            new UiNode(UiKind.LabeledItem) { Label = "Contents" }.With(
                new UiNode(UiKind.Box) { Key = "note", Color = "label", Text = note }).With(
                Chemicals(contents)).With(
                contents.Count > 0 && showPH ? new UiNode(UiKind.Box) { Key = "ph", Text = "pH:" + TguiFormat.JsNumber(beaker.Num("pH")) } : null));
    }

    // tg: BeakerDisplay.tsx:68
    public static UiNode SectionDisplay(TguiContext c, UiValue beaker, UiValue replaceContents = null, string titleLabel = null, bool showPH = false,
        bool showInsertButton = false, bool hasBeakerInHand = false)
    {
        bool loaded = beaker?.Truthy ?? false;
        IReadOnlyList<UiValue> contents = ContentsOf(beaker, replaceContents);

        // tg: BeakerDisplay.tsx:90  buttons={isBeakerLoaded ? <>...</> : showInsertButton && <Button insert>}
        UiNode buttons = loaded
            ? new UiNode(UiKind.Fragment).With(
                new UiNode(UiKind.Box) { Key = "volume", Color = "label", Text = TguiFormat.JsNumber(beaker.Num("currentVolume")) + " / " + TguiFormat.JsNumber(beaker.Num("maxVolume")) + " units " },
                new UiNode(UiKind.Button) { Key = "eject", Icon = "eject", Text = "Eject", OnClick = () => c.Act("eject") })
            : showInsertButton
                ? new UiNode(UiKind.Button)
                {
                    Key = "insert",
                    Icon = "eject",
                    Text = "Insert",
                    Tooltip = hasBeakerInHand ? null : "You need to hold a container in your hand",
                    OnClick = () => c.Act("insert"),
                }
                : null;

        // tg: BeakerDisplay.tsx:112  (!beaker && 'No beaker loaded') || (beakerContents.length === 0 && 'Nothing')
        string note = !loaded ? "No beaker loaded" : contents.Count == 0 ? "Nothing" : "";

        // tg: BeakerDisplay.tsx:89
        return new UiNode(UiKind.Section) { Title = !string.IsNullOrEmpty(titleLabel) ? titleLabel : "Beaker", Buttons = buttons }.With(
            new UiNode(UiKind.Box) { Key = "note", Color = "label", Text = note }).With(
            Chemicals(contents)).With(
            contents.Count > 0 && showPH ? new UiNode(UiKind.Box) { Key = "ph", Text = "pH:" + TguiFormat.JsNumber(beaker.Num("pH")) } : null);
    }
}
