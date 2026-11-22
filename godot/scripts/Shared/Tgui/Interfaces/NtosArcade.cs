using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosArcade.tsx (gaps S4c, D327): Outbomb Cuban Pete Ultra,
// the player's health and magic, the boss's health and picture (tg's arcade GIFs, icons/ui/arcade/), the turn's status and the
// buttons. AnimatedNumber is the plain number. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosArcade
{
    private static readonly (string, double, double)[] PlayerHealthRanges =
    {
        ("olive", 31, double.PositiveInfinity),
        ("good", 20, 31),
        ("average", 10, 20),
        ("bad", double.NegativeInfinity, 10),
    };

    private static readonly (string, double, double)[] PlayerMagicRanges =
    {
        ("purple", 11, double.PositiveInfinity),
        ("violet", 3, 11),
        ("bad", double.NegativeInfinity, 3),
    };

    private static readonly (string, double, double)[] BossRanges =
    {
        ("good", 30, double.PositiveInfinity),
        ("average", 5, 30),
        ("bad", double.NegativeInfinity, 5),
    };

    // tg: NtosArcade.tsx:29
    public static UiNode Render(TguiContext c) =>
        NtosWindow.Render(c, 450, 350, null, new UiNode(UiKind.Section) { Title = "Outbomb Cuban Pete Ultra", TextAlign = "center" }.With(
            new UiNode(UiKind.Stack) { Key = "board", Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "player" }.With(PlayerStats(c)),
                new UiNode(UiKind.StackItem) { Key = "boss" }.With(BossBar(c))),
            BottomButtons(c)));

    // tg: NtosArcade.tsx:48
    private static UiNode PlayerStats(TguiContext c)
    {
        UiValue data = c.Data;
        double hp = data.Num("PlayerHitpoints");
        double mp = data.Num("PlayerMP");
        return new UiNode(UiKind.Box).With(
            new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Key = "hp", Label = "Player Health" }.With(new UiNode(UiKind.ProgressBar)
                {
                    Value = hp,
                    MinValue = 0,
                    MaxValue = 30,
                    Ranges = PlayerHealthRanges,
                    Text = TguiFormat.JsNumber(hp) + "HP",
                }),
                new UiNode(UiKind.LabeledItem) { Key = "mp", Label = "Player Magic" }.With(new UiNode(UiKind.ProgressBar)
                {
                    Value = mp,
                    MinValue = 0,
                    MaxValue = 10,
                    Ranges = PlayerMagicRanges,
                    Text = TguiFormat.JsNumber(mp) + "MP",
                })),
            new UiNode(UiKind.Divider),
            new UiNode(UiKind.NoticeBox) { Key = "status", Color = !data.Truthy("PauseState") ? "danger" : null, Text = data.Str("Status") });
    }

    // tg: NtosArcade.tsx:92
    private static UiNode BossBar(TguiContext c)
    {
        UiValue data = c.Data;
        double hp = data.Num("Hitpoints");
        return new UiNode(UiKind.Box).With(
            new UiNode(UiKind.ProgressBar) { Key = "bosshp", Value = hp, MinValue = 0, MaxValue = 45, Ranges = BossRanges, Text = TguiFormat.JsNumber(hp) + "HP" },
            new UiNode(UiKind.Section) { Key = "bosspicture", Width = "156px", TextAlign = "center" }.With(
                new UiNode(UiKind.Box) { Key = "bossimage", DmIcon = "icons/ui/arcade/" + data.Str("BossID") }));
    }

    // tg: NtosArcade.tsx:121
    private static UiNode BottomButtons(TguiContext c)
    {
        UiValue data = c.Data;
        bool active = data.Truthy("GameActive");
        bool paused = data.Truthy("PauseState");
        double tickets = data.Num("TicketCount");
        return new UiNode(UiKind.Box) { Key = "buttons" }.With(
            new UiNode(UiKind.Button) { Key = "attack", Icon = "fist-raised", Tooltip = "Go in for the kill!", Disabled = !active || paused, Text = "Attack!", OnClick = () => c.Act("Attack") },
            new UiNode(UiKind.Button) { Key = "heal", Icon = "band-aid", Tooltip = "Heal yourself!", Disabled = !active || paused, Text = "Heal!", OnClick = () => c.Act("Heal") },
            new UiNode(UiKind.Button) { Key = "recharge", Icon = "magic", Tooltip = "Recharge your magic!", Disabled = !active || paused, Text = "Recharge!", OnClick = () => c.Act("Recharge_Power") },
            new UiNode(UiKind.Box) { Key = "restart" }.With(
                new UiNode(UiKind.Button) { Key = "start", Icon = "sync-alt", Tooltip = "One more game couldn't hurt.", Disabled = active, Text = "Begin Game", OnClick = () => c.Act("Start_Game") },
                new UiNode(UiKind.Button) { Key = "tickets", Icon = "ticket-alt", Tooltip = "Claim at your local Arcade Computer for Prizes!", Disabled = active, Text = "Claim Tickets", OnClick = () => c.Act("Dispense_Tickets") }),
            new UiNode(UiKind.Box) { Key = "earned", Color = tickets >= 1 ? "good" : "normal", Text = "Earned Tickets: " + TguiFormat.JsNumber(tickets) });
    }
}
