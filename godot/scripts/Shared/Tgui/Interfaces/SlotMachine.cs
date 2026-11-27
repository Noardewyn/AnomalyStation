using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/SlotMachine.tsx: the slot machine's banner (its pages
// turned every five seconds, the winning text while a prize shows), the five reels of three symbols that roll while the machine
// spins and stop on the server's result, the jackpot and trap symbols, the balance with its Refund button and the Spin button.
// The symbols are drawn from tg's .dmi files by their icon and state (DV664); the banner's blinking cost is drawn bold. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class SlotMachine
{
    // tg: SlotMachine.tsx:162
    private static readonly string[] BannerTexts =
    {
        "SPIN! SPIN!", "WARMED UP!", "HOT SLOTS", "SPIN & WIN!", "BELIEVE IT!", "ZERO 2 HERO!", "JUST ONE MORE", "SPIN OF FATE",
        "BONUS TIME!", "BORN TO SPIN!", "NICE SPIN!", "NO SPIN NO WIN", "BET & FORGET", "HONK 4 LUCK", "DEBT 4 LIFE", "SPINGULARITY",
        "SPIN CITY", "BURN & EARN", "JACKPOT SOON!", "WIN THE DAY!", "FORTUNE CALLS", "INSTANT GOLD!", "DREAM BIGGER!", "WINNERS ONLY!",
        "SPIN IS LIFE", "BIG ONE SOON!", "LUCKY SPIN!", "CASH OUT? NO!",
    };

    // tg: SlotMachine.tsx:193
    private static readonly string[] WinningTexts = { null, "FREE SPINS!", "PRIZE!", "BIG PRIZE!", "JACKPOT!!!" };

    // tg: SlotMachine.tsx:153  getBannerPages: title, only a few credits, prize money, title, jackpot, stats
    private const int BannerPages = 6;

    // the strip's symbols change this often while a reel spins (SlotMachine.scss's strip animation)
    private const double RollSeconds = 0.1;

    private static string PluralS(double amount) => amount == 1 ? "" : "s";

    // tg: SlotMachine.tsx:51
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        var symbols = new Dictionary<string, UiValue>();
        foreach (UiValue symbol in data.Arr("symbols"))
            symbols[symbol.Str("id")] = symbol;
        bool spinning = data.Num("working") == 1;
        double cost = data.Num("cost");
        double balance = data.Num("balance");
        symbols.TryGetValue(data.Str("jackpot_id"), out UiValue jackpotSymbol);
        UiValue trapSymbol = null;
        if (!string.IsNullOrEmpty(data.Str("trap_id")))
            symbols.TryGetValue(data.Str("trap_id"), out trapSymbol);

        var reels = new UiNode(UiKind.Stack) { Key = "reels", Justify = "center" };
        IReadOnlyList<UiValue> reelData = data.Arr("reels");
        for (int i = 0; i < reelData.Count; i++)
            reels.Children.Add(new UiNode(UiKind.StackItem) { Key = "reel:" + i }.With(IconStrip(c, i, data.Arr("symbols"), symbols, reelData[i].Arr("symbols"), spinning)));

        var jackpot = new UiNode(UiKind.Section) { Key = "jackpot", Fill = true, Align = "center" }.With(
            new UiNode(UiKind.Box) { Key = "jackpot-label", TextAlign = "center", Color = "good", Bold = true, Text = "Jackpot:" });
        if (jackpotSymbol != null)
            jackpot.Children.Add(SymbolBox("jackpot-symbol", jackpotSymbol));
        if (trapSymbol != null)
        {
            jackpot.Children.Add(new UiNode(UiKind.Box) { Key = "trap-label", Color = "bad", Bold = true, Text = "Trap:" });
            jackpot.Children.Add(SymbolBox("trap-symbol", trapSymbol));
        }

        return c.Window(300, 445, null,
            Banner(c, data),
            new UiNode(UiKind.Section) { Key = "reels-section" }.With(reels),
            new UiNode(UiKind.Stack) { Key = "bottom", Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "jackpot-item" }.With(jackpot),
                new UiNode(UiKind.StackItem) { Key = "controls", Grow = 1 }.With(new UiNode(UiKind.Stack) { Key = "controls-row", Align = "stretch" }.With(
                    new UiNode(UiKind.StackItem) { Key = "balance-item", Grow = 1 }.With(new UiNode(UiKind.Section)
                    {
                        Key = "balance", Fill = true, Title = "Balance",
                        Buttons = new UiNode(UiKind.Button) { Key = "refund", Text = "Refund", Disabled = balance <= 0, OnClick = () => c.Act("payout") },
                    }.With(new UiNode(UiKind.Box) { Key = "balance-value", TextAlign = "center", FontSize = 2, Text = TguiFormat.FormatMoney(balance) + " cr" })),
                    new UiNode(UiKind.StackItem) { Key = "spin-item", Grow = 1 }.With(new UiNode(UiKind.Section) { Key = "spin-section", Fill = true }.With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "spin", Fluid = true, TextAlign = "center", FontSize = 3, Color = "green", Text = "Spin!",
                            Disabled = spinning || balance < cost, OnClick = () => c.Act("spin"),
                        }))))));
    }

    private static UiNode SymbolBox(string key, UiValue symbol) =>
        new UiNode(UiKind.Box) { Key = key, DmIcon = symbol.Str("icon"), DmIconState = symbol.Str("icon_state"), Size = 1 };

    // tg: SlotMachine.tsx:201
    private static UiNode Banner(TguiContext c, UiValue data)
    {
        string winningText = (int)data.Num("winning") is int w && w >= 0 && w < WinningTexts.Length ? WinningTexts[w] : null;
        if (winningText != null)
            return new UiNode(UiKind.Section) { Key = "banner-winning", Color = "good" }.With(BannerTitle(c, data, winningText));

        // useEffect's setInterval: the page turns every five seconds
        double now = c.Now;
        double start = c.UseState("banner-start", double.NaN);
        if (double.IsNaN(start))
        {
            start = now;
            c.Local.SetQuiet("banner-start", start);
        }
        int page = (int)((now - start) / 5) % BannerPages;
        c.RenderAgainAt(start + ((int)((now - start) / 5) + 1) * 5);
        UiNode content = page switch
        {
            1 => BannerOnlyFewCreds(c, data),
            2 => BannerPrizeMoney(data),
            4 => BannerJackpot(data),
            5 => BannerStats(data),
            _ => BannerTitle(c, data, null),
        };
        return new UiNode(UiKind.Section) { Key = "banner" }.With(content);
    }

    // tg: SlotMachine.tsx:232
    private static UiNode BannerTitle(TguiContext c, UiValue data, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            double balance = data.Num("balance");
            if (balance <= 0)
                text = "INSERT COIN";
            else if (balance <= 5)
                text = "ONE LAST SPIN";
            else
            {
                // useRef(pickRandom(BANNER_TEXTS)): picked once for the window
                int pick = c.UseState("banner-text", -1);
                if (pick < 0)
                {
                    pick = Random.Shared.Next(BannerTexts.Length);
                    c.Local.SetQuiet("banner-text", pick);
                }
                text = BannerTexts[pick];
            }
        }
        return new UiNode(UiKind.Box) { Key = "banner-title", TextAlign = "center", FontSize = 2, Bold = true, Text = text };
    }

    // tg: SlotMachine.tsx:254
    private static UiNode BannerOnlyFewCreds(TguiContext c, UiValue data)
    {
        int variant = c.UseState("banner-variant", -1);
        if (variant < 0)
        {
            variant = Random.Shared.Next(2);
            c.Local.SetQuiet("banner-variant", variant);
        }
        double cost = data.Num("cost");
        if (variant == 1)
        {
            return new UiNode(UiKind.Box) { Key = "few-creds", TextAlign = "center" }.With(
                new UiNode(UiKind.Box) { Key = "line1", Text = "For only " + TguiFormat.JsNumber(cost) + " credit" + PluralS(cost) + "!", Bold = true },
                new UiNode(UiKind.Box) { Key = "line2", FontSize = "12px", Text = "You can fix all your problems!" });
        }
        return new UiNode(UiKind.Box) { Key = "few-creds", TextAlign = "center" }.With(
            new UiNode(UiKind.Box) { Key = "line1", Text = "Only " + TguiFormat.JsNumber(cost) + " credit" + PluralS(cost) + " for a chance" },
            new UiNode(UiKind.Box) { Key = "line2", Text = "to win big!" });
    }

    // tg: SlotMachine.tsx:287
    private static UiNode BannerPrizeMoney(UiValue data)
    {
        double money = data.Num("money");
        return new UiNode(UiKind.Box) { Key = "prize-money", TextAlign = "center" }.With(
            new UiNode(UiKind.Box) { Key = "line1", Text = "Available prize money:" },
            new UiNode(UiKind.Box) { Key = "line2", Bold = true, Text = TguiFormat.JsNumber(money) + " credit" + PluralS(money) });
    }

    // tg: SlotMachine.tsx:300
    private static UiNode BannerJackpot(UiValue data)
    {
        double total = data.Num("money") + data.Num("jackpot");
        return new UiNode(UiKind.Box) { Key = "jackpot-banner", TextAlign = "center" }.With(
            new UiNode(UiKind.Box) { Key = "line1", Text = "Current jackpot:" },
            new UiNode(UiKind.Box) { Key = "line2", Bold = true, Text = TguiFormat.JsNumber(total) + " credit" + PluralS(total) + "!" });
    }

    // tg: SlotMachine.tsx:313
    private static UiNode BannerStats(UiValue data)
    {
        double plays = data.Num("plays");
        double jackpots = data.Num("jackpots");
        return new UiNode(UiKind.Box) { Key = "stats", TextAlign = "center" }.With(
            new UiNode(UiKind.Box) { Key = "line1", FontSize = "13px", Text = "So far people have spun " + TguiFormat.JsNumber(plays) + " time" + PluralS(plays) },
            new UiNode(UiKind.Box) { Key = "line2", Text = "and won " + TguiFormat.JsNumber(jackpots) + " jackpot" + PluralS(jackpots) + "!" });
    }

    // tg: SlotMachine.tsx:338  IconStrip: the reel's three visible symbols; while it spins they roll through random symbols, and it stops
    // on the symbols the server rolled (symbolsNeeded)
    private static UiNode IconStrip(TguiContext c, int reel, IReadOnlyList<UiValue> all, Dictionary<string, UiValue> symbols, IReadOnlyList<UiValue> needed, bool spinning)
    {
        var strip = new UiNode(UiKind.Stack) { Key = "strip", Vertical = true };
        for (int row = 0; row < 3; row++)
        {
            UiValue symbol = null;
            if (spinning && all.Count > 0)
            {
                int frame = (int)(c.Now / RollSeconds);
                symbol = all[(int)((uint)(frame * 7 + reel * 13 + row * 5) % (uint)all.Count)];
            }
            else if (row < needed.Count)
                symbols.TryGetValue(needed[row].AsStr(), out symbol);
            strip.Children.Add(new UiNode(UiKind.StackItem) { Key = "row:" + row }.With(
                symbol != null ? SymbolBox("symbol", symbol) : new UiNode(UiKind.Box) { Key = "empty", Width = "32px", Height = "32px" }));
        }
        if (spinning)
            c.RenderAgainAt(c.Now + RollSeconds);
        return strip;
    }
}
