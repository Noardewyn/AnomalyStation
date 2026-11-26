using System.Text.RegularExpressions;

namespace Anomaly.Client.Shared;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// D184: a chat message tg's chat draws in a box, drawn whole in the chat as a framed block (<c>tgui-panel/styles/tgchat/chat-dark.scss</c>):
/// tg's <c>boxed_message</c> (<c>__DEFINES/chat.dm:48</c>: the scans and readouts, the tip of the round, and as a <c>fieldset_block</c> the
/// admin PMs and adminhelps) in its <c>red_box</c>, <c>green_box</c>, <c>blue_box</c>, <c>purple_box</c> colours, with a thin border and a
/// thick left one; an announcement's <c>chat_alert_&lt;colour&gt;</c> (<c>__HELPERS/announcements.dm</c>) and the OOC announcement's
/// <c>ooc_alert</c>, with a thick border all round. Colours are <c>0xRRGGBBAA</c>.
/// </summary>
/// <param name="Alert">An announcement's box: its border is thick all round (tg's 4 px striped border, drawn in one of its two colours).</param>
public readonly record struct ChatBox(uint Bg, uint Border, bool Alert = false)
{
    public static readonly ChatBox Plain = new(ChatStyles.Hsl(220, 10, 10), Hsla(220, 40, 75, 0.25));
    public static readonly ChatBox Red = new(ChatStyles.Hsl(0, 20, 10), Hsla(0, 100, 50, 0.5));
    public static readonly ChatBox Green = new(ChatStyles.Hsl(140, 20, 10), Hsla(120, 100, 50, 0.5));
    public static readonly ChatBox Blue = new(ChatStyles.Hsl(220, 20, 10), Hsla(225, 90, 65, 0.5));
    public static readonly ChatBox Purple = new(ChatStyles.Hsl(260, 25, 12.5), Hsla(260, 100, 75, 0.5));

    /// <summary>chat-dark.scss <c>.ooc_alert</c>: the admins' OOC announcement.</summary>
    public static readonly ChatBox OocAlert = new(ChatStyles.Hsl(50.6, 100, 15.1), ChatStyles.Hsl(47.9, 100, 40), true);

    // chat-dark.scss $alert-stripe-alternate-colors: the second colour of an announcement's stripes, its border here
    private static readonly Dictionary<string, uint> AlertBorders = new(StringComparer.Ordinal)
    {
        ["default"] = ChatStyles.Hsl(198.3, 100, 13.5), ["green"] = ChatStyles.Hsl(120, 100, 13.9), ["blue"] = ChatStyles.Hsl(198.3, 100, 13.5),
        ["pink"] = ChatStyles.Hsl(325.3, 100, 12.5), ["yellow"] = ChatStyles.Hsl(50.6, 100, 15.1), ["orange"] = ChatStyles.Hsl(37, 100, 21),
        ["red"] = ChatStyles.Hsl(0, 100, 16.1), ["purple"] = ChatStyles.Hsl(295.1, 100, 12), ["grey"] = ChatStyles.Hsl(0, 0, 16.1),
    };

    private static readonly Regex ClassAttribute = new("class=['\"]([^'\"]*)['\"]", RegexOptions.CultureInvariant);

    /// <summary>
    /// The box of a chat message: the first element of its <paramref name="html"/> with a box's class; else, for a line of tg's admin PM
    /// type (<see cref="ChatLog.TypeAdminPm"/>: the player's own "PM to-Admins", the PMs relayed to the other admins), the red box of
    /// tg's admin PM (DV598); else null.
    /// </summary>
    public static ChatBox? Of(string html, string type = null)
    {
        if (html != null)
        {
            foreach (Match match in ClassAttribute.Matches(html))
            {
                string[] names = match.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (string name in names)
                {
                    if (name == "boxed_message")
                        return names.Contains("red_box") ? Red : names.Contains("green_box") ? Green : names.Contains("blue_box") ? Blue
                            : names.Contains("purple_box") ? Purple : Plain;
                    if (name == "ooc_alert")
                        return OocAlert;
                    if (name.StartsWith("chat_alert_", StringComparison.Ordinal) && AlertBorders.TryGetValue(name.Substring("chat_alert_".Length), out uint border))
                    {
                        (double h, double s, double l) = ChatStyles.AlertStripes[name.Substring("chat_alert_".Length)];
                        return new ChatBox(ChatStyles.Hsl(h, s, l), border, true);
                    }
                }
            }
        }
        return type == ChatLog.TypeAdminPm ? Red : null;
    }

    private static uint Hsla(double h, double s, double l, double a) => (ChatStyles.Hsl(h, s, l) & 0xFFFFFF00) | (uint)Math.Round(a * 255);
}
