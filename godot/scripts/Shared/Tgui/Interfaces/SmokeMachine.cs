using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/SmokeMachine.tsx (gaps package F1). The window reads
// the smoke machine's ui_data (smoke_machine.dm:169: the tank, active, setting, maxSetting) and sends power, setting and purge.
// tgui-core's AnimatedNumber renders as its text, as the other interfaces do; the kit has no margin (the Range list's Box mt={1}).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class SmokeMachine
{
    // tg: SmokeMachine.tsx:22
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue tank = data.Get("tank");
        bool active = data.Truthy("active");
        double setting = data.Num("setting");
        double maxSetting = data.Num("maxSetting");
        double currentVolume = tank?.Num("currentVolume") ?? 0;
        double maxVolume = tank?.Num("maxVolume") ?? 0;
        IReadOnlyList<UiValue> contents = tank?.Get("contents") is { Kind: UiValueKind.Array } list ? list.Items : Array.Empty<UiValue>();

        // tg: SmokeMachine.tsx:27  <Window width={350} height={350}>
        return c.Window(350, 350, null,
            // tg: SmokeMachine.tsx:29
            new UiNode(UiKind.Section)
            {
                Key = "tank",
                Title = "Dispersal Tank",
                Buttons = new UiNode(UiKind.Button)
                {
                    Key = "power",
                    Icon = active ? "power-off" : "times",
                    Selected = active,
                    Text = active ? "On" : "Off",
                    OnClick = () => c.Act("power"),
                },
            }.With(
                // tg: SmokeMachine.tsx:41  <ProgressBar value={tank.currentVolume / tank.maxVolume} ranges={{ bad: [-Infinity, 0.3] }}>
                new UiNode(UiKind.ProgressBar)
                {
                    Key = "volume",
                    Value = maxVolume > 0 ? currentVolume / maxVolume : 0,
                    Text = TguiFormat.JsNumber(currentVolume) + " / " + TguiFormat.JsNumber(maxVolume),
                    Ranges = new[] { ("bad", double.NegativeInfinity, 0.3) },
                },
                // tg: SmokeMachine.tsx:50  <Box mt={1}><LabeledList><LabeledList.Item label="Range">
                new UiNode(UiKind.Box) { Key = "range" }.With(
                    new UiNode(UiKind.LabeledList).With(
                        new UiNode(UiKind.LabeledItem) { Label = "Range" }.With(
                            new[] { 1, 2, 3, 4, 5 }.Select(amount => new UiNode(UiKind.Button)
                            {
                                Key = "setting:" + amount,
                                Disabled = maxSetting < amount,
                                Icon = "plus",
                                Selected = setting == amount,
                                Text = TguiFormat.JsNumber(amount * 3),
                                OnClick = () => c.Act("setting", ("amount", amount)),
                            }).ToArray())))),
            // tg: SmokeMachine.tsx:67
            new UiNode(UiKind.Section)
            {
                Key = "contents",
                Title = "Contents",
                Buttons = new UiNode(UiKind.Button)
                {
                    Key = "purge",
                    Icon = "trash",
                    Text = "Purge",
                    OnClick = () => c.Act("purge"),
                },
            }.With(
                // tg: SmokeMachine.tsx:78  <Box key={chemical.name} color="label"><AnimatedNumber value={chemical.volume} /> units of{' '}{chemical.name}</Box>
                contents.Select(chemical => new UiNode(UiKind.Box)
                {
                    Key = "chemical:" + chemical.Str("name"),
                    Color = "label",
                    Text = TguiFormat.JsNumber(chemical.Num("volume")) + " units of " + chemical.Str("name"),
                }).ToArray()));
    }
}
