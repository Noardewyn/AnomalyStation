namespace Anomaly.Client.Shared;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui-panel/styles/tgchat/chat-dark.scss and main.scss (the dark chat
// theme's text: the chat's colour and the classes' colour, weight and style; the rule's line in a comment). M9 (the owner, 2026-10-23):
// the feed at the lower left colours tg's chat lines as tg's chat panel does. Sizes, backgrounds and the alert boxes come with the big chat
// window (M9 slice C). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code); tgui-panel's styles MIT (Aleksej Komarov). See ../tgstation/LICENSE.

/// <summary>The look of a run of chat text: its colour (<c>0xRRGGBBAA</c>), bold, italic, its font scale, its background (0: none) and the
/// colour of the announcement box (<c>chat_alert_*</c>) it is in, if any.</summary>
public readonly record struct ChatStyle(uint Rgba, bool Bold, bool Italic, float Scale = 1f, uint Bg = 0, string Alert = null);

/// <summary>tg's dark chat style sheet: the classes of its spans (<c>notice</c>, <c>warning</c>, <c>radio</c>, ...) as text styles.</summary>
public static class ChatStyles
{
    /// <summary>The chat's text (main.scss <c>--chat-text-color: hsl(210, 50%, 80%)</c>).</summary>
    public static readonly ChatStyle Base = new(Hsl(210, 50, 80), false, false);

    private readonly record struct Rule(string Class, uint? Rgba, bool? Bold, bool? Italic);

    private static Rule R(string name, uint? rgba, bool? bold, bool? italic) => new(name, rgba, bold, italic);

    // In the sheet's order: for an element with several classes the later rule wins (CSS's order for equal selectors).
    private static readonly Rule[] Sheet =
    {
        R("motd", Hsl(213.6, 37.9, 74.1), null, null), // :204
        R("bold", null, true, null), // :230
        R("name", null, true, null), // :230
        R("prefix", null, true, null), // :230
        R("ooc", null, true, null), // :230
        R("looc", null, true, null), // :230
        R("adminooc", null, true, null), // :230
        R("admin", null, true, null), // :230
        R("medal", null, true, null), // :230
        R("yell", null, true, null), // :230
        R("italic", null, null, true), // :244
        R("italics", null, null, true), // :244
        R("ooc", Hsl(47.9, 100, 40), true, null), // :275
        R("adminobserverooc", Hsl(195, 100, 40), true, null), // :280
        R("adminooc", Hsl(226.6, 52.8, 50.2), true, null), // :285
        R("adminsay", Hsl(16.2, 100, 50), true, null), // :290
        R("admin", Hsl(227, 63.5, 60.2), true, null), // :295
        R("name", null, true, null), // :300
        R("deadsay", Hsl(271.9, 100, 87.8), null, null), // :312
        R("binarysay", Hsl(209.6, 100, 55.9), null, null), // :316
        R("radio", Hsl(132.8, 74.4, 45.9), null, null), // :332
        R("sciradio", Hsl(271.6, 91.7, 76.5), null, null), // :336
        R("comradio", Hsl(53, 97.6, 50), null, null), // :340
        R("secradio", Hsl(0, 71.2, 53.7), null, null), // :344
        R("medradio", Hsl(202, 83.6, 64.1), null, null), // :348
        R("engradio", Hsl(17, 87.8, 61.4), null, null), // :352
        R("suppradio", Hsl(33.7, 44.9, 49.8), null, null), // :356
        R("servradio", Hsl(88.1, 60.6, 40.8), null, null), // :360
        R("enteradio", Hsl(157.1, 39.6, 62.4), null, null), // :364
        R("syndradio", Hsl(359.1, 31.8, 42.5), null, null), // :368
        R("gangradio", Hsl(305.2, 57.8, 42.7), null, null), // :372
        R("centcomradio", Hsl(197, 62.6, 39.8), null, null), // :376
        R("aiprivradio", Hsl(332.2, 59.6, 60.2), null, null), // :380
        R("redteamradio", Hsl(0, 100, 63.3), null, null), // :384
        R("blueteamradio", Hsl(240, 98, 59.8), null, null), // :388
        R("greenteamradio", Hsl(120, 98, 59.8), null, null), // :392
        R("yellowteamradio", Hsl(60, 98, 59.8), null, null), // :396
        R("captaincast", Hsl(157.1, 39.6, 62.4), null, null), // :400
        R("yell", null, true, null), // :404
        R("alert", Hsl(0, 74.2, 48.6), null, null), // :408
        R("userdanger", Hsl(0, 73.6, 44.5), true, null), // :412
        R("bolddanger", Hsl(0, 73.6, 44.5), true, null), // :418
        R("danger", Hsl(0, 73.6, 44.5), null, null), // :423
        R("tinydanger", Hsl(0, 73.6, 44.5), null, null), // :427
        R("smalldanger", Hsl(0, 73.6, 44.5), null, null), // :432
        R("warning", Hsl(0, 73.6, 44.5), null, true), // :436
        R("alertwarning", Hsl(0, 100, 50), true, null), // :441
        R("boldwarning", Hsl(0, 73.6, 44.5), true, true), // :446
        R("announce", Hsl(0, 73.6, 44.5), true, null), // :452
        R("boldannounce", Hsl(0, 73.6, 44.5), true, null), // :457
        R("minorannounce", Hsl(0, 73.6, 44.5), true, null), // :462
        R("minoralert", Hsl(213.6, 37.9, 74.1), null, null), // :468
        R("priorityannounce", Hsl(213.6, 37.9, 74.1), true, null), // :473
        R("prioritytitle", Hsl(227, 87.7, 68), true, null), // :479
        R("priorityalert", Hsl(0, 73.6, 44.5), null, null), // :485
        R("greenannounce", Hsl(132.8, 93.4, 29.6), true, null), // :490
        R("rose", Hsl(0, 100, 65.7), null, null), // :495
        R("info", Hsl(226.9, 100, 80.2), null, null), // :499
        R("notice", Hsl(227, 87.7, 68), null, null), // :503
        R("tinynotice", Hsl(227, 87.7, 68), null, null), // :507
        R("tinynoticeital", Hsl(227, 87.7, 68), null, true), // :512
        R("smallnotice", Hsl(227, 87.7, 68), null, null), // :518
        R("smallnoticeital", Hsl(227, 87.7, 68), null, true), // :523
        R("bolditalic", null, true, true), // :529
        R("boldnotice", Hsl(227, 87.7, 68), true, null), // :534
        R("hear", Hsl(227, 87.7, 68), null, true), // :539
        R("adminnotice", Hsl(227, 87.7, 68), null, null), // :544
        R("adminhelp", Hsl(0, 100, 50), true, null), // :548
        R("unconscious", Hsl(213.6, 37.9, 74.1), true, null), // :553
        R("suicide", Hsl(0, 100, 65.7), null, true), // :558
        R("green", Hsl(132.9, 100, 50.6), null, null), // :563
        R("grey", Hsl(0, 0, 51.4), null, null), // :567
        R("red", Hsl(0, 100, 50), null, null), // :571
        R("blue", Hsl(224.1, 100, 56.5), null, null), // :575
        R("nicegreen", Hsl(132.8, 93.4, 29.6), null, null), // :579
        R("boldnicegreen", Hsl(132.8, 93.4, 29.6), true, null), // :583
        R("tinynicegreen", Hsl(132.8, 93.4, 29.6), null, null), // :588
        R("blob", Hsl(16.1, 100, 46.7), null, null), // :593
        R("blobannounce", Hsl(82, 39, 30.2), true, null), // :597
        R("cult", Hsl(2, 43.8, 41.2), null, null), // :603
        R("cult_italic", Hsl(2, 43.8, 41.2), null, true), // :607
        R("cult_bold", Hsl(2, 43.8, 41.2), true, true), // :612
        R("cult_bold_italic", Hsl(2, 43.8, 41.2), true, null), // :618
        R("cult_large", Hsl(2, 43.8, 41.2), true, null), // :624
        R("narsie", Hsl(2, 43.8, 41.2), true, null), // :630
        R("narsiesmall", Hsl(2, 43.8, 41.2), true, null), // :636
        R("colossus", Hsl(358.6, 52.1, 32.7), null, null), // :642
        R("hierophant", Hsl(279.9, 83.6, 59.4), true, true), // :647
        R("hierophant_warning", Hsl(280.3, 82.7, 68.2), null, true), // :653
        R("purple", Hsl(272.2, 58.7, 58.2), null, null), // :658
        R("holoparasite", Hsl(257.1, 12.4, 55.7), null, null), // :662
        R("revennotice", Hsl(272.1, 55.7, 74.3), null, null), // :666
        R("revenboldnotice", Hsl(272.1, 55.7, 74.3), true, null), // :670
        R("revenbignotice", Hsl(272.1, 55.7, 74.3), true, null), // :675
        R("revenminor", Hsl(273.5, 52.7, 48), null, null), // :681
        R("revenwarning", Hsl(275.9, 85.1, 39.6), null, true), // :685
        R("revendanger", Hsl(275.9, 85.1, 39.6), true, null), // :690
        R("deconversion_message", Hsl(272, 100, 63.9), null, true), // :696
        R("ghostalert", Hsl(264, 100, 50), true, true), // :702
        R("alien", Hsl(300, 17.7, 44.3), null, null), // :708
        R("noticealien", Hsl(132.8, 93.4, 29.6), null, null), // :712
        R("alertalien", Hsl(132.8, 93.4, 29.6), true, null), // :716
        R("changeling", Hsl(132.8, 93.4, 29.6), null, true), // :721
        R("alertsyndie", Hsl(0, 100, 50), true, null), // :726
        R("spiderbroodmother", Hsl(272, 100, 50), true, null), // :732
        R("spiderbreacher", Hsl(35, 72.3, 67.5), true, null), // :738
        R("spiderscout", Hsl(242.9, 68, 35.5), true, null), // :744
        R("interface", Hsl(300, 78.6, 25.7), null, null), // :750
        R("tape_recorder", Hsl(0, 100, 50), null, null), // :766
        R("command_headset", null, true, null), // :771
        R("soapbox", null, true, null), // :776
        R("greentext", Hsl(132.8, 93.4, 29.6), null, null), // :805
        R("redtext", Hsl(0, 73.6, 44.5), null, null), // :810
        R("clown", Hsl(326, 100, 72), true, null), // :815
        R("singing", null, null, true), // :822
        R("his_grace", Hsl(119.1, 84.4, 45.3), null, true), // :827
        R("hypnophrase", Hsl(0, 0, 12.5), true, null), // :833
        R("phobia", Hsl(0, 100, 43.3), true, null), // :862
        R("memo", Hsl(75.3, 100, 26.1), null, null), // :895
        R("policy", Hsl(276, 70, 52), null, true), // :905
        R("abductor", Hsl(300, 96, 38.8), null, true), // :912
        R("mind_control", Hsl(320, 71.7, 55.7), true, true), // :917
        R("slime", Hsl(180.9, 100, 41), null, null), // :924
        R("drone", Hsl(60, 100, 50), null, true), // :928
        R("monkey", Hsl(17.8, 50.2, 39.4), null, null), // :934
        R("swarmer", Hsl(219.2, 100, 58.6), null, null), // :938
        R("resonate", Hsl(174.1, 55.4, 36.1), null, null), // :942
        R("mansus", 0x0D9B00FF, null, null), // :951
        R("cyan", 0xBDE0DCFF, null, null), // :956
        R("connectionClosed", 0xFFFFFFFF, null, null), // :961
        R("fatalError", 0xFFFFFFFF, null, null), // :961
        R("text-normal", null, false, false), // :979
        R("tooltip", null, null, true), // :1001
        R("subheader_announcement_text", null, true, null), // :1164
        R("major_announcement_text", Hsl(0, 0, 91.8), true, null), // :1174
        R("minor_announcement_title", null, true, null), // :1185
        R("minor_announcement_text", Hsl(0, 0, 91.8), null, null), // :1196
        R("ooc_alert", Hsl(0, 0, 100), true, null), // :1210
        R("ooc_announcement_text", Hsl(47.9, 100, 40), null, null), // :1221
    };

    /// <summary>The style an element with <paramref name="classes"/> (its <c>class</c> attribute) gives text inside <paramref name="inherited"/>.</summary>
    public static ChatStyle Apply(ChatStyle inherited, string classes)
    {
        if (string.IsNullOrWhiteSpace(classes))
            return inherited;
        var named = new HashSet<string>(classes.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
        ChatStyle style = inherited;
        foreach (Rule rule in Sheet)
        {
            if (!named.Contains(rule.Class))
                continue;
            style = style with { Rgba = rule.Rgba ?? style.Rgba, Bold = rule.Bold ?? style.Bold, Italic = rule.Italic ?? style.Italic };
        }
        foreach (string name in named)
            style = Announcement(style, name);
        return style;
    }

    // chat-dark.scss:1110-1287 (M9 slice D, the announcements): the striped box of an announcement (.chat_alert_<colour>, white bold
    // text) and, inside it, its titles' sizes and colours and its text's background (the stripes' colour 5 % darker; outside a box
    // tg's own greys). The stripes themselves are drawn as the text's background.
    private static ChatStyle Announcement(ChatStyle style, string name)
    {
        const string Box = "chat_alert_";
        if (name.StartsWith(Box, StringComparison.Ordinal) && AlertStripes.ContainsKey(name.Substring(Box.Length)))
            return style with { Rgba = Hsl(0, 0, 100), Bold = true, Alert = name.Substring(Box.Length) };
        string alert = style.Alert;
        switch (name)
        {
            case "major_announcement_title":
                return style with { Scale = 1.75f, Rgba = alert != null ? MajorHeader[alert] : style.Rgba };
            case "minor_announcement_title":
                return style with { Scale = 1.5f, Bold = true, Rgba = alert != null ? MajorHeader[alert] : style.Rgba };
            case "subheader_announcement_text":
                return style with { Scale = 1.25f, Bold = true, Rgba = alert != null ? SubHeader[alert] : style.Rgba };
            case "major_announcement_text":
                return style with { Bg = alert != null ? Darker(AlertStripes[alert]) : Hsl(0, 0, 7.5) };
            case "minor_announcement_text":
                return style with { Bg = alert != null ? Darker(AlertStripes[alert]) : Hsl(0, 0, 12.5) };
        }
        return style;
    }

    private static readonly Dictionary<string, (double H, double S, double L)> AlertStripes = new(StringComparer.Ordinal)
    {
        ["default"] = (198.6, 100, 11.4), ["green"] = (120, 100, 12), ["blue"] = (198.6, 100, 11.4), ["pink"] = (326.2, 100, 9.4),
        ["yellow"] = (51, 100, 17.1), ["orange"] = (35.1, 100, 17.5), ["red"] = (0, 100, 12.9), ["purple"] = (295, 100, 9.4), ["grey"] = (0, 0, 14.5),
    };

    private static readonly Dictionary<string, uint> MajorHeader = new(StringComparer.Ordinal)
    {
        ["default"] = Hsl(192.4, 100, 60), ["green"] = Hsl(150.1, 100, 50), ["blue"] = Hsl(192.4, 100, 60), ["pink"] = Hsl(336.1, 100, 66.1),
        ["yellow"] = Hsl(38.7, 100, 93.9), ["orange"] = Hsl(20.9, 92, 95.1), ["red"] = Hsl(336.1, 100, 66.1), ["purple"] = Hsl(266.5, 84.3, 80), ["grey"] = Hsl(336.1, 100, 66.1),
    };

    private static readonly Dictionary<string, uint> SubHeader = new(StringComparer.Ordinal)
    {
        ["default"] = Hsl(336.1, 100, 66.1), ["green"] = Hsl(336.4, 100, 76.1), ["blue"] = Hsl(336.1, 100, 66.1), ["pink"] = Hsl(192.4, 100, 60),
        ["yellow"] = Hsl(192.4, 100, 60), ["orange"] = Hsl(192.4, 100, 60), ["red"] = Hsl(192.4, 100, 60), ["purple"] = Hsl(192.4, 100, 60), ["grey"] = Hsl(192.4, 100, 60),
    };

    // color.adjust($lightness: -5%, $space: hsl)
    private static uint Darker((double H, double S, double L) c) => Hsl(c.H, c.S, Math.Max(0, c.L - 5));

    /// <summary>CSS <c>hsl(h, s%, l%)</c> as <c>0xRRGGBBFF</c>.</summary>
    public static uint Hsl(double h, double s, double l)
    {
        s /= 100;
        l /= 100;
        double c = (1 - Math.Abs(2 * l - 1)) * s;
        double hp = (h % 360 + 360) % 360 / 60;
        double x = c * (1 - Math.Abs(hp % 2 - 1));
        (double r, double g, double b) = hp switch
        {
            < 1 => (c, x, 0d),
            < 2 => (x, c, 0d),
            < 3 => (0d, c, x),
            < 4 => (0d, x, c),
            < 5 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        double m = l - c / 2;
        static uint Byte(double v) => (uint)Math.Clamp(Math.Round(v * 255), 0, 255);
        return (Byte(r + m) << 24) | (Byte(g + m) << 16) | (Byte(b + m) << 8) | 0xFF;
    }
}
