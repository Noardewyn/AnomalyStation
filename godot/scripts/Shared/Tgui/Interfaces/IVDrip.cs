using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/IVDrip.tsx (gaps package G1, D291). The window reads tg's
// ui_data of the IV drip (iv_drip.dm:95) and its static data (:89) and sends tg's acts: changeRate, changeMode, eject and detach.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
//
// Not drawn: the text shadow of the container bar's label (a CSS text-shadow).

public static class IVDrip
{
    // tg: IVDrip.tsx:42
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool hasContainer = data.Truthy("hasContainer");
        bool canRemoveContainer = data.Truthy("canRemoveContainer");
        bool mode = data.Truthy("mode");
        bool canDraw = data.Truthy("canDraw");
        bool hasInternalStorage = data.Truthy("hasInternalStorage");
        double transferRate = data.Num("transferRate");
        double transferStep = data.Num("transferStep");
        double maxTransferRate = data.Num("maxTransferRate");
        double minTransferRate = data.Num("minTransferRate");
        bool hasObjectAttached = data.Truthy("hasObjectAttached");

        // tg: IVDrip.tsx:62  <Window width={400} height={220}><Window.Content><Section fill><LabeledList>
        return c.Window(400, 220, null,
            new UiNode(UiKind.Section) { Fill = true }.With(
                new UiNode(UiKind.LabeledList).With(
                    FlowRate(c, transferRate, transferStep, minTransferRate, maxTransferRate),
                    Direction(c, mode, canDraw, hasInternalStorage),
                    Container(c, data, hasContainer || hasInternalStorage, hasInternalStorage, canRemoveContainer),
                    AttachedObject(c, data, hasObjectAttached))));
    }

    // tg: IVDrip.tsx:66  the flow rate: the slider and the two buttons that set the minimum and the maximum
    private static UiNode FlowRate(TguiContext c, double transferRate, double transferStep, double min, double max) =>
        new UiNode(UiKind.LabeledItem)
        {
            Key = "rate",
            Label = "Flow Rate",
            Buttons = new UiNode(UiKind.Fragment).With( // tg: <Box> of two inline buttons
                new UiNode(UiKind.Button)
                {
                    Key = "rate-min",
                    Width = 4,
                    Align = "center",
                    Icon = "angles-left",
                    OnClick = () => c.Act("changeRate", ("rate", min)),
                },
                new UiNode(UiKind.Button)
                {
                    Key = "rate-max",
                    Width = 4,
                    Align = "center",
                    Icon = "angles-right",
                    OnClick = () => c.Act("changeRate", ("rate", max)),
                }),
        }.With(
            new UiNode(UiKind.Slider)
            {
                Key = "rate-slider",
                Step = transferStep,
                StepPixelSize = 8,
                Value = transferRate,
                MinValue = min,
                MaxValue = max,
                Unit = "units/sec.",
                Format = value => TguiFormat.JsNumber(value) + " units/sec.",
                OnChange = value => c.Act("changeRate", ("rate", value)),
            });

    // tg: IVDrip.tsx:98  the direction: injecting or draining, a button that flips it
    private static UiNode Direction(TguiContext c, bool mode, bool canDraw, bool hasInternalStorage) =>
        new UiNode(UiKind.LabeledItem)
        {
            Key = "direction",
            Label = "Direction",
            Buttons = new UiNode(UiKind.Button)
            {
                Key = "mode",
                Width = 8,
                Align = "center",
                Disabled = !canDraw,
                Color = !mode ? "bad" : null,
                Text = mode ? "Injecting" : "Draining",
                Icon = mode ? "syringe" : "droplet",
                OnClick = () => c.Act("changeMode"),
            },
        }.With(
            // the item's colour (tg: color={!mode ? 'bad' : ''}) is the content's
            new UiNode(UiKind.Box)
            {
                Key = "mode-text",
                Color = !mode ? "bad" : null,
                Text = mode
                    ? hasInternalStorage ? "Reagents from network" : "Reagents from container"
                    : "Blood into container",
            });

    // tg: IVDrip.tsx:121  the container: the bar of its volume (coloured by its reagents) and the eject button, else a notice
    private static UiNode Container(TguiContext c, UiValue data, bool shown, bool hasInternalStorage, bool canRemoveContainer)
    {
        if (!shown)
        {
            return new UiNode(UiKind.LabeledItem) { Key = "container", Label = "Container" }.With(
                new UiNode(UiKind.NoticeBox)
                {
                    Key = "no-container",
                    Text = "No container attached.",
                    Tooltip = "Click the drip with a container in hand to attach.",
                });
        }

        double current = data.Num("containerCurrentVolume");
        double maximum = data.Num("containerMaxVolume");
        return new UiNode(UiKind.LabeledItem)
        {
            Key = "container",
            Label = "Container",
            Buttons = !hasInternalStorage && canRemoveContainer
                ? new UiNode(UiKind.Button)
                {
                    Key = "eject",
                    Width = 8,
                    Align = "center",
                    Icon = "eject",
                    Text = "Eject",
                    OnClick = () => c.Act("eject"),
                }
                : null,
        }.With(
            new UiNode(UiKind.ProgressBar)
            {
                Key = "volume",
                Value = current,
                MinValue = 0,
                MaxValue = maximum,
                Color = data.Field("containerReagentColor") is { Kind: UiValueKind.String } color ? color.Text : null,
                Text = TguiFormat.JsNumber(current) + " of " + TguiFormat.JsNumber(maximum) + " units",
            });
    }

    // tg: IVDrip.tsx:153  the object the drip is attached to, with the disconnect button, else a notice
    private static UiNode AttachedObject(TguiContext c, UiValue data, bool hasObjectAttached)
    {
        if (!hasObjectAttached)
        {
            return new UiNode(UiKind.LabeledItem) { Key = "object", Label = "Object" }.With(
                new UiNode(UiKind.NoticeBox)
                {
                    Key = "no-object",
                    Text = "No object attached.",
                    Tooltip = "Drag the cursor from the drip and drop it on an object to connect.",
                });
        }

        return new UiNode(UiKind.LabeledItem)
        {
            Key = "object",
            Label = "Object",
            Buttons = new UiNode(UiKind.Button)
            {
                Key = "detach",
                Width = 8,
                Align = "center",
                Icon = "ban",
                Text = "Disconnect",
                OnClick = () => c.Act("detach"),
            },
        }.With(
            new UiNode(UiKind.Box) { Key = "object-name", Text = data.Str("objectName"), Wrap = true });
    }
}
