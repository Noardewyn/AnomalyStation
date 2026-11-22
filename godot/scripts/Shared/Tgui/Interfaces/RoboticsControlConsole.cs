using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/RoboticsControlConsole.jsx (silicons SI2, D326: the
// robotics control console's cyborgs and drones). Engine-free (compiled into the tests too). useSharedState is the window's state.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class RoboticsControlConsole
{
    // tg: RoboticsControlConsole.jsx:13
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        int tab = c.UseState("tab", 1);
        bool canHack = data.Truthy("can_hack");
        bool canDetonate = data.Truthy("can_detonate");
        IReadOnlyList<UiValue> cyborgs = data.Arr("cyborgs");
        IReadOnlyList<UiValue> drones = data.Arr("drones");

        var tabs = new UiNode(UiKind.Tabs) { Key = "tabs" }.With(
            new UiNode(UiKind.Tab) { Key = "tab:cyborgs", Icon = "list", Selected = tab == 1, Text = "Cyborgs (" + cyborgs.Count + ")", OnClick = () => c.SetState("tab", 1) },
            new UiNode(UiKind.Tab) { Key = "tab:drones", Icon = "list", Selected = tab == 2, Text = "Drones (" + drones.Count + ")", OnClick = () => c.SetState("tab", 2) });

        var content = new UiNode(UiKind.Fragment).With(tabs);
        if (tab == 1)
            content.With(Cyborgs(c, cyborgs, canHack, canDetonate));
        if (tab == 2)
            content.With(Drones(c, drones));
        // <Window width={500} height={460}><Window.Content scrollable>
        return c.Window(500, 460, null, content);
    }

    private static UiNode Status(string color, string text) => new UiNode(UiKind.Box) { Key = "value", Color = color, Text = text };

    // tg: RoboticsControlConsole.jsx:52
    private static UiNode Cyborgs(TguiContext c, IReadOnlyList<UiValue> cyborgs, bool canHack, bool canDetonate)
    {
        if (cyborgs.Count == 0)
            return new UiNode(UiKind.NoticeBox) { Key = "none", Text = "No cyborg units detected within access parameters" };
        var list = new UiNode(UiKind.Fragment) { Key = "cyborgs" };
        foreach (UiValue cyborg in cyborgs)
        {
            string @ref = cyborg.Str("ref");
            bool lockedDown = cyborg.Truthy("locked_down");
            bool notResponding = cyborg.Truthy("status");
            var buttons = new UiNode(UiKind.Fragment);
            if (canHack && !cyborg.Truthy("emagged"))
                buttons.With(new UiNode(UiKind.Button) { Key = "hack", Icon = "terminal", Text = "Hack", Color = "bad", OnClick = () => c.Act("magbot", ("ref", @ref)) });
            buttons.With(c.ButtonConfirm("stop:" + @ref, new UiNode(UiKind.Button)
            {
                Icon = lockedDown ? "unlock" : "lock",
                Color = lockedDown ? "good" : "default",
                Text = lockedDown ? "Release" : "Lockdown",
                OnClick = () => c.Act("stopbot", ("ref", @ref)),
            }));
            if (canDetonate)
                buttons.With(c.ButtonConfirm("kill:" + @ref, new UiNode(UiKind.Button)
                {
                    Icon = "bomb",
                    Text = "Detonate",
                    Color = "bad",
                    OnClick = () => c.Act("killbot", ("ref", @ref)),
                }));

            UiValue chargeValue = cyborg.Get("charge");
            bool hasCharge = chargeValue is { Kind: UiValueKind.Number };
            double charge = hasCharge ? chargeValue.Number : 0;
            string master = cyborg.Str("synchronization");
            list.With(new UiNode(UiKind.Section) { Key = @ref, Title = cyborg.Str("name"), Buttons = buttons }.With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Key = "status", Label = "Status" }.With(Status(
                        notResponding ? "bad" : lockedDown ? "average" : "good",
                        notResponding ? "Not Responding" : lockedDown ? "Locked Down" : "Nominal")),
                    new UiNode(UiKind.LabeledItem) { Key = "charge", Label = "Charge" }.With(Status(
                        charge <= 30 ? "bad" : charge <= 70 ? "average" : "good",
                        hasCharge ? charge + "%" : "Not Found")),
                    new UiNode(UiKind.LabeledItem) { Key = "model", Label = "Model", Text = cyborg.Str("module") },
                    new UiNode(UiKind.LabeledItem) { Key = "ai", Label = "Master AI" }.With(Status(
                        cyborg.Truthy("synchronization") ? "default" : "average",
                        cyborg.Truthy("synchronization") ? master : "None")))));
        }
        return list;
    }

    // tg: RoboticsControlConsole.jsx:145
    private static UiNode Drones(TguiContext c, IReadOnlyList<UiValue> drones)
    {
        if (drones.Count == 0)
            return new UiNode(UiKind.NoticeBox) { Key = "none", Text = "No drone units detected within access parameters" };
        return new UiNode(UiKind.Fragment) { Key = "drones" }.With(drones.Select(drone =>
        {
            string @ref = drone.Str("ref");
            bool notResponding = drone.Truthy("status");
            return new UiNode(UiKind.Section)
            {
                Key = @ref,
                Title = drone.Str("name"),
                Buttons = c.ButtonConfirm("killdrone:" + @ref, new UiNode(UiKind.Button)
                {
                    Icon = "bomb",
                    Text = "Detonate",
                    Color = "bad",
                    OnClick = () => c.Act("killdrone", ("ref", @ref)),
                }),
            }.With(new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Key = "status", Label = "Status" }.With(Status(
                    notResponding ? "bad" : "good",
                    notResponding ? "Not Responding" : "Nominal"))));
        }).ToArray());
    }
}
