using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ExosuitControlConsole.tsx (D321). tgui-core's
// AnimatedNumber renders as its text. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ExosuitControlConsole
{
    // tg: tgui/packages/tgui/interfaces/ExosuitControlConsole.tsx:30
    public static UiNode Render(TguiContext c)
    {
        var mechs = c.Data.Arr("mechs");
        var content = new UiNode(UiKind.Fragment);
        if (mechs.Count == 0)
            content.With(new UiNode(UiKind.NoticeBox) { Text = "No exosuits detected" });
        foreach (UiValue mech in mechs)
            content.With(Mech(c, mech));
        // tg: ExosuitControlConsole.tsx:34  <Window width={500} height={500}><Window.Content scrollable>
        return c.Window(500, 500, null, new UiNode(UiKind.Section) { Key = "content", Fill = true, Scrollable = true }.With(content));
    }

    // tg: ExosuitControlConsole.tsx:37
    private static UiNode Mech(TguiContext c, UiValue mech)
    {
        string trackerRef = mech.Str("tracker_ref");
        string[] pilots = mech.Arr("pilot").Select(p => p.AsStr()).ToArray();
        bool recharging = mech.Truthy("emp_recharging");
        double integrity = mech.Num("integrity");
        UiValue charge = mech.Field("charge");
        UiValue airtank = mech.Field("airtank");
        UiValue cargo = mech.Field("cargo_space");

        var list = new UiNode(UiKind.LabeledList).With(
            new UiNode(UiKind.LabeledItem) { Label = "Integrity" }.With(
                new UiNode(UiKind.Box) { Key = "integrity", Color = integrity <= 30 ? "bad" : integrity <= 70 ? "average" : "good", Text = TguiFormat.JsNumber(integrity) + "%" }),
            new UiNode(UiKind.LabeledItem) { Label = "Charge" }.With(
                charge is { IsNull: false }
                    ? new UiNode(UiKind.Box) { Key = "charge", Color = charge.AsNum() <= 30 ? "bad" : charge.AsNum() <= 70 ? "average" : "good", Text = TguiFormat.JsNumber(charge.AsNum()) + "%" }
                    : new UiNode(UiKind.Box) { Key = "charge", Text = "Not Found" }),
            new UiNode(UiKind.LabeledItem) { Label = "Airtank", Text = airtank is { IsNull: false } ? TguiFormat.ToFixed(airtank.AsNum(), 2) + " kPa" : "Not Equipped" },
            new UiNode(UiKind.LabeledItem) { Label = "Pilot", Text = pilots.Length > 0 ? string.Concat(pilots.Select(p => p + (pilots.Length > 1 ? "|" : ""))) : "None" },
            new UiNode(UiKind.LabeledItem) { Label = "Location", Text = string.IsNullOrEmpty(mech.Str("location")) ? "Unknown" : mech.Str("location") });
        if (cargo is { IsNull: false })
        {
            double used = cargo.AsNum();
            list.With(new UiNode(UiKind.LabeledItem) { Label = "Used Cargo Space" }.With(
                new UiNode(UiKind.Box) { Key = "cargo", Color = used <= 30 ? "good" : used <= 70 ? "average" : "bad", Text = TguiFormat.JsNumber(used) + "%" }));
        }

        return new UiNode(UiKind.Section)
        {
            Key = trackerRef,
            Title = mech.Str("name"),
            Buttons = new UiNode(UiKind.Fragment).With(
                new UiNode(UiKind.Button) { Key = "send_message", Icon = "envelope", Text = "Message", Disabled = pilots.Length < 1, OnClick = () => c.Act("send_message", ("tracker_ref", trackerRef)) },
                new UiNode(UiKind.Button) { Key = "shock", Icon = "wifi", Color = "bad", Text = recharging ? "Recharging..." : "EMP Burst", Disabled = recharging, OnClick = () => c.Act("shock", ("tracker_ref", trackerRef)) }),
        }.With(list);
    }
}
