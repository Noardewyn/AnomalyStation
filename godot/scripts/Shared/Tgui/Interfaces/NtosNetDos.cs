using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosNetDos.jsx (gaps S4c, D327): the DoS Traffic Generator,
// the relays to target and EXECUTE, then the flood's speed and lines of ones and zeroes as dense as the relay's overload; an error
// with its Reset. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosNetDos
{
    // tg: NtosNetDos.jsx:11
    public static UiNode Render(TguiContext c) => NtosWindow.Render(c, 400, 250, null, Content(c));

    // tg: NtosNetDos.jsx:21
    public static UiNode Content(TguiContext c)
    {
        UiValue data = c.Data;
        string error = data.Str("error");
        if (!string.IsNullOrEmpty(error))
            return new UiNode(UiKind.Box).With(
                new UiNode(UiKind.NoticeBox) { Key = "error", Text = error },
                new UiNode(UiKind.Button) { Key = "reset", Fluid = true, Text = "Reset", TextAlign = "center", OnClick = () => c.Act("PRG_reset") });

        double overload = data.Num("overload");
        double capacity = data.Num("capacity");
        string Generate10String(int length)
        {
            var outString = new System.Text.StringBuilder();
            double factor = overload / capacity;
            while (outString.Length < length)
                outString.Append(Random.Shared.NextDouble() > factor ? '0' : '1');
            return outString.ToString();
        }
        const int lineLength = 45;

        if (data.Truthy("target"))
        {
            var flood = new UiNode(UiKind.Section) { Key = "flood", TextAlign = "center" }.With(
                new UiNode(UiKind.Box) { Key = "speed", Text = "CURRENT SPEED: " + TguiFormat.JsNumber(data.Num("speed")) + " GQ/s" });
            for (int i = 0; i < 5; i++)
                flood.With(new UiNode(UiKind.Box) { Key = "line:" + i, Text = Generate10String(lineLength) });
            return flood;
        }

        string focus = data.Get("focus") is { IsNull: false } f ? f.Text ?? TguiFormat.JsNumber(f.Number) : null;
        var targets = new UiNode(UiKind.LabeledItem) { Key = "target", Label = "Target" };
        foreach (UiValue relay in data.Arr("relays"))
        {
            UiValue id = relay.Get("id");
            string idText = id?.Text ?? TguiFormat.JsNumber(id?.Number ?? 0);
            targets.With(new UiNode(UiKind.Button)
            {
                Key = "relay:" + idText,
                Text = idText,
                Selected = focus == idText,
                OnClick = () => c.Act("PRG_target_relay", ("targid", id)),
            });
        }
        return new UiNode(UiKind.Section).With(
            new UiNode(UiKind.LabeledList).With(targets),
            new UiNode(UiKind.Button) { Key = "execute", Fluid = true, Bold = true, Text = "EXECUTE", Color = "bad", TextAlign = "center", Disabled = focus == null, OnClick = () => c.Act("PRG_execute") });
    }
}
