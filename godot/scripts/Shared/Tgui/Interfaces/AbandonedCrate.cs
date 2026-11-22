using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AbandonedCrate.tsx (gaps ST3): the abandoned crate's
// deca-code lock, the warning of how many failed attempts are left before its anti-tamper bomb and the table of the previous
// attempts with their bulls (right digits in the right places) and cows (right digits in the wrong places)
// (crates/abandoned_crates/abandoned_crates.dm:175 ui_data). Original copyright and license: tgstation, AGPL-3.0 (code).
// See ../tgstation/LICENSE.

public static class AbandonedCrate
{
    // tg: AbandonedCrate.tsx:24
    private const string BullsCowsInfo = "Codes are made of a series of non-repeating digits.\n"
        + "Each wrong guess will return the number of correct digits in correct locations,\n"
        + "and the number of correct digits in incorrect locations.";

    // tg: AbandonedCrate.tsx:18
    private static string CheckAttempts(double attemptsToCheck) =>
        attemptsToCheck == 1 ? "on next failed access attempt." : "after " + TguiFormat.JsNumber(attemptsToCheck) + " failed access attempts.";

    // tg: AbandonedCrate.tsx:28
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> previousAttempts = data.Arr("previous_attempts");
        double attemptsLeft = data.Num("attempts_left");

        var table = new UiNode(UiKind.Table) { Key = "attempts" };
        if (previousAttempts.Count > 0)
            table.With(new UiNode(UiKind.TableRow) { Key = "header", Bold = true, FontSize = "125%" }.With(
                new UiNode(UiKind.TableCell) { Key = "h-attempt", Color = "white", TextAlign = "center", Text = "Attempt" },
                new UiNode(UiKind.TableCell) { Key = "h-bulls", TextAlign = "center" }.With(
                    new UiNode(UiKind.Button) { Key = "bulls-info", Icon = "check", Color = "green", Tooltip = "Correct digits at correct positions" }),
                new UiNode(UiKind.TableCell) { Key = "h-cows", TextAlign = "center" }.With(
                    new UiNode(UiKind.Button) { Key = "cows-info", Icon = "asterisk", Color = "yellow", Tooltip = "Correct digits at incorrect positions" })));
        int index = 0;
        foreach (UiValue attempt in previousAttempts)
        {
            // tg keys the rows by the attempt; a repeated guess would share a key, so the index is added
            string key = "attempt:" + index++ + ":" + attempt.Str("attempt");
            table.With(new UiNode(UiKind.TableRow) { Key = key }.With(
                new UiNode(UiKind.TableCell) { Key = "attempt", TextAlign = "center", Color = "white", FontSize = "125%", Text = attempt.Str("attempt") },
                new UiNode(UiKind.TableCell) { Key = "bulls", TextAlign = "center", Color = "green", FontSize = "125%", Text = TguiFormat.JsNumber(attempt.Num("bulls")) },
                new UiNode(UiKind.TableCell) { Key = "cows", TextAlign = "center", Color = "yellow", FontSize = "125%", Text = TguiFormat.JsNumber(attempt.Num("cows")) }));
        }

        var section = new UiNode(UiKind.Section)
        {
            Key = "lock",
            Title = "Deca-Code Lock",
            Buttons = new UiNode(UiKind.Button) { Key = "info", Icon = "info", Tooltip = BullsCowsInfo },
        }.With(
            new UiNode(UiKind.NoticeBox) { Key = "warning", Color = "bad", Text = "Anti-Tamper Bomb will activate " + CheckAttempts(attemptsLeft) },
            table);
        // tg: AbandonedCrate.tsx:33  <Window width={335} height={180 + previous_attempts.length * 19}>
        return c.Window(335, 180 + previousAttempts.Count * 19, null, section);
    }
}
