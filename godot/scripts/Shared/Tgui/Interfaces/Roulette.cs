using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Roulette/ (index.tsx, Board.tsx, NumberCell.tsx,
// BetTable.tsx, helpers.tsx): the roulette table's board of numbers, dozens, columns and outside bets, each a button that picks the bet
// type, and the bet table with the last spin in its colour, the current bet, the bet amounts with a custom one, the house balance and
// the bolts. The board's table cells that span rows or columns are laid out as rows of fixed-width buttons. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Roulette
{
    private const string CellWidth = "32px";

    // tg: Board.tsx:6-25
    private static readonly int[] FirstRow = { 3, 6, 9, 12, 15, 18, 21, 24, 27, 30, 33, 36 };
    private static readonly int[] SecondRow = { 2, 5, 8, 11, 14, 17, 20, 23, 26, 29, 32, 35 };
    private static readonly int[] ThirdRow = { 1, 4, 7, 10, 13, 16, 19, 22, 25, 28, 31, 34 };
    private static readonly (string Value, string Text)[] FourthRow = { ("s1-12", "1st 12"), ("s13-24", "2nd 12"), ("s25-36", "3rd 12") };
    private static readonly (string Color, string Text, string Value)[] FifthRow =
    {
        ("transparent", "1-18", "s1-18"), ("transparent", "Even", "even"), ("black", "Black", "black"),
        ("red", "Red", "red"), ("transparent", "Odd", "odd"), ("transparent", "19-36", "s19-36"),
    };

    // tg: Roulette/index.tsx:5
    public static UiNode Render(TguiContext c)
    {
        c.WindowTheme = "cardtable";
        return c.Window(570, 520, null, Board(c), BetTable(c));
    }

    // tg: Roulette/helpers.tsx:1
    public static string NumberColor(double number)
    {
        bool inRedOddRange = (number >= 1 && number <= 10) || (number >= 19 && number <= 28);
        if (number % 2 == 1)
            return inRedOddRange ? "red" : "black";
        return inRedOddRange ? "black" : "red";
    }

    // tg: Roulette/NumberCell.tsx:18
    private static UiNode Cell(TguiContext c, string color, string text, string value, int span = 1, string height = null) =>
        new UiNode(UiKind.Button)
        {
            Key = "bet:" + value, Color = color, Text = text, TextAlign = "center", Width = span == 1 ? CellWidth : (32 * span) + "px", Height = height,
            OnClick = () => c.Act("ChangeBetType", ("type", value)),
        };

    private static UiNode Row(string key, IEnumerable<UiNode> cells) => new UiNode(UiKind.Stack) { Key = key }.With(cells.Select((cell, i) => new UiNode(UiKind.StackItem) { Key = "cell:" + cell.Key }.With(cell)));

    // tg: Roulette/Board.tsx:27
    private static UiNode Board(TguiContext c)
    {
        UiNode zero = Cell(c, "transparent", "0", "0", 1, "96px");
        var numbers = new UiNode(UiKind.Stack) { Key = "numbers", Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "row1" }.With(Row("row1", FirstRow.Select(n => Cell(c, NumberColor(n), n.ToString(), n.ToString())).Append(Cell(c, "transparent", "2 to 1", "s3rd col")))),
            new UiNode(UiKind.StackItem) { Key = "row2" }.With(Row("row2", SecondRow.Select(n => Cell(c, NumberColor(n), n.ToString(), n.ToString())).Append(Cell(c, "transparent", "2 to 1", "s2nd col")))),
            new UiNode(UiKind.StackItem) { Key = "row3" }.With(Row("row3", ThirdRow.Select(n => Cell(c, NumberColor(n), n.ToString(), n.ToString())).Append(Cell(c, "transparent", "2 to 1", "s1st col")))),
            new UiNode(UiKind.StackItem) { Key = "row4" }.With(Row("row4", FourthRow.Select(d => Cell(c, "transparent", d.Text, d.Value, 4)))),
            new UiNode(UiKind.StackItem) { Key = "row5" }.With(Row("row5", FifthRow.Select(o => Cell(c, o.Color, o.Text, o.Value, 2)))));
        return new UiNode(UiKind.Box) { Key = "board", TextAlign = "center" }.With(new UiNode(UiKind.Stack) { Key = "board-stack", Justify = "center" }.With(
            new UiNode(UiKind.StackItem) { Key = "zero" }.With(zero),
            new UiNode(UiKind.StackItem) { Key = "numbers" }.With(numbers)));
    }

    // tg: Roulette/BetTable.tsx:22
    private static UiNode BetTable(TguiContext c)
    {
        UiValue data = c.Data;
        double lastSpin = data.Num("LastSpin");
        double houseBalance = data.Num("HouseBalance");
        double customBet = c.UseState("custom-bet", 500.0);
        string betType = data.Str("BetType");
        if (betType.StartsWith('s'))
            betType = betType.Substring(1);

        UiNode Amount(double amount) => new UiNode(UiKind.Button)
        {
            Key = "amount:" + amount, Fluid = true, Text = "Bet " + TguiFormat.JsNumber(amount) + " cr", OnClick = () => c.Act("ChangeBetAmount", ("amount", amount)),
        };

        return new UiNode(UiKind.Table) { Key = "bet-table" }.With(
            new UiNode(UiKind.TableRow) { Key = "header" }.With(
                new UiNode(UiKind.TableCell) { Key = "last-header", Bold = true, TextAlign = "center", Text = "Last Spin:" },
                new UiNode(UiKind.TableCell) { Key = "bet-header", Bold = true, TextAlign = "center", Text = "Current Bet:" }),
            new UiNode(UiKind.TableRow) { Key = "values" }.With(
                new UiNode(UiKind.TableCell) { Key = "last-spin", TextAlign = "center", FontSize = "40px", Bold = true, Text = TguiFormat.JsNumber(lastSpin), Color = NumberColor(lastSpin) },
                new UiNode(UiKind.TableCell) { Key = "bets" }.With(
                    new UiNode(UiKind.Box) { Key = "current", Bold = true, FontSize = "20px", TextAlign = "center", Text = TguiFormat.JsNumber(data.Num("BetAmount")) + " cr on " + betType },
                    Amount(10), Amount(50), Amount(100), Amount(500),
                    new UiNode(UiKind.Stack) { Key = "custom" }.With(
                        new UiNode(UiKind.StackItem) { Key = "custom-button", Grow = 1 }.With(new UiNode(UiKind.Button)
                        {
                            Key = "custom-bet", Fluid = true, Text = "Bet custom amount...", OnClick = () => c.Act("ChangeBetAmount", ("amount", customBet)),
                        }),
                        new UiNode(UiKind.StackItem) { Key = "custom-input" }.With(new UiNode(UiKind.NumberInput)
                        {
                            Key = "custom-amount", Value = customBet, MinValue = 0, MaxValue = 1000, Step = 10, StepPixelSize = 4, Width = "40px",
                            OnChange = value => c.SetState("custom-bet", value),
                        })))),
            new UiNode(UiKind.TableRow) { Key = "swipe" }.With(
                new UiNode(UiKind.TableCell) { Key = "swipe-cell", Bold = true, FontSize = "14px", TextAlign = "center", Text = "Swipe an ID card with a connected account to spin!" }),
            new UiNode(UiKind.TableRow) { Key = "house" }.With(
                new UiNode(UiKind.TableCell) { Key = "house-balance", Text = "House Balance: " + (houseBalance != 0 ? TguiFormat.JsNumber(houseBalance) + " cr" : "None") },
                new UiNode(UiKind.TableCell) { Key = "bolts-cell" }.With(new UiNode(UiKind.Button)
                {
                    Key = "anchor", Fluid = true, Color = "transparent", TextAlign = "center", Text = data.Truthy("IsAnchored") ? "Bolted" : "Unbolted",
                    OnClick = () => c.Act("anchor"),
                })));
    }
}
